﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Configuration;
using System.Text;
using System.Net.Sockets;
using System.Net;
using System.IO;
using SkyNet20.Utility;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;

using ProtoBuf;
using SkyNet20.Network;
using SkyNet20.Network.Commands;
using SkyNet20.Extensions;
using SkyNet20.SDFS.Responses;
using SkyNet20.SDFS;
using SkyNet20.SDFS.Requests;
using SkyNet20.Sava;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using SkyNet20.Sava.UDF;
using SkyNet20.Sava.Communication;

namespace SkyNet20
{
    public class SkyNetNode
    {
        private ConcurrentDictionary<string, SkyNetNodeInfo> machineList = new ConcurrentDictionary<string, SkyNetNodeInfo>(2, 10);
        private Dictionary<string, string> localDnsCache = new Dictionary<string, string>();
        private SkyNetNodeInfo[] knownIntroducers;
        private String machineId;
        private IPHostEntry hostEntry;
        private StreamWriter logFileWriter;
        private String logFilePath;
        private bool isIntroducer;
        private bool isConnected = false;
        private bool isSavaMaster;
        private bool isSavaBackup;
        private Dictionary<string, int> savaWorkerCompletion = new Dictionary<string, int>();
        public List<SkyNetNodeInfo> savaMachines = new List<SkyNetNodeInfo>();
        private int currentIteration;

        private Job runningJob;
        private bool jobHasFailed;

        private Worker worker;
        private IPAddress IPAddress
        {
            get
            {
                return this.GetIpAddress(this.hostEntry.HostName);
            }
        }

        //// Master properties:

        // filename | machines | timestamp | last instruction time stamp
        private Dictionary<string, Tuple<List<string>, DateTime?, DateTime>> indexFile =
            new Dictionary<string, Tuple<List<string>, DateTime?, DateTime>>();

        // filename | lastPutRequestDateTime
        private Dictionary<string, DateTime> fileLastUpdatedIndex = new Dictionary<string, DateTime>();

        /// <summary>
        /// Initializes the instance of <see cref="SkyNetNode"/> class.
        /// </summary>
        public SkyNetNode()
        {
            foreach (var machine in SkyNetConfiguration.HostNames)
            {
                this.localDnsCache.Add(this.GetIpAddress(machine).ToString(), machine);
            }

            this.hostEntry = Dns.GetHostEntry(Dns.GetHostName());
            this.machineId = SkyNetNodeInfo.GetMachineId(this.GetIpAddress(this.hostEntry.HostName));

            var machines = SkyNetConfiguration.Machines;
            this.isIntroducer = machines.ContainsKey(this.hostEntry.HostName) ? SkyNetConfiguration.Machines[this.hostEntry.HostName].IsIntroducer : false;
            this.knownIntroducers = machines
                .Where(kv => kv.Value.IsIntroducer == true)
                .Select(kv => new SkyNetNodeInfo(kv.Key, SkyNetNodeInfo.GetMachineId(this.GetIpAddress(kv.Key)))).ToArray();
            isSavaMaster = machines.ContainsKey(this.hostEntry.HostName) ? SkyNetConfiguration.Machines[this.hostEntry.HostName].IsSavaMaster : false;
            isSavaBackup = machines.ContainsKey(this.hostEntry.HostName) ? SkyNetConfiguration.Machines[this.hostEntry.HostName].IsSavaBackupMaster : false;

            string machineNumber = this.GetMachineNumber(this.hostEntry.HostName);
            string logPath = SkyNetConfiguration.LogPath;

            logFilePath = SkyNetConfiguration.LogPath
                + Path.DirectorySeparatorChar
                + $"vm.{machineNumber}.log";

            if (Directory.Exists(logPath))
            {
                Directory.Delete(logPath, true);
            }

            Directory.CreateDirectory(logPath);

            Storage.Initialize();

            logFileWriter = File.AppendText(logFilePath);
            logFileWriter.AutoFlush = true;
        }
        

        //// Master Methods:

        /// Get Master Node
        private SortedList<int, SkyNetNodeInfo> GetMasterNodes()
        {
            SortedList<int, SkyNetNodeInfo> ret = new SortedList<int, SkyNetNodeInfo>();

            foreach (KeyValuePair<string, SkyNetNodeInfo> kvp in this.machineList.Where(x => x.Value.IsMaster == true && x.Value.Status == Status.Alive))
            {
                SkyNetNodeInfo node = kvp.Value;
                string sMachineNumber = this.GetMachineNumber(node.HostName);
                int iMachineNumber = 100;
                bool bParse = Int32.TryParse(sMachineNumber, out iMachineNumber);
                if (!bParse)
                    this.LogVerbose($"Parsing machine number failed for {node.HostName}");

                if (kvp.Value.IsMaster && !ret.ContainsKey(iMachineNumber))
                    ret.Add(iMachineNumber, kvp.Value);
            }

            return ret;
        }

        /// Get Active Master Node
        private SkyNetNodeInfo GetActiveMaster()
        {
            SortedList<int, SkyNetNodeInfo> masters = GetMasterNodes();

            foreach (KeyValuePair<int, SkyNetNodeInfo> kvp in masters)
            {
                if (kvp.Key < 11)
                    return kvp.Value;
            }

            return null;
        }

        private bool IsActiveMaster()
        {
            return this.isConnected && GetActiveMaster()?.MachineId == this.machineId;
        }

        /// Get file save locations
        private List<SkyNetNodeInfo> GetMachineLocationsForFile(string filename)
        {
            List<SkyNetNodeInfo> ret = new List<SkyNetNodeInfo>();
            List<SkyNetNodeInfo> nodes = this.machineList.Values.ToList();

            int machineIndex = GetMachineLocationFromHash(filename);
            SkyNetNodeInfo node1 = nodes[machineIndex];
            ret.Add(node1);

            for (int i = machineIndex + 1; i < nodes.Count; i++)
            {
                if (ret.Count >= 3)
                    break;

                SkyNetNodeInfo node = nodes[i];

                ret.Add(node);
            }

            for (int i = machineIndex - 1; i > 0; i--)
            {
                if (ret.Count >= 3)
                    break;

                SkyNetNodeInfo node = nodes[i];

                ret.Add(node);
            }

            return ret;
        }

        private int GetMachineLocationFromHash(string filename)
        {
            int count = this.machineList.Count;

            int ret = filename.GetHashCode() % (count);

            return ret;
        }

        /// Put
        private async Task<OperationResult> ProcessPutFromClient(string filename, byte[] content)
        {
            // Send an Update message to those nodes
            List<SkyNetNodeInfo> nodes = GetExistingOrNewNodesForFile(filename);
            return await SendPutToNodes(filename, nodes, content);
        }

        private async Task<OperationResult> SendPutToNodes(string filename, List<SkyNetNodeInfo> nodes, byte[] content)
        {
            DateTime timestamp = DateTime.UtcNow;

            // Send an Update message to those nodes
            OperationResult result = await SendPutCommandToNodes(filename, content, nodes, timestamp);

            if (result.success)
            {
                DateTime previousTimeStamp = DateTime.MinValue;
                if (indexFile.ContainsKey(filename))
                {
                    previousTimeStamp = indexFile[filename].Item3;
                }

                this.indexFile[filename] = new Tuple<List<string>, DateTime?, DateTime>(
                    nodes.Select(x => x.MachineId).ToList(),
                    timestamp,
                    timestamp.CompareTo(previousTimeStamp) < 1 ? previousTimeStamp : timestamp);
            }

            return result;
        }

        private List<SkyNetNodeInfo> GetNewNodesForFile(string filename)
        {
            List<SkyNetNodeInfo> results = new List<SkyNetNodeInfo>();
            var ringList = this.GetRingList();
            Random r = new Random();
            int index = r.Next(ringList.Count);

            var primary = ringList.Where(kvp => ringList.IndexOfKey(kvp.Key) == index).First().Value;
            results.Add(primary);
            results.AddRange(GetSuccessors(primary, ringList));

            return results;
        }

        private List<SkyNetNodeInfo> GetExistingOrNewNodesForFile(string filename)
        {
            List<SkyNetNodeInfo> nodes;
            if (!this.indexFile.ContainsKey(filename))
            {
                nodes = GetNewNodesForFile(filename);
            }
            else
            {
                // Existing nodes
                Tuple<List<string>, DateTime?, DateTime> index = this.indexFile[filename];

                nodes = new List<SkyNetNodeInfo>();

                foreach (string machine in index.Item1)
                {
                    if (!this.machineList.TryGetValue(machine, out SkyNetNodeInfo existingMachine) || existingMachine.Status != Status.Alive)
                        continue;

                    nodes.Add(existingMachine);
                }
            }

            return nodes;
        }

        private async Task<OperationResult> SendPutCommandToNodes(string filename, byte[] content, List<SkyNetNodeInfo> nodes, DateTime timestamp)
        {
            byte[] message;

            using (MemoryStream stream = new MemoryStream())
            {
                SkyNetPacketHeader header = new SkyNetPacketHeader
                {
                    MachineId = this.machineId,
                    PayloadType = PayloadType.PutFile,
                };

                PutFileCommand putFileCommand = new PutFileCommand
                {
                    filename = filename,
                    content = content,
                    instructionTime = timestamp
                };

                Serializer.SerializeWithLengthPrefix(stream, header, PrefixStyle.Base128);
                Serializer.SerializeWithLengthPrefix(stream, putFileCommand, PrefixStyle.Base128);

                message = stream.ToArray();
            }

            List<Task<bool>> tasks = new List<Task<bool>>();

            for (int i = 0; i < nodes.Count; i++)
            {
                SkyNetNodeInfo node = nodes[i];
                tasks.Add(SendPutFilePacketToNode(message, node));
            }

            int countPassed = 0;

            while (tasks.Count > 0)
            {
                Task<bool> result = await Task.WhenAny(tasks);
                tasks.Remove(result);

                if (result.IsCompletedSuccessfully && result.Result)
                {
                    countPassed++;
                }

                if (countPassed >= Quorum(nodes.Count))
                {
                    break;
                }
            }


            if (countPassed >= Quorum(nodes.Count))
            {
                return new OperationResult { success = true };
            }
            else
            {
                return new OperationResult { success = false, errorCode = ErrorCode.UnexpectedError };
            }
        }

        private async Task<bool> SendPutFilePacketToNode(byte[] message, SkyNetNodeInfo node)
        {
            bool sendFileSent = false;

            try
            {
                using (TcpClient tcpClient = new TcpClient())
                {
                    //tcpClient.Client.SendTimeout = 5000;
                    //tcpClient.Client.ReceiveTimeout = 5000;
                    tcpClient.Connect(node.StorageFileTransferEndPoint);
                    NetworkStream stream = tcpClient.GetStream();
                    await stream.WriteAsync(message, 0, message.Length);

                    byte[] putFileAck = BitConverter.GetBytes(true);
                    await stream.ReadAsync(putFileAck, 0, putFileAck.Length);
                    sendFileSent = true;
                }
            }
            catch (SocketException se)
            {
                this.LogError("Send file failure due to socket exception: " + se.SocketErrorCode);
                sendFileSent = false;
            }
            catch (Exception e)
            {
                this.LogError("Send file failure due to exception: " + e.ToString());
                sendFileSent = false;
            }

            return sendFileSent;
        }

        /// Get
        private async Task<OperationResult> ProcessGetFromClient(string filename)
        {
            return await SendGetFileCommandFromMasterToNodes(filename);
        }

        private async Task<OperationResult> SendGetFileCommandFromMasterToNodes(string filename)
        {
            // Get the machines that hold this file
            if (!this.indexFile.ContainsKey(filename))
            {
                this.LogVerbose($"Sending get file command aborted, missing filename" +
                    $", {filename}, in indexfile");
                return new OperationResult { success = false, errorCode = ErrorCode.FileNotFound };
            }

            Tuple<List<string>, DateTime?, DateTime> index = this.indexFile[filename];

            List<SkyNetNodeInfo> nodes = new List<SkyNetNodeInfo>();

            foreach (string machine in index.Item1)
            {
                if (!this.machineList.ContainsKey(machine))
                    continue;

                SkyNetNodeInfo skyNetNodeInfo = this.machineList[machine];
                nodes.Add(skyNetNodeInfo);
            }

            byte[] message;

            using (MemoryStream stream = new MemoryStream())
            {
                SkyNetPacketHeader header = new SkyNetPacketHeader
                {
                    MachineId = this.machineId,
                    PayloadType = PayloadType.GetFile,
                };

                GetFileCommand getFileCommand = new GetFileCommand
                {
                    filename = filename,
                    lastModifiedDateTime = index.Item2 ?? DateTime.UtcNow,
                };

                Serializer.SerializeWithLengthPrefix(stream, header, PrefixStyle.Base128);
                Serializer.SerializeWithLengthPrefix(stream, getFileCommand, PrefixStyle.Base128);

                message = stream.ToArray();
            }

            List<Task<GetFileResponseCommand>> tasks = new List<Task<GetFileResponseCommand>>();

            for (int i = 0; i < nodes.Count; i++)
            {
                SkyNetNodeInfo node = nodes[i];
                tasks.Add(SendGetFilePacketToNode(message, node));
            }

            bool received = false;
            int countPassed = 0;

            while (tasks.Count > 0)
            {
                Task<GetFileResponseCommand> result = await Task.WhenAny(tasks);
                tasks.Remove(result);

                if (result.IsCompletedSuccessfully && result.Result.response == GetFileResponse.OK)
                {
                    if (!received)
                    {
                        await File.WriteAllBytesAsync(filename, result.Result.content);
                        received = true;
                    }

                    countPassed++;
                }

                if (countPassed >= Quorum(nodes.Count))
                {
                    break;
                }
            }

            if (countPassed >= Quorum(nodes.Count))
            {
                return new OperationResult { success = true };
            }
            else
            {
                return new OperationResult { success = false, errorCode = ErrorCode.UnexpectedError };
            }
        }

