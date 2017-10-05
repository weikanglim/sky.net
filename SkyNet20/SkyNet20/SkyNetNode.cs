using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Sockets;
using System.Net;
using System.IO;
using SkyNet20.Utility;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using SkyNet20.Network;
using SkyNet20.Network.Commands;
using ProtoBuf;
using System.Configuration;
using SkyNet20.Configuration;

namespace SkyNet20
{
    public class SkyNetNode
    {
        private Dictionary<string, SkyNetNodeInfo> machineList = new Dictionary<string, SkyNetNodeInfo>();
        private Dictionary<string, string> localDnsCache = new Dictionary<string, string>();
        private String machineId;
        private IPHostEntry hostEntry;
        private StreamWriter logFileWriter;
        private String logFilePath;
        private bool isIntroducer;
        private static readonly byte AckLeave = 0xFA;
        private static readonly byte AckGrep = 0xF0;

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

            string machineNumber = this.GetMachineNumber(this.hostEntry.HostName);
            logFilePath = SkyNetConfiguration.LogPath
                + Path.DirectorySeparatorChar
                + $"vm.{machineNumber}.log";

            if (!Directory.Exists(SkyNetConfiguration.LogPath))
            {
                Directory.CreateDirectory(SkyNetConfiguration.LogPath);
            }

            logFileWriter = File.AppendText(logFilePath);
        }

        private string GetMachineNumber(string hostname)
        {
            string prefix = "fa17-cs425-g50-";
            string suffix = ".cs.illinois.edu";
            string machineNumber = "0";

            if (hostname.StartsWith(prefix) && hostname.EndsWith(suffix))
            {
                machineNumber = hostname.Substring(prefix.Length, 2).TrimStart('0');
            }

            return machineNumber;
        }

        private void SendHeartBeat()
        {

        }

        private bool SendJoinCommand(SkyNetNodeInfo introducer)
        {
            this.Log($"Sending join command to {introducer.HostName}.");

            bool joinSuccessful;
            byte[] joinPacket;
            IPEndPoint endPoint = introducer.EndPoint;

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

            try
            {
                UdpClient udpClient = new UdpClient();
                udpClient.Client.SendTimeout = 5000;
                udpClient.Client.ReceiveTimeout = 5000;
                udpClient.Send(joinPacket, joinPacket.Length, endPoint);

                this.Log("Waiting for join command acknowledgement.");
                byte[] received = udpClient.Receive(ref endPoint);
                this.HandleCommand(received);

                joinSuccessful = true;
            }
            catch (SocketException se)
            {
                this.LogError("Join failure due to socket exception: " + se.SocketErrorCode);
                joinSuccessful = false;
            }

            return joinSuccessful;
        }

        private bool SendLeaveCommand(SkyNetNodeInfo introducer)
        {
            this.Log($"Sending leave command to {introducer.HostName}.");

            bool leaveSucessful;
            byte[] joinPacket;
            IPEndPoint endPoint = introducer.EndPoint;

            using (MemoryStream stream = new MemoryStream())
            {
                SkyNetPacketHeader header = new SkyNetPacketHeader
                {
                    MachineId = this.machineId,
                    PayloadType = PayloadType.MembershipLeave,
                };

                Serializer.SerializeWithLengthPrefix(stream, header, PrefixStyle.Base128);
                joinPacket = stream.ToArray();
            }

            try
            {
                UdpClient udpClient = new UdpClient(SkyNetConfiguration.DefaultPort);
                udpClient.Client.SendTimeout = 5000;
                udpClient.Client.ReceiveTimeout = 5000;
                udpClient.Send(joinPacket, joinPacket.Length, endPoint);

                this.Log("Waiting for leave command acknowledgement.");
                byte[] received = udpClient.Receive(ref endPoint);
                leaveSucessful = received.Length == 1 && received[0] == AckLeave;
            }
            catch (SocketException se)
            {
                this.LogError("Leave failure due to socket exception: " + se.SocketErrorCode);
                leaveSucessful = false;
            }

            return leaveSucessful;
        }

        private void ProcessGrepCommand(SkyNetNodeInfo skyNetNode, String grepExpression)
        {
            this.Log($"Received grep request: {grepExpression}");
            byte[] ack = { AckGrep };
            UdpClient udpClient = new UdpClient();
            udpClient.Send(ack, ack.Length, skyNetNode.EndPoint);

            TcpListener server = new TcpListener(IPAddress.Any, SkyNetConfiguration.DefaultPort);
            server.Start();

            using (TcpClient client = server.AcceptTcpClient())
            {
                using (NetworkStream stream = client.GetStream())
                {
                    StreamWriter writer = new StreamWriter(stream);
                    
                    CmdResult result = CmdUtility.RunGrep(grepExpression, logFilePath);
                    int length = result.Output.Length;
                    writer.WriteLine(length);
                    writer.WriteLine(result.OutputLines);
                    writer.Write(result.Output);


                    StreamReader reader = new StreamReader(stream);
                    reader.ReadLine();
                }
            }

            server.Stop();

            this.Log("Processed grep request.");
        }