        private async Task<GetFileResponseCommand> SendGetFilePacketToNode(byte[] message, SkyNetNodeInfo node)
        {
            GetFileResponseCommand response = null;

            try
            {
                using (TcpClient tcpClient = new TcpClient())
                {
                    //tcpClient.Client.SendTimeout = 5000;
                    //tcpClient.Client.ReceiveTimeout = 5000;
                    tcpClient.Connect(node.StorageFileTransferEndPoint);

                    NetworkStream stream = tcpClient.GetStream();
                    stream.Write(message, 0, message.Length);
                    stream.Flush();

                    response = await Task.Run(() => Serializer.DeserializeWithLengthPrefix<GetFileResponseCommand>(stream, PrefixStyle.Base128));
                }
            }
            catch (SocketException se)
            {
                this.LogError("Get file failure due to socket exception: " + se.SocketErrorCode);
            }
            catch (Exception e)
            {
                this.LogError("Get file failure due to exception: " + e.ToString());
            }

            return response;
        }

        //// Delete
        // Process Delete command from client (Might not need this method)
        private async Task<OperationResult> ProcessDeleteFromClient(string filename)
        {
            OperationResult result = await SendDeleteFileCommandFromMasterToNodes(filename);

            if (result.success)
            {
                this.indexFile.Remove(filename, out Tuple<List<string>, DateTime?, DateTime> value);
            }

            return result;
        }

        // Process Delete File command to nodes
        private async Task<OperationResult> SendDeleteFileCommandFromMasterToNodes(string filename)
        {
            // Get the machines that hold this file
            if (!this.indexFile.ContainsKey(filename))
            {
                this.LogVerbose($"Sending delete file command aborted, missing filename" +
                    $", {filename}, in indexfile");
                return new OperationResult { success = false, errorCode = ErrorCode.FileNotFound };
            }

            Tuple<List<string>, DateTime?, DateTime> index = this.indexFile[filename];

            List<SkyNetNodeInfo> nodes = new List<SkyNetNodeInfo>();

            foreach (string machine in index.Item1)
            {
                if (!this.machineList.ContainsKey(machine))
                    continue;

                SkyNetNodeInfo skyNetNodeInfo = this.machineList[machine];
                nodes.Add(skyNetNodeInfo);
            }

            byte[] message;

            using (MemoryStream stream = new MemoryStream())
            {
                SkyNetPacketHeader header = new SkyNetPacketHeader
                {
                    MachineId = this.machineId,
                    PayloadType = PayloadType.DeleteFile,
                };

                DeleteFileCommand deleteFileCommand = new DeleteFileCommand
                {
                    filename = filename,
                };

                Serializer.SerializeWithLengthPrefix(stream, header, PrefixStyle.Base128);
                Serializer.SerializeWithLengthPrefix(stream, deleteFileCommand, PrefixStyle.Base128);

                message = stream.ToArray();
            }

            List<Task<bool>> tasks = new List<Task<bool>>();

            for (int i = 0; i < nodes.Count; i++)
            {
                SkyNetNodeInfo node = nodes[i];
                tasks.Add(SendDeleteFilePacketToNode(message, node));
            }

            int countPassed = 0;

            while (tasks.Count > 0)
            {
                Task<bool> result = await Task.WhenAny(tasks);
                tasks.Remove(result);

                if (result.IsCompletedSuccessfully && result.Result)
                {
                    countPassed++;
                }

                if (countPassed >= Quorum(nodes.Count))
                {
                    break;
                }
            }

            if (countPassed >= Quorum(nodes.Count))
            {
                return new OperationResult { success = true };
            }
            else
            {
                return new OperationResult { success = false, errorCode = ErrorCode.UnexpectedError };
            }
        }

        private int Quorum(int size)
        {
            return (int)Math.Ceiling((decimal)(size) / 2);
        }

        // Send Delete file packet command
        private async Task<bool> SendDeleteFilePacketToNode(byte[] message, SkyNetNodeInfo node)
        {
            bool deleteFileSent = false;

            try
            {
                using (TcpClient tcpClient = new TcpClient())
                {
                    //tcpClient.Client.SendTimeout = 5000;
                    //tcpClient.Client.ReceiveTimeout = 5000;
                    tcpClient.Connect(node.StorageFileTransferEndPoint);

                    NetworkStream stream = tcpClient.GetStream();
                    stream.Write(message, 0, message.Length);

                    byte[] deleteFileAck = BitConverter.GetBytes(true);
                    await stream.ReadAsync(deleteFileAck, 0, deleteFileAck.Length);
                    deleteFileSent = true;
                }
            }
            catch (SocketException se)
            {
                this.LogError("Delete file failure due to socket exception: " + se.SocketErrorCode);
                deleteFileSent = false;
            }
            catch (Exception e)
            {
                this.LogError("Delete file failure due to exception: " + e.ToString());
                deleteFileSent = false;
            }

            return deleteFileSent;
        }

        //// Node Failure
        private async Task<bool> ProcessNodeFailureFileRecovery(SkyNetNodeInfo failedNode)
        {
            await Task.Delay(1);

            //Console.WriteLine($"Node Fail: {failedNode.HostName}");

            if (failedNode.Status == Status.Alive)
            {
                failedNode.Status = Status.Failed;
            }

            // Is current node a master, else dont process failure
            if (!this.machineList.ContainsKey(this.machineId))
                return true;

            SkyNetNodeInfo currentNode = this.machineList[this.machineId];
            if (!currentNode.IsMaster)
                return true;

            // Is this the active master
            SkyNetNodeInfo activeMaster = GetActiveMaster();

            if (failedNode.HostName == activeMaster.HostName)
            {
                if (GetMasterNodes()[1].MachineId != currentNode.MachineId)
                    return true;
            }
            else if (currentNode.MachineId != activeMaster.MachineId)
                return false;

            if (failedNode.LeaveFailSdfsProcessed)
                return true;
            else
                failedNode.LeaveFailSdfsProcessed = true;

            // Process Recovery
            if (!ProcessNodeFailFileRecovery(failedNode))
                return false;

            //Console.WriteLine("Is deleted node a master?");
            // elect a new master if the failed node is a master
            if (failedNode.IsMaster)
            {
                this.LogImportant($"{failedNode.HostName} was a master node");

                // elect a new master
                List<string> masterNodes = new List<string>();
                foreach (SkyNetNodeInfo node in GetMasterNodes().Values)
                {
                    masterNodes.Add(node.MachineId);
                }

                SkyNetNodeInfo selectedMasterNode = ChooseRandomNode(masterNodes);

                if (selectedMasterNode != null)
                {
                    SendFileIndexFileMessageToNode(selectedMasterNode);
                    selectedMasterNode.IsMaster = true;
                    this.LogImportant($"{selectedMasterNode.HostName} is the new selected master node");
                }
                else
                    this.LogError("Master node was not available");
            }

            return true;
        }

        private bool ProcessNodeFailFileRecovery(SkyNetNodeInfo failedNode)
        {
            //Console.WriteLine($"index file count: {this.indexFile.Count}");

            if (this.indexFile == null)
            {
                Console.WriteLine("Unexpected null index file");
                return false;
            }

            foreach (KeyValuePair<string, Tuple<List<string>, DateTime?, DateTime>> kvp
                in this.indexFile)
            {
                if (kvp.Value.Item1.Contains(failedNode.MachineId))
                {
                    string filename = kvp.Key;

                    // remove failed node from list
                    kvp.Value.Item1.Remove(failedNode.MachineId);

                    // Send a Time Stamp command to all the machines with that file
                    Tuple<SkyNetNodeInfo, DateTime?> recoveryFileFromNode = ProcessLocationOfRecoveryFile(kvp.Value.Item1, kvp.Key);

                    if (recoveryFileFromNode == null || recoveryFileFromNode.Item1 == null)
                    {
                        this.LogImportant($"Node not available for recovery for file {filename}");
                        continue;
                    }
                    else
                        this.LogImportant($"Recovery Node: {recoveryFileFromNode.Item1.HostName}");

                    // update list with a new node
                    SkyNetNodeInfo recoveryFileToNode = ChooseRandomNode(kvp.Value.Item1);

                    // The latest time stamp, send a file transfer command 
                    if (recoveryFileFromNode.Item1.HostName == this.GetCurrentNodeInfo().HostName)
                    {
                        byte[] content = LoadFileToMemory(filename);
                        OperationResult result =
                            SendPutCommandToNodes(
                                filename,
                                content,
                                new List<SkyNetNodeInfo>() { recoveryFileToNode },
                                recoveryFileFromNode.Item2 == null ? DateTime.UtcNow : (DateTime)recoveryFileFromNode.Item2).Result;
                    }
                    //else if (SendFileTransferMessageToNode(recoveryFileFromNode.Item1, recoveryFileToNode, kvp.Key))
                    //    Console.WriteLine("File sent to node to transfer");
                    else
                        Console.WriteLine("File not sent");

                    kvp.Value.Item1.Add(recoveryFileToNode.MachineId);
                }
            }

            return true;
        }

        private Tuple<SkyNetNodeInfo, DateTime?> ProcessLocationOfRecoveryFile(List<string> machines, string filename)
        {
            byte[] message = null;

            using (MemoryStream stream = new MemoryStream())
            {
                SkyNetPacketHeader header = new SkyNetPacketHeader
                {
                    MachineId = this.machineId,
                    PayloadType = PayloadType.FileTimeStampRequest,
                };

                FileTimeStampRequestCommand fileCommand = new FileTimeStampRequestCommand()
                {
                    filename = filename,
                };

                Serializer.SerializeWithLengthPrefix(stream, header, PrefixStyle.Base128);
                Serializer.SerializeWithLengthPrefix(stream, fileCommand, PrefixStyle.Base128);

                message = stream.ToArray();
            }

            DateTime retTime = DateTime.MinValue;
            SkyNetNodeInfo retNode = null;
            DateTime? dt = null;

            if (message != null)
            {
                foreach (string machineId in machines)
                {
                    if (this.machineList.TryGetValue(machineId, out SkyNetNodeInfo value))
                    {
                        Console.WriteLine($"Asking for timestame of ${filename} at {value.HostName}");
                        dt = null;

                        if (value.HostName == this.GetCurrentNodeInfo().HostName)
                        {
                            Console.WriteLine("File Stored Locally");
                            if (this.indexFile.ContainsKey(filename))
                                dt = this.indexFile[filename].Item2;
                        }
                        else
                            dt = SendTimeStampPacketToNode(message, value);

                        if (dt != null)
                        {
                            Console.WriteLine($"{dt.Value.ToString()}");

                            if (DateTime.Compare((DateTime)dt, retTime) > 0)
                            {
                                retTime = (DateTime)dt;
                                retNode = value;
                            }
                        }
                    }
                }
            }

            return Tuple.Create<SkyNetNodeInfo, DateTime?>(retNode, dt);
        }

        private DateTime? SendTimeStampPacketToNode(byte[] message, SkyNetNodeInfo node)
        {
            try
            {
                using (TcpClient tcpClient = new TcpClient(node.HostName, SkyNetConfiguration.TimeStampPort))
                {
                    tcpClient.Client.SendTimeout = 5000;
                    tcpClient.Client.ReceiveTimeout = 5000;
                    NetworkStream stream = tcpClient.GetStream();
                    stream.Write(message, 0, message.Length);

                    byte[] payload = new byte[512];

                    Int32 bytes = stream.Read(payload, 0, payload.Length);

                    using (MemoryStream responseStream = new MemoryStream(payload))
                    {
                        SkyNetPacketHeader packetHeader =
                            Serializer.DeserializeWithLengthPrefix<SkyNetPacketHeader>(responseStream, PrefixStyle.Base128);

                        if (packetHeader.PayloadType != PayloadType.FileTimeStampResponse)
                            return null;

                        FileTimeStampResponseCommand fileTimeStampResponseCommand =
                            Serializer.DeserializeWithLengthPrefix<FileTimeStampResponseCommand>(responseStream, PrefixStyle.Base128);

                        if (fileTimeStampResponseCommand.timeStamp == null)
                            Console.WriteLine("null time stamp from " + node.HostName);
                        else
                            Console.WriteLine(fileTimeStampResponseCommand.timeStamp.Value.ToString() + " time from " + node.HostName);

                        return fileTimeStampResponseCommand.timeStamp;
                    }

                }
            }
            catch (SocketException se)
            {
                this.LogError("File time stamp request failed due to socket exception: " + se.SocketErrorCode);
            }
            catch (Exception e)
            {
                this.LogError("File time stamp request failed due to exception: " + e.ToString());
            }

            this.LogError("File time stamp request did not complete");

            return null;
        }