        private async Task<string> SendGrepCommand(string grepExpression, SkyNetNodeInfo skyNetNode)
        {
            string results = "";

            try
            {
                using (UdpClient client = new UdpClient(SkyNetConfiguration.DefaultPort))
                {
                    client.Client.SendTimeout = 2000;
                    client.Client.ReceiveTimeout = 2000;

                    byte[] grepPacket;
                    IPEndPoint endPoint = skyNetNode.EndPoint;

                    using (MemoryStream stream = new MemoryStream())
                    {
                        // Send grep
                        SkyNetPacketHeader packetHeader = new SkyNetPacketHeader { PayloadType = PayloadType.Grep, MachineId = machineId };
                        GrepCommand grepCommand = new GrepCommand { Query = grepExpression };

                        Serializer.SerializeWithLengthPrefix(stream, packetHeader, PrefixStyle.Base128);
                        Serializer.SerializeWithLengthPrefix(stream, grepCommand, PrefixStyle.Base128);

                        grepPacket = stream.ToArray();
                    }
                    
                    await client.SendAsync(grepPacket, grepPacket.Length, endPoint);

                    int ackRetries = 5;
                    bool ackSuccess = false;
                    while (!ackSuccess && ackRetries > 0)
                    {
                        UdpReceiveResult result = await client.ReceiveAsync();
                        ackSuccess = result.RemoteEndPoint == endPoint && result.Buffer.Length == 1 && result.Buffer[0] == AckGrep;

                        ackRetries--;
                    }


                    using (TcpClient tcpClient = new TcpClient())
                    {
                        if (!tcpClient.ConnectAsync(skyNetNode.IPAddress, SkyNetConfiguration.DefaultPort).Wait(5000))
                        {
                            throw new SocketException();
                        }

                        using (NetworkStream stream = tcpClient.GetStream())
                        {
                            // Process grep
                            StreamReader reader = new StreamReader(stream);

                            int packetLength = Convert.ToInt32(await reader.ReadLineAsync());
                            int lineCount = Convert.ToInt32(await reader.ReadLineAsync()); ;
                            string grepLogFile = $"vm.{this.GetMachineNumber(skyNetNode.HostName)}.log";

                            try
                            {
                                char[] buffer = new char[packetLength];
                                await reader.ReadAsync(buffer, 0, buffer.Length);

                                using (StreamWriter fileWriter = File.AppendText(grepLogFile))
                                {
                                    await fileWriter.WriteAsync(buffer);
                                }
                            }
                            catch (IOException e)
                            {
                                this.Log("ERROR: Unable to write file " + grepLogFile);
                                Debugger.Log((int)TraceLevel.Error, Debugger.DefaultCategory, e.ToString());
                            }

                            results = $"{skyNetNode.HostName} : {lineCount}";

                            StreamWriter writer = new StreamWriter(stream);
                            writer.WriteLine("EOT");
                        }
                    }
                }
            }
            catch (SocketException)
            {
                results = $"No response from {skyNetNode.HostName}";
            }
            catch (IOException)
            {
                results = $"No response from {skyNetNode.HostName}";
            }
            catch (AggregateException ae)
            {
                if (ae.InnerException is SocketException)
                {
                    results = $"No response from {skyNetNode.HostName}";
                }
                else
                {
                    results = "Unhandled exception " + ae.InnerException.Message;
                    Debugger.Log((int)TraceLevel.Error, Debugger.DefaultCategory, ae.ToString());
                }
            }
            catch (Exception e)
            {
                results = "Unhandled exception " + e.InnerException.Message;
                Debugger.Log((int) TraceLevel.Error, Debugger.DefaultCategory, e.ToString());
            }

            return results;
        }

        /// <summary>
        /// Runs a distributed grep command to all connected nodes.
        /// </summary>
        /// <param name="grepExp">
        /// A valid regular expression for GNU grep.
        /// </param>
        /// <returns>
        /// The distributed grep results.
        /// </returns>
        public async Task<List<String>> DistributedGrep(string grepExp)
        {
            var results = new List<String>();
            var tasks = new List<Task<String>>();

            foreach (var netNodeInfo in machineList.Values)
            {
                tasks.Add(SendGrepCommand(grepExp, netNodeInfo));
            }

            while (tasks.Count > 0)
            {
                Task<string> finishedtask = await Task.WhenAny(tasks);

                tasks.Remove(finishedtask);

                if (finishedtask.IsCompletedSuccessfully)
                {
                    results.Add(finishedtask.Result);
                }
            }

            return results;
        }