        private bool SendFileTransferMessageToNode(SkyNetNodeInfo nodeFrom, SkyNetNodeInfo nodeTo, string transFilename)
        {
            byte[] message = null;

            using (MemoryStream stream = new MemoryStream())
            {
                SkyNetPacketHeader header = new SkyNetPacketHeader
                {
                    MachineId = this.machineId,
                    PayloadType = PayloadType.FileTransferRequest,
                };

                FileTransferRequestCommand fileCommand = new FileTransferRequestCommand()
                {
                    fromMachineId = nodeFrom.MachineId,
                    toMachineId = nodeTo.MachineId,
                    filename = transFilename,
                };

                Serializer.SerializeWithLengthPrefix(stream, header, PrefixStyle.Base128);
                Serializer.SerializeWithLengthPrefix(stream, fileCommand, PrefixStyle.Base128);

                message = stream.ToArray();
            }

            if (message == null)
                return false;

            bool retValue = false;

            try
            {
                using (TcpClient tcpClient = new TcpClient())
                {
                    tcpClient.Connect(nodeFrom.IPAddress, SkyNetConfiguration.FileTransferPort);
                    tcpClient.Client.SendTimeout = 5000;
                    tcpClient.Client.ReceiveTimeout = 5000;
                    NetworkStream stream = tcpClient.GetStream();
                    stream.Write(message, 0, message.Length);

                    byte[] responseMessage = new byte[256];

                    Int32 bytes = stream.Read(responseMessage, 0, responseMessage.Length);

                    Console.WriteLine("File Transfer request successful");

                    if (bytes > 0)
                        return true;
                }
            }
            catch (SocketException se)
            {
                this.LogError("File transfer request failed due to socket exception: " + se.SocketErrorCode);
            }
            catch (Exception e)
            {
                this.LogError("File transfer request failed due to exception: " + e.ToString());
            }

            if (!retValue)
            {
                this.LogError("File transfer request failed");
            }

            return retValue;
        }

        private bool SendFileIndexFileMessageToNode(SkyNetNodeInfo node)
        {
            byte[] message = null;

            using (MemoryStream stream = new MemoryStream())
            {
                SkyNetPacketHeader header = new SkyNetPacketHeader
                {
                    MachineId = this.machineId,
                    PayloadType = PayloadType.FileIndexTransferRequest,
                };

                IndexFileCommand fileCommand = new IndexFileCommand()
                {
                    indexFile = this.indexFile,
                };

                Serializer.SerializeWithLengthPrefix(stream, header, PrefixStyle.Base128);
                Serializer.SerializeWithLengthPrefix(stream, fileCommand, PrefixStyle.Base128);

                message = stream.ToArray();
            }

            if (message == null)
                return false;

            bool retValue = false;

            try
            {
                using (TcpClient tcpClient = new TcpClient())
                {
                    tcpClient.Connect(node.IPAddress, SkyNetConfiguration.FileIndexTransferPort);
                    tcpClient.Client.SendTimeout = 5000;
                    tcpClient.Client.ReceiveTimeout = 5000;
                    NetworkStream stream = tcpClient.GetStream();
                    stream.Write(message, 0, message.Length);

                    byte[] responseMessage = new byte[256];

                    Int32 bytes = stream.Read(responseMessage, 0, responseMessage.Length);
                    retValue = BitConverter.ToBoolean(responseMessage, 0);
                }
            }
            catch (SocketException se)
            {
                this.LogError("File transfer request failed due to socket exception: " + se.SocketErrorCode);
            }
            catch (Exception e)
            {
                this.LogError("File transfer request failed due to exception: " + e.ToString());
            }

            return retValue;
        }

        private SkyNetNodeInfo ChooseRandomNode(List<string> exclusionNodes)
        {
            List<SkyNetNodeInfo> nodes = new List<SkyNetNodeInfo>();

            string machineId = string.Empty;

            foreach (string machine in this.machineList.Keys)
            {
                if (!exclusionNodes.Contains(machine))
                {
                    if (this.machineList[machine].Status == Status.Alive)
                        nodes.Add(this.machineList[machine]);
                }

            }

            int count = nodes.Count;

            if (count < 1)
            {
                Console.WriteLine("Random node not available!");
                LogError("Random node not available!");
                return null;
            }

            Random r = new Random();

            int index = r.Next(0, count);

            return nodes[index];
        }

        /// Node Recovery Servers
        private async Task NodeRecoveryTimeStampServer()
        {
            while (!this.isConnected)
            {
                await Task.Delay(100);
            }

            TcpListener server = new TcpListener(IPAddress.Any, SkyNetConfiguration.TimeStampPort);
            server.Start();

            // Buffer for reading data
            Byte[] bytes = new Byte[512];

            // Enter the listening loop.
            while (true)
            {
                try
                {
                    this.Log("Time stamp server started... ");

                    // Perform a blocking call to accept requests.
                    // You could also user server.AcceptSocket() here.
                    TcpClient client = await server.AcceptTcpClientAsync();

                    // Get a stream object for reading and writing
                    NetworkStream stream = client.GetStream();

                    int i;

                    // Loop to receive all the data sent by the client.
                    while ((i = stream.Read(bytes, 0, bytes.Length)) != 0)
                    {
                        PayloadType payloadType;
                        string filename = string.Empty;

                        using (MemoryStream retStream = new MemoryStream(bytes))
                        {
                            SkyNetPacketHeader packetHeader = Serializer.DeserializeWithLengthPrefix<SkyNetPacketHeader>(retStream, PrefixStyle.Base128);
                            string machineId = packetHeader.MachineId;
                            this.LogVerbose($"Received {packetHeader.PayloadType.ToString()} packet from {machineId}.");

                            payloadType = packetHeader.PayloadType;

                            if (payloadType != PayloadType.FileTimeStampRequest)
                            {
                                this.LogError($"Unknown Packet was received at from {machineId}");
                            }
                            else
                            {
                                FileTimeStampRequestCommand fileTimeStampRequestCommand =
                                    Serializer.DeserializeWithLengthPrefix<FileTimeStampRequestCommand>(retStream, PrefixStyle.Base128);

                                filename = fileTimeStampRequestCommand.filename;
                            }
                        }

                        byte[] retmessage;
                        this.fileLastUpdatedIndex.TryGetValue(filename, out DateTime dt);
                        Console.WriteLine($"{filename} was stored with timestamp of {dt.ToString()}");

                        using (MemoryStream resStream = new MemoryStream())
                        {
                            SkyNetPacketHeader header = new SkyNetPacketHeader
                            {
                                MachineId = this.machineId,
                                PayloadType = PayloadType.FileTimeStampResponse,
                            };

                            FileTimeStampResponseCommand fileTimeStampResponseCommand = new FileTimeStampResponseCommand()
                            {
                                filename = filename,
                                timeStamp = dt
                            };

                            Serializer.SerializeWithLengthPrefix(resStream, header, PrefixStyle.Base128);
                            Serializer.SerializeWithLengthPrefix(resStream, fileTimeStampResponseCommand, PrefixStyle.Base128);

                            retmessage = resStream.ToArray();
                        }

                        // Send back a response.
                        stream.Write(retmessage, 0, retmessage.Length);
                    }

                    // Shutdown and end connection
                    client.Close();
                }
                catch (Exception e)
                {
                    Console.WriteLine("Timestampserver: " + e);
                }
            }
        }

        private async Task NodeRecoveryTransferRequestServer()
        {
            while (!this.isConnected)
            {
                await Task.Delay(100);
            }

            TcpListener server = new TcpListener(IPAddress.Any, SkyNetConfiguration.FileTransferPort);
            server.Start();

            // Buffer for reading data
            Byte[] bytes = new Byte[512];

            // Enter the listening loop.
            while (true)
            {
                try
                {
                    this.Log("Time stamp server started... ");

                    TcpClient client = await server.AcceptTcpClientAsync();
                    NetworkStream stream = client.GetStream();

                    int i;

                    // Loop to receive all the data sent by the client.
                    while ((i = stream.Read(bytes, 0, bytes.Length)) != 0)
                    {
                        PayloadType payloadType;
                        string filename = string.Empty;

                        using (MemoryStream retStream = new MemoryStream(bytes))
                        {
                            SkyNetPacketHeader packetHeader = Serializer.DeserializeWithLengthPrefix<SkyNetPacketHeader>(retStream, PrefixStyle.Base128);
                            string machineId = packetHeader.MachineId;
                            this.LogVerbose($"Received {packetHeader.PayloadType.ToString()} packet from {machineId}.");

                            payloadType = packetHeader.PayloadType;

                            if (payloadType != PayloadType.FileTransferRequest)
                            {
                                this.LogError($"Unknown Packet was received at from {machineId}");
                            }
                            else
                            {
                                FileTransferRequestCommand fileTransferRequestCommand =
                                    Serializer.DeserializeWithLengthPrefix<FileTransferRequestCommand>(retStream, PrefixStyle.Base128);

                                if (fileTransferRequestCommand.fromMachineId == this.machineId)
                                {
                                    filename = fileTransferRequestCommand.filename;
                                    DateTime timestamp = this.fileLastUpdatedIndex[filename];

                                    SkyNetNodeInfo nodeTo = this.machineList[fileTransferRequestCommand.toMachineId];

                                    byte[] content = LoadFileToMemory(filename);
                                    OperationResult result =
                                        SendPutCommandToNodes(
                                            filename,
                                            content,
                                            new List<SkyNetNodeInfo>() { nodeTo },
                                            timestamp).Result;
                                }
                                else
                                    Console.WriteLine("incorrect machine id");
                            }
                        }

                        byte[] retmessage;

                        using (MemoryStream resStream = new MemoryStream())
                        {
                            SkyNetPacketHeader header = new SkyNetPacketHeader
                            {
                                MachineId = this.machineId,
                                PayloadType = PayloadType.FileTimeStampResponse,
                            };

                            FileTransferResponseCommand fileCommand = new FileTransferResponseCommand()
                            {
                                IsSuccessful = true
                            };

                            Serializer.SerializeWithLengthPrefix(resStream, header, PrefixStyle.Base128);
                            Serializer.SerializeWithLengthPrefix(resStream, fileCommand, PrefixStyle.Base128);

                            retmessage = resStream.ToArray();
                        }

                        // Send back a response.
                        stream.Write(retmessage, 0, retmessage.Length);
                    }

                    // Shutdown and end connection
                    client.Close();
                }
                catch (Exception e)
                {
                    Console.WriteLine("FileRecoveryTransfer: " + e);
                }
            }
        }

        private async Task NodeRecoveryIndexFileTransferServer()
        {
            while (!this.isConnected)
            {
                await Task.Delay(100);
            }

            TcpListener server = new TcpListener(IPAddress.Any, SkyNetConfiguration.FileIndexTransferPort);
            server.Start();

            // Buffer for reading data
            Byte[] bufferBytes = new Byte[1024];

            // total bytes
            List<Byte> totalBytes = new List<Byte>();

            // Enter the listening loop.
            while (true)
            {
                try
                {
                    TcpClient client = await server.AcceptTcpClientAsync();
                    NetworkStream stream = client.GetStream();

                    //Console.WriteLine($"Stream received from {client.Client.RemoteEndPoint}");

                    SkyNetPacketHeader packetHeader = Serializer.DeserializeWithLengthPrefix<SkyNetPacketHeader>(stream, PrefixStyle.Base128);
                    string machineId = packetHeader.MachineId;
                    this.LogVerbose($"Received {packetHeader.PayloadType.ToString()} packet from {machineId}.");
                    //Console.WriteLine($"Received {packetHeader.PayloadType.ToString()} packet from {machineId}.");

                    IndexFileCommand indexFileCommand = Serializer.DeserializeWithLengthPrefix<IndexFileCommand>(stream, PrefixStyle.Base128);
                    this.indexFile = indexFileCommand.indexFile;

                    // Send back a response.
                    byte[] retmessage = BitConverter.GetBytes(true);
                    stream.Write(retmessage, 0, retmessage.Length);

                    // Shutdown and end connection
                    client.Close();
                }
                catch (Exception e)
                {
                    this.LogError("IndexFileSrever: " + e);
                }
            }
        }

        private async Task NodeStorageFileTransferServer()
        {
            while (!this.isConnected)
            {
                await Task.Delay(100);
            }

            TcpListener server = new TcpListener(IPAddress.Any, SkyNetConfiguration.StorageFileTransferPort); ;
            server.Start();
            this.Log("File transfer server started... ");

            // Enter the listening loop.
            while (true)
            {
                try
                {
                    TcpClient client = await server.AcceptTcpClientAsync();
                    NetworkStream stream = client.GetStream();

                    SkyNetPacketHeader packetHeader = Serializer.DeserializeWithLengthPrefix<SkyNetPacketHeader>(stream, PrefixStyle.Base128);
                    string machineId = packetHeader.MachineId;
                    this.LogVerbose($"Received {packetHeader.PayloadType.ToString()} packet from {machineId}.");

                    switch (packetHeader.PayloadType)
                    {
                        case PayloadType.GetFile:
                            GetFileCommand getFileCommand = Serializer.DeserializeWithLengthPrefix<GetFileCommand>(stream, PrefixStyle.Base128);
                            GetFileResponseCommand responseCommand = new GetFileResponseCommand();
                            string getFileName = getFileCommand.filename;
                            byte[] fileResponse;
                            using (MemoryStream ms = new MemoryStream())
                            {
                                bool fileExists = Storage.Exists(getFileName);
                                bool freshCopy = fileExists && (Storage.LastModified(getFileName) - getFileCommand.lastModifiedDateTime).Duration() < TimeSpan.FromSeconds(15);

                                if (fileExists && freshCopy)
                                {
                                    responseCommand.response = GetFileResponse.OK;
                                    responseCommand.content = await Storage.ReadContentAsync(getFileName);

                                    Serializer.SerializeWithLengthPrefix<GetFileResponseCommand>(ms, responseCommand, PrefixStyle.Base128);
                                }
                                else if (!freshCopy)
                                {
                                    responseCommand.response = GetFileResponse.NotUpToDate;

                                    Serializer.SerializeWithLengthPrefix<GetFileResponseCommand>(ms, responseCommand, PrefixStyle.Base128);
                                }
                                else
                                {
                                    responseCommand.response = GetFileResponse.DoesNotExist;

                                    Serializer.SerializeWithLengthPrefix<GetFileResponseCommand>(ms, responseCommand, PrefixStyle.Base128);
                                }

                                fileResponse = ms.ToArray();
                            }
                            stream.Write(fileResponse, 0, fileResponse.Length);
                            break;
                        case PayloadType.PutFile:
                            PutFileCommand putFileCommand = Serializer.DeserializeWithLengthPrefix<PutFileCommand>(stream, PrefixStyle.Base128);

                            await Storage.StageAsync(putFileCommand.content, putFileCommand.filename);

                            // TODO: what do we do with this?
                            DateTime instructionTime = putFileCommand.instructionTime;

                            Console.WriteLine($"{putFileCommand.filename} at {putFileCommand.instructionTime.ToString()}");

                            if (this.fileLastUpdatedIndex.ContainsKey(putFileCommand.filename))
                            {
                                fileLastUpdatedIndex[putFileCommand.filename] = instructionTime;
                            }
                            else
                            {
                                this.fileLastUpdatedIndex.Add(putFileCommand.filename, putFileCommand.instructionTime);
                            }

                            // Send back a response.
                            byte[] putFileAck = BitConverter.GetBytes(true);
                            await stream.WriteAsync(putFileAck, 0, putFileAck.Length);
                            Storage.MoveStagingToStorage(putFileCommand.filename);

                            LogImportant($"Stored file {putFileCommand.filename}");
                            break;

                        case PayloadType.DeleteFile:
                            DeleteFileCommand deleteFileCommand = Serializer.DeserializeWithLengthPrefix<DeleteFileCommand>(stream, PrefixStyle.Base128);
                            Storage.Delete(deleteFileCommand.filename);

                            // Send back a response.
                            byte[] deleteFileAck = BitConverter.GetBytes(true);
                            stream.Write(deleteFileAck, 0, deleteFileAck.Length);

                            LogImportant($"Deleted file {deleteFileCommand.filename}");
                            break;
                    }

                    // Shutdown and end connection
                    client.Close();
                }
                catch (Exception e)
                {
                    this.LogError("FileTransferError: " + e.ToString());
                }
            }
        }

        private string GetMachineNumber(string hostname)
        {
            string prefix = "fa17-cs425-g50-";
            string suffix = ".cs.illinois.edu";
            string machineNumber = "11";

            if (hostname.StartsWith(prefix) && hostname.EndsWith(suffix))
            {
                machineNumber = hostname.Substring(prefix.Length, 2).TrimStart('0');
            }

            return machineNumber;
        }

        private bool SendJoinCommand()
        {
            bool joinSuccessful = false;
            byte[] joinPacket;

            this.machineId = SkyNetNodeInfo.GetMachineId(SkyNetNodeInfo.ParseMachineId(this.machineId).Item1);
            this.Log($"Updated machine id to {this.machineId}");

            // Create join packet
            using (MemoryStream stream = new MemoryStream())
            {
                SkyNetPacketHeader header = new SkyNetPacketHeader
                {
                    MachineId = this.machineId,
                    PayloadType = PayloadType.MembershipJoin,
                };

                Serializer.SerializeWithLengthPrefix(stream, header, PrefixStyle.Base128);
                joinPacket = stream.ToArray();
            }

            SkyNetNodeInfo[] introducers = this.GetIntroducers();
            using (UdpClient udpClient = new UdpClient())
            {
                for (int i = 0; i < introducers.Length && !joinSuccessful; i++)
                {
                    SkyNetNodeInfo introducer = introducers[i];

                    try
                    {
                        this.Log($"Sending join command to {introducer.HostName}.");

                        udpClient.Client.SendTimeout = 1000;
                        udpClient.Client.ReceiveTimeout = 1000;
                        this.isConnected = true;
                        udpClient.Send(joinPacket, joinPacket.Length, introducer.DefaultEndPoint);


                        // Wait for membership list to be received
                        for (int waitCount = 0; waitCount < 2 && machineList.Count < 1; waitCount++)
                        {
                            Thread.Sleep(350);
                        }

                        if (machineList.Count > 1 || (this.isIntroducer && this.machineList.ContainsKey(this.machineId)))
                        {
                            joinSuccessful = true;
                            this.isConnected = true;
                        }
                        else
                        {
                            joinSuccessful = false;
                            this.isConnected = false;
                        }
                    }
                    catch (Exception e)
                    {
                        this.isConnected = false;
                        this.LogError($"Unable to send command to {introducer.HostName}, error : " + e.ToString());
                    }
                }
            }

            if (!joinSuccessful)
            {
                this.machineList.Clear();

                this.Log($"Unexpected join failure after {introducers.Length} retries.");
            }

            return joinSuccessful;
        }

        private bool SendLeaveCommand()
        {
            bool leaveSucessful = false;
            byte[] leavePacket;

            using (MemoryStream stream = new MemoryStream())
            {
                SkyNetPacketHeader header = new SkyNetPacketHeader
                {
                    MachineId = this.machineId,
                    PayloadType = PayloadType.MembershipLeave,
                };

                Serializer.SerializeWithLengthPrefix(stream, header, PrefixStyle.Base128);
                leavePacket = stream.ToArray();
            }

            //!TODO: Maybe use predecessors/ successors & introducers?
            SkyNetNodeInfo[] introducers = this.GetIntroducers();
            using (UdpClient udpClient = new UdpClient(SkyNetConfiguration.SecondaryPort))
            {
                for (int i = 0; i < introducers.Length && !leaveSucessful; i++)
                {
                    SkyNetNodeInfo introducer = introducers[i];

                    try
                    {
                        this.Log($"Sending leave command to {introducer.HostName}.");

                        udpClient.Client.SendTimeout = 200;
                        udpClient.Client.ReceiveTimeout = 200;
                        udpClient.Send(leavePacket, leavePacket.Length, introducer.DefaultEndPoint);

                        IPEndPoint receiveEndPoint = new IPEndPoint(introducer.IPAddress, SkyNetConfiguration.SecondaryPort);
                        udpClient.Receive(ref receiveEndPoint);

                        this.machineList.Clear();
                        leaveSucessful = true;
                        this.isConnected = false;
                    }
                    catch (Exception e)
                    {
                        this.LogError($"Unable to send command to {introducer.HostName}, error : " + e.ToString());
                    }
                }
            }

            if (!leaveSucessful)
            {
                this.Log($"Unexpected leave failure after {introducers.Length} retries.");
            }

            return leaveSucessful;
        }

        private bool SendMembershipUpdateCommand(SkyNetNodeInfo node)
        {
            this.LogVerbose($"Sending membership list update command to {node.HostName}.");

            bool membershipSent;
            byte[] membershipListPacket;

            using (MemoryStream stream = new MemoryStream())
            {
                SkyNetPacketHeader header = new SkyNetPacketHeader
                {
                    MachineId = this.machineId,
                    PayloadType = PayloadType.MembershipUpdate,
                };

                MembershipUpdateCommand membershipUpdate = new MembershipUpdateCommand
                {
                    machineList = new Dictionary<string, SkyNetNodeInfo>(this.machineList),
                };

                Serializer.SerializeWithLengthPrefix(stream, header, PrefixStyle.Base128);
                Serializer.SerializeWithLengthPrefix(stream, membershipUpdate, PrefixStyle.Base128);

                membershipListPacket = stream.ToArray();
            }

            try
            {
                using (UdpClient udpClient = new UdpClient())
                {
                    udpClient.Client.SendTimeout = 1000;
                    udpClient.Client.ReceiveTimeout = 1000;
                    udpClient.Send(membershipListPacket, membershipListPacket.Length, node.DefaultEndPoint);

                    membershipSent = true;
                }
            }
            catch (SocketException se)
            {
                this.LogError("Send membership failure due to socket exception: " + se.SocketErrorCode);
                membershipSent = false;
            }
            catch (Exception e)
            {
                this.LogError("Send membership failure due to exception: " + e.ToString());
                membershipSent = false;
            }

            return membershipSent;
        }

        private bool SendHeartBeatCommand(SkyNetNodeInfo node)
        {
            this.LogVerbose($"Sending heartbeat command to {node.HostName}.");

            bool heartbeatSent;
            byte[] heartbeat;

            using (MemoryStream stream = new MemoryStream())
            {
                SkyNetPacketHeader header = new SkyNetPacketHeader
                {
                    MachineId = this.machineId,
                    PayloadType = PayloadType.Heartbeat,
                };

                MembershipUpdateCommand membershipUpdate = new MembershipUpdateCommand
                {
                    machineList = new Dictionary<string, SkyNetNodeInfo>(this.machineList),
                };

                Serializer.SerializeWithLengthPrefix(stream, header, PrefixStyle.Base128);
                Serializer.SerializeWithLengthPrefix(stream, membershipUpdate, PrefixStyle.Base128);

                heartbeat = stream.ToArray();
            }

            try
            {
                using (UdpClient udpClient = new UdpClient())
                {
                    udpClient.Client.SendTimeout = 300;
                    udpClient.Client.ReceiveTimeout = 300;
                    udpClient.Send(heartbeat, heartbeat.Length, node.DefaultEndPoint);

                    heartbeatSent = true;
                }
            }
            catch (SocketException se)
            {
                this.LogError("Heartbeat failure due to socket exception: " + se.SocketErrorCode);
                heartbeatSent = false;
            }
            catch (Exception e)
            {
                this.LogError("Heartbeat failure due to exception: " + e.ToString());
                heartbeatSent = false;
            }

            return heartbeatSent;
        }

        private void SendHeartBeats(List<SkyNetNodeInfo> successors, List<SkyNetNodeInfo> predecessors)
        {
            foreach (SkyNetNodeInfo predecessor in predecessors)
            {
                SendHeartBeatCommand(predecessor);
            }

            foreach (SkyNetNodeInfo sucessor in successors)
            {
                SendHeartBeatCommand(sucessor);
            }
        }

        private async void DetectFailures(List<SkyNetNodeInfo> successors, List<SkyNetNodeInfo> predecessors)
        {
            HashSet<string> failures = new HashSet<string>();
            // Update self's heartbeat
            if (machineList.TryGetValue(machineId, out SkyNetNodeInfo self))
            {
                self.LastHeartbeat = DateTime.UtcNow.Ticks;
            }

            foreach (var element in successors)
            {
                // Check for heartbeats exceeding timeout
                DateTime lastHeartbeat = new DateTime(element.LastHeartbeat);

                if (element.Status == Status.Alive && DateTime.UtcNow - lastHeartbeat > TimeSpan.FromMilliseconds(SkyNetConfiguration.HeartbeatTimeout))
                {
                    failures.Add(element.MachineId);
                }
            }

            foreach (var element in predecessors)
            {
                // Check for heartbeats exceeding timeout
                DateTime lastHeartbeat = new DateTime(element.LastHeartbeat);

                if (element.Status == Status.Alive && DateTime.UtcNow - lastHeartbeat > TimeSpan.FromMilliseconds(SkyNetConfiguration.HeartbeatTimeout))
                {
                    failures.Add(element.MachineId);
                }
            }

            foreach (var failure in failures)
            {
                if (machineList.TryGetValue(failure, out SkyNetNodeInfo failedTarget))
                {
                    this.LogImportant($"{failedTarget.MachineId} ({failedTarget.HostName}) has failed.");
                    failedTarget.Status = Status.Failed;
                }
            }

            // Membership pruning
            HashSet<string> prunes = new HashSet<string>();
            foreach (var key in machineList.Keys)
            {
                var element = machineList[key];

                DateTime lastHeartbeat = new DateTime(element.LastHeartbeat);
                if (element.Status == Status.Failed && DateTime.UtcNow - lastHeartbeat > TimeSpan.FromSeconds(7))
                {
                    prunes.Add(element.MachineId);
                }
            }

            foreach (var prune in prunes)
            {
                machineList.TryRemove(prune, out SkyNetNodeInfo value);
            }

            // Process Failure if it is an active master node
            //if (this.IsActiveMaster())
            //{
            //    foreach (string machineId in failures)
            //    {

            //        machineList.TryGetValue(machineId, out SkyNetNodeInfo failedTarget);

            //        Console.WriteLine("Called ProcessNodeFailureFileRecovery 1");
            //        await this.ProcessNodeFailureFileRecovery(failedTarget);
            //    }
            //}

        }

        private async Task PeriodicFileIndexTransfer()
        {
            while (true)
            {
                await Task.Delay(1000);

                try
                {
                    if (this.isConnected && this.IsActiveMaster())
                    {
                        if (this.GetMasterNodes() != null)
                        {
                            SortedList<int, SkyNetNodeInfo> masternoodes = this.GetMasterNodes();

                            foreach (SkyNetNodeInfo node in masternoodes.Values.Where(item => item.HostName != this.GetCurrentNodeInfo().HostName))
                            {
                                this.SendFileIndexFileMessageToNode(node);
                            }
                        }
                    }
                }
                catch
                {

                }
            }
        }