        public void HandleCommand(byte[] payload)
        {
            using (MemoryStream stream = new MemoryStream(payload))
            {
                SkyNetPacketHeader packetHeader = Serializer.DeserializeWithLengthPrefix<SkyNetPacketHeader>(stream, PrefixStyle.Base128);
                string machineId = packetHeader.MachineId;
                this.Log($"Received {packetHeader.PayloadType.ToString()} packet from {machineId}.");

                switch (packetHeader.PayloadType)
                {
                    case PayloadType.Grep:
                        GrepCommand grepCommand = Serializer.DeserializeWithLengthPrefix<GrepCommand>(stream, PrefixStyle.Base128);

                        ProcessGrepCommand(machineList[machineId], grepCommand.Query);
                        break;

                    case PayloadType.Heartbeat:
                        HeartbeatCommand heartbeatCommand = Serializer.DeserializeWithLengthPrefix<HeartbeatCommand>(stream, PrefixStyle.Base128);
                        //!TODO
                        break;

                    case PayloadType.MembershipJoin:
                        if (this.isIntroducer)
                        {
                            IPAddress introducerAddress = SkyNetNodeInfo.ParseMachineId(machineId).Item1;
                            SkyNetNodeInfo joinedNode = new SkyNetNodeInfo(this.GetHostName(introducerAddress), machineId, this.GetEndPoint(introducerAddress));
                            this.machineList.Add(joinedNode.MachineId, joinedNode);

                            this.LogImportant($"{machineId} has joined.");
                        }
                        else
                        {
                            this.LogWarning($"Not a coordinator, but received request to join.");
                        }

                        break;

                    case PayloadType.MembershipLeave:
                        if (this.isIntroducer)
                        {
                            machineList.Remove(machineId);

                            this.LogImportant($"{machineId} has left.");
                        }
                        else
                        {
                            this.LogWarning($"Not a coordinator, but received request to leave.");
                        }
                        break;

                    case PayloadType.MembershipUpdate:
                        MembershipUpdateCommand updateCommand = Serializer.DeserializeWithLengthPrefix<MembershipUpdateCommand>(stream, PrefixStyle.Base128);
                        this.MergeMembershipList(updateCommand.machineList);
                        break;
                }

            }
        }

        public async Task ReceiveCommand()
        {
            UdpClient server = new UdpClient(SkyNetConfiguration.DefaultPort);
            IPEndPoint groupEP = new IPEndPoint(IPAddress.Any, SkyNetConfiguration.DefaultPort);

            while (true)
            {
                this.Log($"Waiting for a connection on port {((IPEndPoint)server.Client.LocalEndPoint).Port}... ");

                try
                {
                    UdpReceiveResult result = await server.ReceiveAsync();
                    byte[] received = result.Buffer;

                    this.HandleCommand(received);
                }
                catch (Exception e)
                {
                    this.Log("Exception: " + e);
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
                    string cmd = await Console.In.ReadLineAsync();
                    byte option;

                    if (Byte.TryParse(cmd, out option))
                    {
                        switch (option)
                        {
                            case 1:
                                foreach (var keyValuePair in this.machineList)
                                {
                                    Console.WriteLine($"{keyValuePair.Key} (${keyValuePair.Value.HostName})");
                                }
                                break;

                            case 2:
                                Console.WriteLine($"{this.machineId} (${this.hostEntry.HostName})");
                                break;

                            case 3:
                                this.SendJoinCommand(this.GetIntroducer());
                                break;

                            case 4:
                                this.SendLeaveCommand(this.GetIntroducer());
                                break;

                            default:
                                Console.WriteLine("Invalid command. Please enter an integer between 1-4 as listed above.");
                                break;
                        }
                    }
                    else
                    {
                        Console.WriteLine("Invalid command. Please enter an integer between 1-4 as listed above.");
                    }
                }
                catch (Exception e)
                {
                    this.LogError(e.StackTrace);
                }
            }
        }