        private async void PeriodicHeartBeat()
        {
            while (true)
            {
                try
                {
                    if (this.isConnected)
                    {
                        SortedList<string, SkyNetNodeInfo> ringList = new SortedList<string, SkyNetNodeInfo>();
                        foreach (var kvp in this.machineList.Where(kv => kv.Value.Status == Status.Alive))
                        {
                            ringList.Add(kvp.Key, kvp.Value);
                        }

                        var successors = this.GetHeartbeatSuccessors(ringList);
                        var predecessors = this.GetHeartbeatPredecessors(ringList);

                        StringBuilder sb = new StringBuilder("Successors: ");
                        foreach (var element in successors)
                        {
                            sb.Append(element.MachineId + $"({element.HostName})" + ", ");
                        }

                        sb.Append("Predecessors: ");
                        foreach (var element in predecessors)
                        {
                            sb.Append(element.MachineId + $"({element.HostName})" + ", ");
                        }
                        this.LogVerbose(sb.ToString());

                        this.DetectFailures(successors, predecessors);
                        this.SendHeartBeats(successors, predecessors);
                    }
                }
                catch (Exception ex)
                {
                    this.LogError("Unexpected error: " + ex.ToString());
                }

                await Task.Delay(SkyNetConfiguration.HeartbeatInterval);
            }
        }

        private async Task DisseminateMembershipList()
        {
            while (true)
            {
                try
                {
                    if (this.isConnected)
                    {
                        Random rand = new Random();
                        List<string> targets = this.machineList.Keys
                            .Where(machineId => machineId != this.machineId)
                            .ToList<string>();

                        for (int i = 0; i < SkyNetConfiguration.GossipRoundTargets && targets.Count > 0; i++)
                        {
                            int element = rand.Next(targets.Count);
                            string target = targets[element];
                            targets.RemoveAt(element);

                            this.SendMembershipUpdateCommand(this.machineList[target]);
                        }
                    }
                }
                catch (Exception ex)
                {
                    this.LogError("Unexpected error: " + ex.ToString());
                }

                await Task.Delay(SkyNetConfiguration.GossipRoundInterval);
            }
        }

        private async void ProcessLeaveCommand(string machineId)
        {
            if (this.isIntroducer)
            {
                if (machineList.TryGetValue(machineId, out SkyNetNodeInfo leftNode))
                {
                    leftNode.Status = Status.Failed;

                    this.LogImportant($"{machineId} ({leftNode.HostName}) has left.");

                    try
                    {
                        UdpClient client = new UdpClient();
                        byte[] ackPacket;

                        SkyNetPacketHeader header = new SkyNetPacketHeader
                        {
                            MachineId = this.machineId,
                            PayloadType = PayloadType.MembershipLeaveAck
                        };

                        using (MemoryStream stream = new MemoryStream())
                        {
                            Serializer.SerializeWithLengthPrefix(stream, header, PrefixStyle.Base128);
                            ackPacket = stream.ToArray();
                        }
                        client.Send(ackPacket, ackPacket.Length, new IPEndPoint(leftNode.IPAddress, SkyNetConfiguration.SecondaryPort));
                    }
                    catch (Exception)
                    {
                    }

                    // TODO: Node - Failure detection not needed here, because of update method?
                    if (this.IsActiveMaster())
                    {
                        Console.WriteLine("Starting node recovery process");

                        Console.WriteLine("Called ProcessNodeFailureFileRecovery 2");
                        bool processedSucceeded = await ProcessNodeFailureFileRecovery(leftNode);

                        if (!processedSucceeded)
                        {
                            this.LogImportant($"{leftNode.MachineId} files have failed to recovered.");
                        }

                        Console.WriteLine("Completed node recovery process");
                    }

                }
            }
            else
            {
                this.LogWarning($"Not a coordinator, but received request to leave.");
            }
        }

        private void ProcessJoinCommand(string machineId)
        {
            if (this.isIntroducer)
            {
                IPAddress introducerAddress = SkyNetNodeInfo.ParseMachineId(machineId).Item1;
                SkyNetNodeInfo joinedNode = new SkyNetNodeInfo(this.GetHostName(introducerAddress), machineId)
                {
                    LastHeartbeat = DateTime.UtcNow.Ticks,
                };

                // Master - This machine should be a master node if there are less than 3 masters
                int masterCount = this.GetMasterNodes().Count;
                if (masterCount < 3)
                    joinedNode.IsMaster = true;
                else
                    joinedNode.IsMaster = false;

                if (!this.machineList.ContainsKey(machineId))
                {
                    this.machineList.TryAdd(joinedNode.MachineId, joinedNode);
                    this.LogImportant($"{machineId} ({joinedNode.HostName}) has joined.");

                    this.SendMembershipUpdateCommand(joinedNode);
                }
                else if (machineId != this.machineId) // Check to see if the machine was not the introducer itself
                {
                    this.LogWarning($"Unexpected {machineId} attempted to rejoin the network.");
                }
            }
            else
            {
                this.LogWarning($"Not a coordinator, but received request to join.");
            }
        }

        private void ProcessMembershipUpdateCommand(string machineId, MembershipUpdateCommand updateCommand)
        {
            this.MergeMembershipList(updateCommand.machineList);
        }

        private void ProcessHeartbeatCommand(string machineId, MembershipUpdateCommand updateCommand)
        {
            this.LogVerbose($"Received heartbeat from {machineId}");

            if (this.machineId == machineId)
            {
                this.LogError("Received heartbeat from self.");
                return;
            }

            if (this.machineList.TryGetValue(machineId, out SkyNetNodeInfo update))
            {
                update.LastHeartbeat = DateTime.UtcNow.Ticks;
                update.HeartbeatCounter = update.HeartbeatCounter + 1;
            }

            this.ProcessMembershipUpdateCommand(machineId, updateCommand);
        }

        public void HandleCommand(byte[] payload)
        {
            using (MemoryStream stream = new MemoryStream(payload))
            {
                SkyNetPacketHeader packetHeader = Serializer.DeserializeWithLengthPrefix<SkyNetPacketHeader>(stream, PrefixStyle.Base128);
                string machineId = packetHeader.MachineId;
                this.LogVerbose($"Received {packetHeader.PayloadType.ToString()} packet from {machineId}.");

                switch (packetHeader.PayloadType)
                {
                    case PayloadType.Heartbeat:
                        MembershipUpdateCommand heartbeatMembershipUpdate = Serializer.DeserializeWithLengthPrefix<MembershipUpdateCommand>(stream, PrefixStyle.Base128);
                        this.ProcessHeartbeatCommand(machineId, heartbeatMembershipUpdate);
                        break;

                    case PayloadType.MembershipJoin:
                        this.ProcessJoinCommand(machineId);
                        break;

                    case PayloadType.MembershipLeave:
                        this.ProcessLeaveCommand(machineId);
                        Console.WriteLine("Leave processed!");
                        break;

                    case PayloadType.MembershipUpdate:
                        MembershipUpdateCommand updateCommand = Serializer.DeserializeWithLengthPrefix<MembershipUpdateCommand>(stream, PrefixStyle.Base128);
                        this.ProcessMembershipUpdateCommand(machineId, updateCommand);
                        break;

                        //case PayloadType.IndexFileHeartbeat:
                        //    IndexFileHeartbeatCommand indexFileHeartbeatCommand = Serializer.DeserializeWithLengthPrefix<IndexFileHeartbeatCommand>(stream, PrefixStyle.Base128);
                        //    this.ProcessIndexFileHeartbeat();
                        //    break;
                }
            }
        }

        public async void ReceiveCommand()
        {
            UdpClient server = new UdpClient(SkyNetConfiguration.DefaultPort);

            while (true)
            {
                try
                {
                    UdpReceiveResult result = await server.ReceiveAsync();
                    if (this.isConnected)
                    {
                        byte[] received = result.Buffer;

                        this.HandleCommand(received);
                    }
                }
                catch (Exception e)
                {
                    this.Log("Exception: " + e);
                }
            }
        }

        public async Task StorageActiveMasterServer()
        {
            while (!this.isConnected)
            {
                await Task.Delay(10);
            }

            TcpListener server = new TcpListener(IPAddress.Any, SkyNetConfiguration.SecondaryPort);
            server.Start();
            this.Log("Active master server started... ");

            // Enter the listening loop.
            while (true)
            {

                TcpClient client = await server.AcceptTcpClientAsync();

                try
                {
                    if (IsActiveMaster())
                    {
                        NetworkStream stream = client.GetStream();

                        SdfsPacketHeader packetHeader = Serializer.DeserializeWithLengthPrefix<SdfsPacketHeader>(stream, PrefixStyle.Base128);
                        string machineId = packetHeader.MachineId;
                        this.LogVerbose($"Received {packetHeader.PayloadType.ToString()} packet from {machineId}.");

                        switch (packetHeader.PayloadType)
                        {
                            case SdfsPayloadType.GetRequest:
                                GetRequest getRequest = Serializer.DeserializeWithLengthPrefix<GetRequest>(stream, PrefixStyle.Base128);
                                OperationResult result = await ProcessGetFromClient(getRequest.FileName);

                                GetResponse getResponse = new GetResponse()
                                {
                                    getSuccessful = result.success,
                                    errorCode = result.errorCode,
                                    content = await File.ReadAllBytesAsync(getRequest.FileName)
                                };

                                Serializer.SerializeWithLengthPrefix<GetResponse>(stream, getResponse, PrefixStyle.Base128);

                                break;

                            case SdfsPayloadType.PutRequest:
                                PutRequest putRequest = Serializer.DeserializeWithLengthPrefix<PutRequest>(stream, PrefixStyle.Base128);
                                DateTime putRequestTime = DateTime.UtcNow;
                                PutResponse confirmResponse;
                                if (fileLastUpdatedIndex.ContainsKey(putRequest.FileName) && DateTime.UtcNow - fileLastUpdatedIndex[putRequest.FileName] < TimeSpan.FromMinutes(1))
                                {
                                    confirmResponse = new PutResponse()
                                    {
                                        putSuccessful = false,
                                        putConfirmationRequired = true,
                                    };

                                    Serializer.SerializeWithLengthPrefix<PutResponse>(stream, confirmResponse, PrefixStyle.Base128);

                                    byte[] response = BitConverter.GetBytes(true);
                                    await stream.ReadAsync(response, 0, response.Length).WithTimeout(TimeSpan.FromSeconds(30));
                                }
                                else
                                {
                                    confirmResponse = new PutResponse()
                                    {
                                        putSuccessful = false,
                                        putConfirmationRequired = false,
                                    };

                                    Serializer.SerializeWithLengthPrefix<PutResponse>(stream, confirmResponse, PrefixStyle.Base128);
                                }

                                PutRequestFile file = await Task.Run(() => Serializer.DeserializeWithLengthPrefix<PutRequestFile>(stream, PrefixStyle.Base128));

                                OperationResult putResult = await ProcessPutFromClient(putRequest.FileName, file.payload);
                                fileLastUpdatedIndex[putRequest.FileName] = putRequestTime;

                                PutResponse success = new PutResponse()
                                {
                                    putSuccessful = putResult.success,
                                    errorCode = putResult.errorCode
                                };

                                Serializer.SerializeWithLengthPrefix<PutResponse>(stream, success, PrefixStyle.Base128);

                                break;

                            case SdfsPayloadType.DeleteRequest:
                                DeleteRequest deleteRequest = Serializer.DeserializeWithLengthPrefix<DeleteRequest>(stream, PrefixStyle.Base128);
                                OperationResult deleteResult = await ProcessDeleteFromClient(deleteRequest.FileName);

                                DeleteResponse deleteResponse = new DeleteResponse()
                                {
                                    deleteSuccessful = deleteResult.success,
                                    errorCode = deleteResult.errorCode
                                };

                                Serializer.SerializeWithLengthPrefix<DeleteResponse>(stream, deleteResponse, PrefixStyle.Base128);
                                break;

                            case SdfsPayloadType.ListRequest:
                                ListRequest listRequest = Serializer.DeserializeWithLengthPrefix<ListRequest>(stream, PrefixStyle.Base128);
                                ListResponse listResponse;
                                if (indexFile.ContainsKey(listRequest.FileName))
                                {
                                    List<string> machineIds = indexFile[listRequest.FileName].Item1;
                                    listResponse = new ListResponse()
                                    {
                                        machines = machineIds,
                                        errorCode = ErrorCode.None
                                    };
                                }
                                else
                                {
                                    listResponse = new ListResponse()
                                    {
                                        machines = new List<string>(),
                                        errorCode = ErrorCode.FileNotFound
                                    };
                                }
                                Serializer.SerializeWithLengthPrefix<ListResponse>(stream, listResponse, PrefixStyle.Base128);
                                break;
                        }
                    }
                    else
                    {
                        client.Close();
                    }
                }
                catch (Exception e)
                {
                    this.LogError("FileTransferError: " + e.ToString());
                }

                // Shutdown and end connection
                client.Close();
            }
        }

        private void MakeGetRequest(string sdfsFileName, string localFileName)
        {
            SkyNetNodeInfo master = GetActiveMaster();

            using (TcpClient client = new TcpClient())
            {
                client.Connect(master.IPAddress, SkyNetConfiguration.SecondaryPort);
                NetworkStream stream = client.GetStream();

                SdfsPacket<GetRequest> getRequest = new SdfsPacket<GetRequest>
                {
                    Header = new SdfsPacketHeader
                    {
                        MachineId = machineId,
                    },

                    Payload = new GetRequest
                    {
                        FileName = sdfsFileName
                    },
                };

                byte[] packet = getRequest.ToBytes();
                stream.Write(packet, 0, packet.Length);

                GetResponse response = Serializer.DeserializeWithLengthPrefix<GetResponse>(stream, PrefixStyle.Base128);

                if (response.getSuccessful)
                {
                    File.WriteAllBytes(localFileName, response.content);
                    LogImportant($"{sdfsFileName} retrieved as {localFileName}.");
                }
                else
                {
                    switch (response.errorCode)
                    {
                        case ErrorCode.UnexpectedError:
                            LogImportant($"{sdfsFileName} get failed unexpectedly.");
                            break;

                        case ErrorCode.FileNotFound:
                            LogImportant($"{sdfsFileName} was not available.");
                            break;
                    }

                }
            }
        }

        private async Task MakePutRequest(string sdfsFileName, string localFileName)
        {
            if (!File.Exists(localFileName))
            {
                Console.WriteLine($"Local file {localFileName} in directory {Directory.GetCurrentDirectory()} does not exist.");
                return;
            }

            SkyNetNodeInfo master = GetActiveMaster();

            using (TcpClient client = new TcpClient())
            {
                client.Connect(master.IPAddress, SkyNetConfiguration.SecondaryPort);
                NetworkStream stream = client.GetStream();

                SdfsPacket<PutRequest> putRequest = new SdfsPacket<PutRequest>
                {
                    Header = new SdfsPacketHeader
                    {
                        MachineId = machineId,
                    },

                    Payload = new PutRequest
                    {
                        FileName = sdfsFileName
                    },
                };

                byte[] packet = putRequest.ToBytes();
                stream.Write(packet, 0, packet.Length);

                PutResponse confirmResponse = Serializer.DeserializeWithLengthPrefix<PutResponse>(stream, PrefixStyle.Base128);

                if (confirmResponse.putConfirmationRequired)
                {
                    string confirm = string.Empty;
                    try
                    {
                        Console.WriteLine($"File {sdfsFileName} was recently updated. Do you want to continue? (Y to continue)");
                        confirm = await ReadConsoleAsync().WithTimeout(TimeSpan.FromSeconds(30));
                    }
                    catch (TimeoutException)
                    {
                        LogImportant($"Confirmation timed out. Update for file {sdfsFileName} rejected.");
                        return;
                    }

                    if (!confirm.Equals("Y", StringComparison.OrdinalIgnoreCase))
                    {
                        return;
                    }
                    else
                    {
                        byte[] response = BitConverter.GetBytes(true);
                        stream.Write(response, 0, response.Length);
                    }
                }

                PutRequestFile putRequestFile = new PutRequestFile
                {
                    payload = await File.ReadAllBytesAsync(localFileName)
                };
                Serializer.SerializeWithLengthPrefix<PutRequestFile>(stream, putRequestFile, PrefixStyle.Base128);
                PutResponse putResponse = Serializer.DeserializeWithLengthPrefix<PutResponse>(stream, PrefixStyle.Base128);

                if (putResponse.putSuccessful)
                {
                    LogImportant($"{sdfsFileName} has been updated.");
                }
                else
                {
                    switch (putResponse.errorCode)
                    {
                        case ErrorCode.UnexpectedError:
                            LogImportant($"{sdfsFileName} update failed unexpectedly.");
                            break;

                        case ErrorCode.RequestTimedOut:
                            LogImportant($"{sdfsFileName} request timed out.");
                            break;
                    }

                }
            }
        }

        private void MakeDeleteRequest(string sdfsFileName)
        {
            SkyNetNodeInfo master = GetActiveMaster();

            using (TcpClient client = new TcpClient())
            {
                client.Connect(master.IPAddress, SkyNetConfiguration.SecondaryPort);
                NetworkStream stream = client.GetStream();

                SdfsPacket<DeleteRequest> deleteRequest = new SdfsPacket<DeleteRequest>
                {
                    Header = new SdfsPacketHeader
                    {
                        MachineId = machineId,
                    },

                    Payload = new DeleteRequest
                    {
                        FileName = sdfsFileName
                    },
                };

                byte[] packet = deleteRequest.ToBytes();
                stream.Write(packet, 0, packet.Length);

                DeleteResponse response = Serializer.DeserializeWithLengthPrefix<DeleteResponse>(stream, PrefixStyle.Base128);

                if (response.deleteSuccessful)
                {
                    LogImportant($"{sdfsFileName} has been deleted.");
                }
                else
                {
                    switch (response.errorCode)
                    {
                        case ErrorCode.UnexpectedError:
                            LogImportant($"{sdfsFileName} deletion failed unexpectedly.");
                            break;

                        case ErrorCode.FileNotFound:
                            LogImportant($"{sdfsFileName} was not available for delete.");
                            break;
                    }

                }
            }
        }

        private void MakeListRequest(string sdfsFileName)
        {
            SkyNetNodeInfo master = GetActiveMaster();

            using (TcpClient client = new TcpClient())
            {
                client.Connect(master.IPAddress, SkyNetConfiguration.SecondaryPort);
                NetworkStream stream = client.GetStream();

                SdfsPacket<ListRequest> listRequest = new SdfsPacket<ListRequest>
                {
                    Header = new SdfsPacketHeader
                    {
                        MachineId = machineId,
                    },

                    Payload = new ListRequest
                    {
                        FileName = sdfsFileName
                    },
                };

                byte[] packet = listRequest.ToBytes();
                stream.Write(packet, 0, packet.Length);

                ListResponse response = Serializer.DeserializeWithLengthPrefix<ListResponse>(stream, PrefixStyle.Base128);

                if (response.errorCode == ErrorCode.FileNotFound)
                {
                    LogImportant($"No file {sdfsFileName} stored.");
                }
                else
                {
                    LogImportant($"Machines with file {sdfsFileName}");
                    foreach (var machine in response.machines)
                    {
                        LogImportant(machine);
                    }
                }
            }
        }

        public async Task PromptUser()
        {
            while (true)
            {
                try
                {
                    Console.WriteLine();
                    Console.WriteLine("List of commands: ");
                    Console.WriteLine("[1] Show membership list");
                    Console.WriteLine("[2] Show machine id");
                    Console.WriteLine("[3] Join the group");
                    Console.WriteLine("[4] Leave the group");
                    Console.WriteLine("[5] put <localfilename> <sdfsfilename>");
                    Console.WriteLine("[6] get <sdfsfilename> <localfilename>");
                    Console.WriteLine("[7] delete <sdfsfilename>");
                    Console.WriteLine("[8] ls <sdfsfilename>");
                    Console.WriteLine("[9] store");
                    Console.WriteLine("[10] Submit sava job");


                    string cmd = await ReadConsoleAsync();

                    if (Byte.TryParse(cmd, out byte option))
                    {
                        switch (option)
                        {
                            case 1:
                                foreach (var keyValuePair in this.machineList.Where(kv => kv.Value.Status != Status.Failed))
                                {
                                    Console.WriteLine($"{keyValuePair.Key} ({keyValuePair.Value.HostName})");
                                }
                                break;

                            case 2:
                                Console.WriteLine($"{this.machineId} (${this.hostEntry.HostName})");
                                break;

                            case 3:
                                if (!this.isConnected)
                                {
                                    bool joined = this.SendJoinCommand();

                                    // UI waits
                                    await Task.Delay(TimeSpan.FromMilliseconds(200));
                                }
                                else
                                {
                                    Console.WriteLine("Unable to join, already connected to the group.");
                                }
                                break;

                            case 4:
                                if (this.isConnected)
                                {
                                    this.SendLeaveCommand();

                                    Environment.Exit(0);
                                }
                                else
                                {
                                    Console.WriteLine("Unable to leave group, machine is not currently joined to group.");
                                }
                                break;

                            case 9:
                                foreach (var file in Storage.ListStoredFiles())
                                {
                                    Console.WriteLine(file);
                                }
                                break;
                            case 10:
                                Console.WriteLine("Jobs available, type the job name to confirm submission: ");
                                foreach (var job in Sava.Configuration.JobConfiguration)
                                {
                                    
                                    Console.WriteLine(job.JobName);
                                }
                                string jobName = await ReadConsoleAsync();

                                Job jobToSubmit = null;
                                foreach (var job in Sava.Configuration.JobConfiguration)
                                {
                                    if (jobName.Equals(job.JobName, StringComparison.OrdinalIgnoreCase))
                                    {
                                        jobToSubmit = job;
                                        break;
                                    }
                                }

                                if (jobToSubmit == null)
                                {
                                    Console.WriteLine("Unrecognized job.");
                                    continue;
                                }
                                else
                                {
                                    await Task.Run(() => SubmitSavaJob(jobToSubmit));
                                }
                                break;

                            default:
                                Console.WriteLine("Invalid command.");
                                break;
                        }
                    }
                    else
                    {
                        cmd = cmd.Trim();

                        if (cmd.StartsWith("put", StringComparison.OrdinalIgnoreCase))
                        {
                            string[] putTokens = cmd.Split(" ", StringSplitOptions.RemoveEmptyEntries);

                            if (putTokens.Length != 3)
                            {
                                Console.WriteLine("Invalid put command. Please specify local and dfs filename.");
                            }
                            else
                            {
                                string local = putTokens[1];
                                string dest = putTokens[2];

                                await MakePutRequest(dest, local);
                            }
                        }
                        else if (cmd.StartsWith("get", StringComparison.OrdinalIgnoreCase))
                        {
                            string[] getTokens = cmd.Split(" ", StringSplitOptions.RemoveEmptyEntries);

                            if (getTokens.Length != 3)
                            {
                                Console.WriteLine("Invalid get command. Please specify local and dfs filename.");
                            }
                            else
                            {
                                string local = getTokens[2];
                                string dest = getTokens[1];

                                MakeGetRequest(dest, local);
                            }
                        }
                        else if (cmd.StartsWith("delete", StringComparison.OrdinalIgnoreCase))
                        {
                            string[] deleteTokens = cmd.Split(" ", StringSplitOptions.RemoveEmptyEntries);

                            if (deleteTokens.Length != 2)
                            {
                                Console.WriteLine("Invalid delete command. Please specify dfs filename.");
                            }
                            else
                            {
                                string dest = deleteTokens[1];

                                MakeDeleteRequest(dest);
                            }
                        }
                        else if (cmd.StartsWith("ls", StringComparison.OrdinalIgnoreCase))
                        {
                            string[] lsToken = cmd.Split(" ", StringSplitOptions.RemoveEmptyEntries);

                            if (lsToken.Length != 2)
                            {
                                Console.WriteLine("Invalid lls command. Please specify dfs filename.");
                            }
                            else
                            {
                                string file = lsToken[1];

                                MakeListRequest(file);
                            }

                        }
                        else if (cmd.StartsWith("store", StringComparison.OrdinalIgnoreCase))
                        {
                            foreach (var file in Storage.ListStoredFiles())
                            {
                                Console.WriteLine(file);
                            }
                        }
                        else
                        {
                            Console.WriteLine("Invalid command. Please enter an integer between 1-4 as listed above.");
                        }
                    }
                }
                catch (Exception e)
                {
                    this.LogError(e.ToString());
                }
            }
        }

        public void TestPrintOnConsole()
        {
            // TODO: Test - Remove later
            Console.WriteLine();
            IEnumerable<SkyNetNodeInfo> masters = this.GetMasterNodes().Values;
            Console.WriteLine("number of masters: " + masters.Count());
            foreach (SkyNetNodeInfo node in masters)
            {
                Console.WriteLine("Master: " + node.HostName);
            }

            SkyNetNodeInfo active = this.GetActiveMaster();
            if (active != null)
                Console.WriteLine("Active: " + active.HostName);

            Console.WriteLine("--indexFile--");
            if (this.indexFile == null)
                Console.WriteLine("null");
            else
            {
                Console.WriteLine("index file count" + this.indexFile.Count);
                foreach (string filename in this.indexFile.Keys)
                {
                    Console.WriteLine(filename);
                }
            }

            Console.WriteLine("--last time stamp--");
            if (this.fileLastUpdatedIndex != null)
            {
                foreach (KeyValuePair<string, DateTime> kvp in this.fileLastUpdatedIndex)
                {
                    Console.WriteLine(kvp.Key + " : " + kvp.Value);
                }
            }
        }

        /// <summary>
        /// Runs the <see cref="SkyNetNode"/> as a server node.
        /// </summary>
        public void Run()
        {
            Thread ph = new Thread(new ThreadStart(PeriodicHeartBeat));
            ph.Start();

            Task[] serverTasks = {
                Task.Factory.StartNew(() => ReceiveCommand()),
                //Task.Factory.StartNew(() => PeriodicHeartBeat(), TaskCreationOptions.LongRunning),

                PromptUser(),

                DisseminateMembershipList(),

                //NodeRecoveryIndexFileTransferServer(),
                //NodeRecoveryTimeStampServer(),
                //NodeRecoveryTransferRequestServer(),

                NodeStorageFileTransferServer(),
                StorageActiveMasterServer(),

                //PeriodicFileIndexTransfer(),

                SavaServer(),
            };

            Task.WaitAll(serverTasks.ToArray());
        }