        /// <summary>
        /// Runs the <see cref="SkyNetNode"/> as a server node.
        /// </summary>
        public void Run()
        {
            // Add self to membership list
            SkyNetNodeInfo currentNode = new SkyNetNodeInfo(this.hostEntry.HostName, this.machineId, this.GetEndPoint(this.hostEntry.HostName));
            machineList.Add(currentNode.MachineId, currentNode);

            //if (!this.isIntroducer)
            //{
            //    // Join the network
            //    var introducerHostName = SkyNetConfiguration.Machines.Where(kv => kv.Value.IsIntroducer == true).Select(kv => kv.Key).First();
            //    SkyNetNodeInfo introducer = new SkyNetNodeInfo(introducerHostName, this.GetIpAddress(introducerHostName).ToString(), this.GetEndPoint(introducerHostName));
                
            //    while (!this.SendJoinCommand(introducer))
            //    {
            //        this.Log("Re-trying join command.");
            //        Thread.Sleep(1000);
            //    }
            //}

            List<Task> serverTasks = new List<Task>
            {
                ReceiveCommand(),
                PromptUser(),
            };

            foreach (Task task in serverTasks)
            {
                task.Start();
            }

            Task.WaitAll(serverTasks.ToArray());
        }

        /// <summary>
        /// Runs the <see cref="SkyNetNode"/> in interactive mode, acts as a client for debugging purposes.
        /// </summary>
        public void RunInteractive()
        {
            // Retrieve all known nodes from configuration
            foreach (var hostName in SkyNetConfiguration.HostNames)
            {
                SkyNetNodeInfo nodeInfo = new SkyNetNodeInfo(hostName, this.GetIpAddress(hostName).ToString(), this.GetEndPoint(hostName));
                machineList.Add(hostName, nodeInfo);
            }

            while (true)
            {
                Console.WriteLine();
                Console.Write("Query log files: ");
                string cmd = Console.ReadLine();

                if (cmd.Equals("exit", StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }

                string grepExp = cmd;
                Stopwatch stopWatch = new Stopwatch();
                stopWatch.Start();
                var distributedGrep = this.DistributedGrep(grepExp).ConfigureAwait(false).GetAwaiter().GetResult();
                stopWatch.Stop();

                Console.WriteLine($"Results in {stopWatch.ElapsedMilliseconds} ms.");
                int totalLength = 0;
                foreach (var line in distributedGrep)
                {
                    Console.WriteLine(line);
                    int lineCount = 0;
                    string[] res = line.Split(":");
                    if (res.Length >= 2 && Int32.TryParse(res[1].Trim(), out lineCount))
                    {
                        totalLength += lineCount;
                    }
                }

                Console.WriteLine("Total: " + totalLength);
            }

        }

        public void MergeMembershipList(Dictionary<string, SkyNetNodeInfo> listToMerge)
        {
            var additions = listToMerge.Where(entry => !machineList.ContainsKey(entry.Key));
            var deletions = machineList.Where(entry => !listToMerge.ContainsKey(entry.Key));
            var updates = listToMerge.Where(entry => machineList.ContainsKey(entry.Key) && entry.Value.LastHeartbeat > machineList[entry.Key].LastHeartbeat);

            foreach (var addition in additions)
            {
                IPAddress addressToAdd = SkyNetNodeInfo.ParseMachineId(addition.Key).Item1;
                SkyNetNodeInfo nodeToAdd = new SkyNetNodeInfo(this.GetHostName(addressToAdd), addition.Key, this.GetEndPoint(addressToAdd))
                {
                    LastHeartbeat = DateTime.UtcNow.Ticks
                };

                machineList.Add(nodeToAdd.MachineId, nodeToAdd);

                this.LogImportant($"Added {addition.Key} to membership list.");
            }

            foreach (var deletion in deletions)
            {
                machineList.Remove(deletion.Key);

                this.LogImportant($"Removed {deletion.Key} from membership list.");
            }

            foreach (var update in updates)
            {
                var itemToUpdate = machineList[update.Key];

                itemToUpdate.LastHeartbeat = DateTime.UtcNow.Ticks;

                this.LogImportant($"Updated {update.Key} last heartbeat to {itemToUpdate.LastHeartbeat}");
            }
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

        private SkyNetNodeInfo GetIntroducer()
        {
            var introducerHostName = SkyNetConfiguration.Machines.Where(kv => kv.Value.IsIntroducer == true).Select(kv => kv.Key).First();

            return new SkyNetNodeInfo(introducerHostName, this.GetIpAddress(introducerHostName).ToString(), this.GetEndPoint(introducerHostName));
        }

        public void LogWarning(string line)
        {
            this.Log("[Warning] " + line);
        }

        public void LogError(string line)
        {
            this.Log("[Error] " + line);
        }

        public void LogImportant(string line)
        {
            this.Log("[Important] " + line);
        }

        /// <summary>
        /// Logs the given string to file and console.
        /// </summary>
        /// <param name="line"></param>
        public void Log(string line)
        {
            string timestampedLog = $"{DateTime.UtcNow} : {line}";
            Console.WriteLine(timestampedLog);
            logFileWriter.WriteLine(timestampedLog);
        }
    }
}