        public void MergeMembershipList(Dictionary<string, SkyNetNodeInfo> listToMerge)
        {
            // First, detect if self has failed.
            bool selfHasFailed = listToMerge.TryGetValue(this.machineId, out SkyNetNodeInfo self) && self.Status == Status.Failed;

            if (selfHasFailed)
            {
                // Self was detected as false-positive removal, change state to be disconnected
                this.isConnected = false;
                this.machineList.Clear();
                this.LogImportant($"False-positive detection, voluntary left the group.");
                return;
            }

            var additions = listToMerge.Where(entry => !machineList.ContainsKey(entry.Key));
            var deletions = machineList.Where(entry => !listToMerge.ContainsKey(entry.Key));
            var updates = listToMerge.Where(entry => entry.Key != this.machineId && machineList.TryGetValue(entry.Key, out SkyNetNodeInfo existing) && entry.Value.HeartbeatCounter > existing.HeartbeatCounter);

            foreach (var addition in additions)
            {
                IPAddress addressToAdd = SkyNetNodeInfo.ParseMachineId(addition.Key).Item1;
                SkyNetNodeInfo nodeToAdd = new SkyNetNodeInfo(this.GetHostName(addressToAdd), addition.Key)
                {
                    LastHeartbeat = DateTime.UtcNow.Ticks,
                    Status = addition.Value.Status,
                    IsMaster = addition.Value.IsMaster
                };

                machineList.AddOrUpdate(nodeToAdd.MachineId, nodeToAdd, (key, oldValue) =>
                {
                    oldValue.LastHeartbeat = DateTime.UtcNow.Ticks;
                    oldValue.Status = addition.Value.Status;
                    oldValue.IsMaster = addition.Value.IsMaster;
                    return oldValue;
                });

                if (nodeToAdd.Status == Status.Alive && addition.Value.IsMaster)
                {
                    nodeToAdd.IsMaster = addition.Value.IsMaster;
                }

                this.LogVerbose($"Added {addition.Key} ({addition.Value.HostName}) to membership list.");
            }

            List<SkyNetNodeInfo> deletedNodes = new List<SkyNetNodeInfo>();

            foreach (var deletion in deletions)
            {
                machineList.TryRemove(deletion.Key, out SkyNetNodeInfo value);
                deletedNodes.Add(value);

                this.LogVerbose($"Removed {deletion.Key} ({deletion.Value.HostName}) from membership list.");
            }

            foreach (var update in updates)
            {
                SkyNetNodeInfo incomingUpdate = update.Value;

                if (machineList.TryGetValue(update.Key, out SkyNetNodeInfo itemToUpdate))
                {
                    if (itemToUpdate.Status == Status.Alive && incomingUpdate.Status == Status.Failed)
                    {
                        // First instance of failure
                        itemToUpdate.Status = Status.Failed;
                    }
                    else if (itemToUpdate.Status == Status.Failed)
                    {
                        // Items that are failed are considered immutable
                        continue;
                    }

                    if (itemToUpdate.Status == Status.Alive && incomingUpdate.IsMaster)
                    {
                        itemToUpdate.IsMaster = incomingUpdate.IsMaster;
                    }

                    itemToUpdate.HeartbeatCounter = incomingUpdate.HeartbeatCounter;
                    itemToUpdate.LastHeartbeat = DateTime.UtcNow.Ticks;

                    this.LogVerbose($"Updated {update.Key} ({update.Value.HostName}) last heartbeat to {itemToUpdate.LastHeartbeat}");
                }
            }

            foreach (KeyValuePair<string, SkyNetNodeInfo> kvp in listToMerge)
            {
                if (machineList.TryGetValue(kvp.Key, out SkyNetNodeInfo itemToUpdate))
                {
                    if (itemToUpdate.Status == Status.Alive && kvp.Value.IsMaster)
                    {
                        itemToUpdate.IsMaster = kvp.Value.IsMaster;
                    }
                }
            }
        }

        public async Task<List<Vertex>> RunSavaJob(Job job)
        {
            List<Vertex> results;
            runningJob = job;

            do
            {
                currentIteration = 0;
                jobHasFailed = false;

                results = await InitializeAndRunSavaJob(job);
            } while (jobHasFailed);

            return results;
        }

        public async Task<List<Vertex>> InitializeAndRunSavaJob(Job job)
        {
            try
            {
                Stopwatch sw = new Stopwatch();
                this.LogDebug($"Running job {job.JobName}");

                sw.Start();
                InitializeSavaJob();
                RunRounds();
                sw.Stop();

                if (jobHasFailed)
                {
                    LogDebug("Initialize failure");
                    return null;
                }
                else
                {
                    this.LogImportant($"Completed in {sw.Elapsed.TotalMinutes} minutes.");
                    return await GetResults();
                }
            }
            catch (Exception e)
            {
                if (e.InnerException != null)
                {
                    this.LogError("Error encounted, inner error: " + e.InnerException.ToString());
                }
                else
                {
                    this.LogError("Error encounted, error: " + e.ToString());
                }
            }
            return null;
        }

        public void InitializeSavaJob()
        {
            Job job = runningJob;
            FileStream fs = File.Open(SkyNetConfiguration.ProgramPath + Path.DirectorySeparatorChar + job.InputFile, FileMode.Open);
            var savaMachinesRingList = GetSavaMachines();
            savaMachines = savaMachinesRingList.Values.ToList<SkyNetNodeInfo>();
            this.LogDebug("Reading graph file");
            List<Vertex> vertices = job.GraphReader.ReadFile(fs);
            this.LogDebug("Partitioning graph file");
            List<List<Vertex>> partitions = job.GraphPartitioner.Partition(vertices, savaMachines.Count);
            List<string> partitionedFileNames = new List<string>();

            this.LogDebug("Sending all partition data.");

            Task[] tasks = new Task[partitions.Count];
            for (int i = 0; i < partitions.Count; i++)
            {
                string filename = $"{job.JobName}.{i}";
                partitionedFileNames.Add(filename);

                List<SkyNetNodeInfo> nodes = new List<SkyNetNodeInfo>();
                var primary = savaMachines[i];

                nodes.Add(primary);

                tasks[i] = SendPartitionData(partitions[i], filename, i, nodes);
            }

            Task.WaitAll(tasks);

            this.LogDebug("Sending job initialization requests.");

            GraphInfo graphInfo = new GraphInfo();
            graphInfo.NumberOfVertices = vertices.Count;

            tasks = new Task[partitions.Count];
            for (int i = 0; i < partitions.Count; i++)
            {
                tasks[i] = InitializeJob(savaMachines[i], i, job, graphInfo);
            }

            Task.WaitAll(tasks);

            this.LogDebug("Job initiailization complete.");
        }

        public void RunRounds()
        {
            do
            {
                this.LogImportant($"Run iteration: {currentIteration}");

                savaWorkerCompletion.Clear();

                Task[] tasks = new Task[savaMachines.Count];
                for (int i = 0; i < savaMachines.Count; i++)
                {
                    tasks[i] = SendIterationPacket(savaMachines[i], currentIteration);
                }

                Task.WaitAll(tasks);

                PollForWorkers();

                if (jobHasFailed)
                {
                    break;
                }

                currentIteration++;
            } while (!HasJobCompleted());

            LogDebug("RunRounds finish");
        }
        
        public void PollForWorkers()
        {
            while (savaWorkerCompletion.Count != savaMachines.Count)
            {

                foreach (SkyNetNodeInfo savaMachine in savaMachines)
                {
                    if (savaMachine.Status == Status.Failed)
                    {
                        jobHasFailed = true;
                        LogImportant($"Job {runningJob.JobName} restarting due to failure on {savaMachine.HostName}");
                        return;
                    }
                }

                var waitingOn = savaMachines.Where(x => !savaWorkerCompletion.ContainsKey(x.MachineId)).Select(x => x.HostName).ToList<string>();
                LogVerbose("Waiting on " + String.Join(',', waitingOn));

                Thread.Sleep(500);
            }
        }

        public async Task<List<Vertex>> GetResults()
        {
            List<Vertex> combined = new List<Vertex>();
            List<Task<List<Vertex>>> tasks = new List<Task<List<Vertex>>>();
            foreach (SkyNetNodeInfo machine in savaMachines)
            {
                tasks.Add(Task.Run(() => GetResultFrom(machine)));
            }

            while (tasks.Count > 0)
            {
                Task<List<Vertex>> result = await Task.WhenAny(tasks);
                tasks.Remove(result);

                if (result.IsCompletedSuccessfully)
                {
                    combined.AddRange(result.Result);
                }
            }

            await Task.Run(() =>
            {
                combined.Sort((v1, v2) => ((double)v2.Value.UntypedValue).CompareTo((double)v1.Value.UntypedValue));
                int i = 0;

                using (FileStream fs = File.Create("results.txt"))
                {
                    using (StreamWriter sw = new StreamWriter(fs))
                    {
                        foreach (Vertex v in combined)
                        {
                            sw.WriteLine($"{v.VertexId}, {v.Value.UntypedValue.ToString()}");

                            if (i < 25)
                            {
                                Console.WriteLine($"{v.VertexId}, {v.Value.UntypedValue.ToString()}");
                            }

                            i++;
                        }
                    }
                }
            });


            return combined;
        }

        public List<Vertex> GetResultFrom(SkyNetNodeInfo node)
        {
            using (TcpClient client = new TcpClient())
            {
                client.Connect(node.IPAddress, SkyNetConfiguration.SavaPort);
                NetworkStream stream = client.GetStream();

                SavaPacketHeader packetHeader = new SavaPacketHeader()
                {
                    MachineId = machineId,
                    PayloadType = SavaPayloadType.ResultsRequest,
                };

                Serializer.SerializeWithLengthPrefix(stream, packetHeader, PrefixStyle.Base128);

                List<Vertex> results = Serializer.DeserializeWithLengthPrefix<List<Vertex>>(stream, PrefixStyle.Base128);

                byte[] resultsAck = BitConverter.GetBytes(true);
                stream.Write(resultsAck, 0, resultsAck.Length);

                return results;
            }
        }

        public async Task SendIterationPacket(SkyNetNodeInfo node, int iteration)
        {
            using (TcpClient client = new TcpClient())
            {
                await client.ConnectAsync(node.IPAddress, SkyNetConfiguration.SavaPort).WithTimeout(TimeSpan.FromMilliseconds(1000));
                NetworkStream stream = client.GetStream();

                SavaPacket<IterationPacket> packet = new SavaPacket<IterationPacket>
                {
                    Header = new SavaPacketHeader()
                    {
                        MachineId = machineId,
                    },

                    Payload = new IterationPacket
                    {
                        Iteration = iteration
                    },
                };

                byte[] send = packet.ToBytes();
                stream.Write(send, 0, send.Length);
            }
        }

        public bool HasJobCompleted()
        {
            return savaWorkerCompletion.All(x => x.Value == 0);
        }

        public async Task SavaServer()
        {
            TcpListener server = new TcpListener(IPAddress.Any, SkyNetConfiguration.SavaPort); ;
            server.Start();
            this.Log("Sava server started... ");

            // Enter the listening loop.
            while (true)
            {
                try
                {
                    TcpClient client = await server.AcceptTcpClientAsync();
                    NetworkStream stream = client.GetStream();

                    SavaPacketHeader packetHeader = Serializer.DeserializeWithLengthPrefix<SavaPacketHeader>(stream, PrefixStyle.Base128);
                    if (packetHeader.PayloadType != SavaPayloadType.VertexMessage)
                    {
                        this.LogImportant($"Received {packetHeader.PayloadType.ToString()} packet from {packetHeader.MachineId}.");
                    }

                    if (isSavaMaster)
                    {
                        switch (packetHeader.PayloadType)
                        {
                            case SavaPayloadType.JobRequest:
                                JobProcessingRequest rq = Serializer.DeserializeWithLengthPrefix<JobProcessingRequest>(stream, PrefixStyle.Base128);
                                Task t = Task.Run(() => RunSavaJob(rq.requestJob));
                                break;

                            case SavaPayloadType.WorkerCompletion:
                                WorkerCompletionPacket wcp = Serializer.DeserializeWithLengthPrefix<WorkerCompletionPacket>(stream, PrefixStyle.Base128);
                                savaWorkerCompletion.Add(wcp.MachineId, wcp.ActiveVertices);
                                break;
                        }
                    }
                    switch (packetHeader.PayloadType)
                    {
                        case SavaPayloadType.WorkerStart:
                            WorkerStartPacket sp = Serializer.DeserializeWithLengthPrefix<WorkerStartPacket>(stream, PrefixStyle.Base128);
                            savaMachines = GetSavaMachines().Values.ToList<SkyNetNodeInfo>();
                            worker = new Worker(this, sp.partition, sp.job, sp.WorkerPartitions.Count, sp.GraphInfo);
                            break;

                        case SavaPayloadType.Iteration:
                            IterationPacket ip = Serializer.DeserializeWithLengthPrefix<IterationPacket>(stream, PrefixStyle.Base128);
                            Task t = Task.Run(() => worker.ProcessNewIteration(ip.Iteration));
                            break;

                        case SavaPayloadType.VertexMessage:
                            VertexMessagesPacket vmp = Serializer.DeserializeWithLengthPrefix<VertexMessagesPacket>(stream, PrefixStyle.Base128);
                            if (vmp.messages == null)
                            {
                                this.LogError($"Received null messages from {packetHeader.MachineId}");
                            }
                            else
                            {
                                worker.QueueIncomingMessages(vmp.messages);
                            }
                            
                            break;

                        case SavaPayloadType.ResultsRequest:
                            Serializer.SerializeWithLengthPrefix(stream, worker.Vertices, PrefixStyle.Base128);

                            byte[] putFileAck = BitConverter.GetBytes(true);
                            await stream.ReadAsync(putFileAck, 0, putFileAck.Length);

                            break;
                    }

                    // Shutdown and end connection
                    client.Close();
                }
                catch (Exception e)
                {
                    this.LogError("SavaError: " + e);
                }
            }
        }

        public async Task SendPartitionData(List<Vertex> partition, string fileName, int partitionNumber, List<SkyNetNodeInfo> nodes)
        {
            byte[] content;
            using (MemoryStream ms = new MemoryStream())
            {
                Serializer.SerializeWithLengthPrefix<List<Vertex>>(ms, partition, PrefixStyle.Base128);
                content = ms.ToArray();
            }

            await SendPutToNodes(fileName, nodes, content);
        }

        public async Task InitializeJob(SkyNetNodeInfo node, int partition, Job job, GraphInfo graphInfo)
        {
            using (TcpClient client = new TcpClient())
            {
                await client.ConnectAsync(node.IPAddress, SkyNetConfiguration.SavaPort).WithTimeout(TimeSpan.FromMilliseconds(1000));
                NetworkStream stream = client.GetStream();

                SavaPacket<WorkerStartPacket> packet = new SavaPacket<WorkerStartPacket>
                {
                    Header = new SavaPacketHeader()
                    {
                        MachineId = machineId,
                    },

                    Payload = new WorkerStartPacket
                    {
                        job = job,
                        partition = partition,
                        WorkerPartitions = savaMachines.Select(x => x.MachineId).ToList<string>(),
                        GraphInfo = graphInfo
                    },
                };

                byte[] send = packet.ToBytes();
                stream.Write(send, 0, send.Length);
            }
        }

        public List<Vertex> SubmitSavaJob(Job job)
        {
            Stopwatch sw = new Stopwatch();
            List<Vertex> results = new List<Vertex>();
            IPAddress master = GetIpAddress(SkyNetConfiguration.Machines.First(x => x.Value.IsSavaMaster).Key);
            using (TcpClient client = new TcpClient())
            {
                client.Connect(master, SkyNetConfiguration.SavaPort);
                NetworkStream stream = client.GetStream();

                SavaPacket<JobProcessingRequest> savaPacket = new SavaPacket<JobProcessingRequest>()
                {
                    Header = new SavaPacketHeader()
                    {
                        MachineId = machineId,
                    },

                    Payload = new JobProcessingRequest
                    {
                        requestJob = job
                    },
                };

                byte[] packet = savaPacket.ToBytes();
                stream.Write(packet, 0, packet.Length);
            }
            this.LogImportant($"Job {job.JobName} has been submitted.");
            sw.Start();

            // Wait for response
            if (!this.isConnected)
            {
                TcpListener server = new TcpListener(IPAddress.Any, SkyNetConfiguration.SavaPort);
                server.Start();

                try
                {
                    TcpClient client = server.AcceptTcpClient();
                    NetworkStream stream = client.GetStream();

                    results = Serializer.DeserializeWithLengthPrefix<List<Vertex>>(stream, PrefixStyle.Base128);

                    // Shutdown and end connection
                    client.Close();
                }
                catch (Exception e)
                {
                    this.LogError("SavaError: " + e);
                }

                sw.Stop();
                this.LogImportant($"Completed in {sw.Elapsed.TotalMinutes} minutes.");

                if (results.Count == 0)
                {
                    this.LogImportant("No results found.");
                }
            }

            return results;
        }

        public void SendWorkerCompletion(int activeVertices)
        {
            SkyNetNodeInfo master = GetSavaMaster();
            using (TcpClient client = new TcpClient())
            {
                client.Connect(master.IPAddress, SkyNetConfiguration.SavaPort);
                NetworkStream stream = client.GetStream();

                SavaPacket<WorkerCompletionPacket> savaPacket = new SavaPacket<WorkerCompletionPacket>()
                {
                    Header = new SavaPacketHeader()
                    {
                        MachineId = machineId,
                    },

                    Payload = new WorkerCompletionPacket
                    {
                        ActiveVertices = activeVertices,
                        MachineId = machineId,
                    },
                };

                byte[] packet = savaPacket.ToBytes();
                stream.Write(packet, 0, packet.Length);
            }
        }

        public void SendVertexMessages(Message[] sendMessages, int partitionNumber)
        {
            try
            {
                using (TcpClient client = new TcpClient())
                {
                    client.Connect(savaMachines[partitionNumber].IPAddress, SkyNetConfiguration.SavaPort);
                    NetworkStream stream = client.GetStream();

                    SavaPacket<VertexMessagesPacket> savaPacket = new SavaPacket<VertexMessagesPacket>()
                    {
                        Header = new SavaPacketHeader()
                        {
                            MachineId = machineId,
                        },

                        Payload = new VertexMessagesPacket
                        {
                            messages = sendMessages,
                        },
                    };

                    byte[] packet = savaPacket.ToBytes();
                    stream.Write(packet, 0, packet.Length);
                }
            } catch (Exception e)
            {
                // This could happen due to node failure
            }
        }

        private SortedList<string, SkyNetNodeInfo> GetSavaMachines()
        {
            SortedList<string, SkyNetNodeInfo> ringList = new SortedList<string, SkyNetNodeInfo>();
            foreach (var kvp in this.machineList.Where(kv => kv.Value.Status == Status.Alive && !SkyNetConfiguration.Machines[kv.Value.HostName].IsSavaMaster && !SkyNetConfiguration.Machines[kv.Value.HostName].IsSavaBackupMaster))
            {
                ringList.Add(kvp.Key, kvp.Value);
            }

            return ringList;
        }

        private SkyNetNodeInfo GetSavaMaster()
        {
            var activeMaster = machineList.Values.FirstOrDefault(x => x.Status == Status.Alive && SkyNetConfiguration.Machines[x.HostName].IsSavaMaster);

            if (activeMaster ==  null)
            {
                var backupMaster = machineList.Values.FirstOrDefault(x => x.Status == Status.Alive && SkyNetConfiguration.Machines[x.HostName].IsSavaBackupMaster);
                return backupMaster;
            }

            return activeMaster;
        }

        #region Utility functions
        private Task<string> ReadConsoleAsync()
        {
            return Task.Run(() => Console.ReadLine());
        }
		
		private SortedList<string, SkyNetNodeInfo> GetRingList()
        {
            SortedList<string, SkyNetNodeInfo> ringList = new SortedList<string, SkyNetNodeInfo>();
            foreach (var kvp in this.machineList.Where(kv => kv.Value.Status == Status.Alive))
            {
                ringList.Add(kvp.Key, kvp.Value);
            }

            return ringList;
        }

        private SkyNetNodeInfo GetCurrentNodeInfo()
        {
            if (this.machineList.TryGetValue(this.machineId, out SkyNetNodeInfo info))
                return info;

            return null;
        }

        private string GetHostName(IPAddress address)
        {
            if (this.localDnsCache.ContainsKey(address.ToString()))
            {
                return this.localDnsCache[address.ToString()];
            }

            return Dns.GetHostEntry(address.ToString()).HostName;
        }

        private IPEndPoint GetEndPoint(string hostname)
        {
            return new IPEndPoint(this.GetIpAddress(hostname), SkyNetConfiguration.DefaultPort);
        }

        private IPEndPoint GetEndPoint(IPAddress address)
        {
            return new IPEndPoint(address, SkyNetConfiguration.DefaultPort);
        }

        private IPAddress GetIpAddress(string hostname)
        {
            string ipCached = this.localDnsCache.Where(kv => kv.Value == hostname).Select(kv => kv.Key).FirstOrDefault();
            if (ipCached != null)
            {
                return IPAddress.Parse(ipCached);
            }

            IPAddress[] addresses = (Dns.GetHostEntry(hostname)).AddressList;
            return addresses.FirstOrDefault(ip => ip.AddressFamily == AddressFamily.InterNetwork);
        }

        private SkyNetNodeInfo[] GetIntroducers()
        {
            var introducers = this.knownIntroducers;

            for (int i = 0; i < introducers.Length; i++)
            {
                // If current machine is also an introducer, move to the last of the queue
                if (introducers[i].IPAddress.Equals(this.IPAddress))
                {
                    var swap = introducers[i];
                    introducers[i] = introducers[introducers.Length - 1];
                    introducers[introducers.Length - 1] = swap;
                    break;
                }
            }

            // !TODO : Filter out known failures

            return introducers;
        }

        private SkyNetNodeInfo GetSuccessor(SkyNetNodeInfo node, SortedList<string, SkyNetNodeInfo> sortedList)
        {
            int nodeIndex = sortedList.IndexOfKey(node.MachineId);
            int sucessorIndex = (nodeIndex + 1) % sortedList.Count;

            return sortedList.ElementAt(sucessorIndex).Value;
        }

        private SkyNetNodeInfo GetPredecessor(SkyNetNodeInfo node, SortedList<string, SkyNetNodeInfo> sortedList)
        {
            int nodeIndex = sortedList.IndexOfKey(node.MachineId);
            int sucessorIndex = nodeIndex - 1;

            if (sucessorIndex < 0)
            {
                sucessorIndex = sucessorIndex + sortedList.Count;
            }

            return sortedList.ElementAt(sucessorIndex).Value;
        }

        private List<SkyNetNodeInfo> GetSuccessors(SkyNetNodeInfo machine, SortedList<string, SkyNetNodeInfo> sortedList)
        {
            List<SkyNetNodeInfo> successors = new List<SkyNetNodeInfo>();
            if (sortedList.ContainsKey(machine.MachineId))
            {
                var currentSuccessor = sortedList[machine.MachineId];

                for (int i = 0; i < SkyNetConfiguration.HeartbeatSuccessors; i++)
                {
                    currentSuccessor = this.GetSuccessor(currentSuccessor, sortedList);

                    if (currentSuccessor.IPAddress.Equals(machine.IPAddress))
                    {
                        // This means we've looped around the ring, where number of members < number of monitor predecessors
                        break;
                    }

                    successors.Add(currentSuccessor);
                }
            }

            return successors;
        }

        private List<SkyNetNodeInfo> GetHeartbeatSuccessors(SortedList<string, SkyNetNodeInfo> sortedList)
        {
            List<SkyNetNodeInfo> heartbeatSucessors = new List<SkyNetNodeInfo>();
            if (sortedList.ContainsKey(this.machineId))
            {
                var currentSuccessor = sortedList[this.machineId];

                for (int i = 0; i < SkyNetConfiguration.HeartbeatSuccessors; i++)
                {
                    currentSuccessor = this.GetSuccessor(currentSuccessor, sortedList);

                    if (currentSuccessor.IPAddress.Equals(this.IPAddress))
                    {
                        // This means we've looped around the ring, where number of members < number of monitor predecessors
                        break;
                    }

                    heartbeatSucessors.Add(currentSuccessor);
                }
            }

            return heartbeatSucessors;
        }

        private List<SkyNetNodeInfo> GetHeartbeatPredecessors(SortedList<string, SkyNetNodeInfo> sortedList)
        {
            List<SkyNetNodeInfo> heartbeatPredecessors = new List<SkyNetNodeInfo>();
            if (sortedList.ContainsKey(this.machineId))
            {
                var currentPredecessor = sortedList[this.machineId];

                for (int i = 0; i < SkyNetConfiguration.HeartbeatPredecessors; i++)
                {
                    currentPredecessor = this.GetPredecessor(currentPredecessor, sortedList);

                    if (currentPredecessor.IPAddress.Equals(this.IPAddress))
                    {
                        // This means we've looped around the ring, where number of members < number of monitor predecessors
                        break;
                    }

                    heartbeatPredecessors.Add(currentPredecessor);
                }
            }

            return heartbeatPredecessors;
        }
        #endregion

        #region Log functions
        internal void LogWarning(string line)
        {
            this.Log("[Warning] " + line);
        }

        internal void LogError(string line)
        {
            this.Log("[Error] " + line, true);
        }

        internal void LogImportant(string line)
        {
            this.Log("[Important] " + line);
        }

        internal void LogVerbose(string line)
        {
            this.Log("[Verbose]" + line, false);
        }

        internal void LogDebug(string line)
        {
            this.Log("[Debug]" + line, true);
        }
        #endregion

        /// <summary>
        /// Logs the given string to file and console.
        /// </summary>
        /// <param name="line"></param>
        private void Log(string line, bool writeToConsole = true)
        {
            string timestampedLog = $"{DateTime.UtcNow} : {line}";

            if (writeToConsole)
            {
                Console.WriteLine(timestampedLog);
            }

            logFileWriter.WriteLine(timestampedLog);
        }

        private byte[] LoadFileToMemory(string stfsFileName)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                bool fileExists = Storage.Exists(stfsFileName);

                if (fileExists)
                {
                    return Storage.ReadContentAsync(stfsFileName).Result;
                }
            }

            return null;
        }
    }
}
