using System;
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
using SkyNet20.Configuration;
using SkyNet20.Extensions;

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
        private bool isConnected = false;

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
            logFileWriter.AutoFlush = true;
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
            this.machineId = SkyNetNodeInfo.GetMachineId(SkyNetNodeInfo.ParseMachineId(this.machineId).Item1);
            this.Log($"Updated machine id to {this.machineId}");

            this.Log($"Sending join command to {introducer.HostName}.");
            bool joinSuccessful;
            byte[] joinPacket;

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
                using (UdpClient udpClient = new UdpClient())
                {
                    udpClient.Client.SendTimeout = 2000;
                    udpClient.Client.ReceiveTimeout = 2000;
                    udpClient.Send(joinPacket, joinPacket.Length, introducer.DefaultEndPoint);

                    joinSuccessful = true;
                    this.isConnected = true;
                }
            }
            catch (SocketException se)
            {
                this.LogError("Join failure due to socket exception: " + se.SocketErrorCode);
                this.machineList.Clear();

                joinSuccessful = false;
            }

            return joinSuccessful;
        }

        private bool SendLeaveCommand(SkyNetNodeInfo introducer)
        {
            this.Log($"Sending leave command to {introducer.HostName}.");

            bool leaveSucessful;
            byte[] joinPacket;

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
                using (UdpClient udpClient = new UdpClient())
                {
                    udpClient.Client.SendTimeout = 5000;
                    udpClient.Client.ReceiveTimeout = 5000;
                    udpClient.Send(joinPacket, joinPacket.Length, introducer.DefaultEndPoint);

                    this.machineList.Clear();
                    leaveSucessful = true;
                    this.isConnected = false;
                }
            }
            catch (SocketException se)
            {
                this.LogError("Leave failure due to socket exception: " + se.SocketErrorCode);
                leaveSucessful = false;
            }
            catch (Exception e)
            {
                this.LogError("Leave failure due to socket exception: " + e.StackTrace);
                leaveSucessful = false;
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
                    machineList = this.machineList,
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
                this.LogError("Send membership failure due to socket exception: " + e.StackTrace);
                membershipSent = false;
            }

            return membershipSent;
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
                    this.LogError("Unexpected error: " + ex.StackTrace);
                }

                await Task.Delay(SkyNetConfiguration.GossipRoundInterval);
            }
        }

        private void ProcessLeaveCommand(string machineId)
        {
            if (this.isIntroducer)
            {
                if (machineList.ContainsKey(machineId))
                {
                    machineList.Remove(machineId);

                    this.LogImportant($"{machineId} has left.");
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
                SkyNetNodeInfo joinedNode = new SkyNetNodeInfo(this.GetHostName(introducerAddress), machineId);

                if (!this.machineList.ContainsKey(machineId))
                {
                    this.machineList.Add(joinedNode.MachineId, joinedNode);
                    this.LogImportant($"{machineId} has joined.");

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

        private async Task ProcessGrepCommand(SkyNetNodeInfo skyNetNode, String grepExpression)
        {
            this.Log($"Received grep request: {grepExpression}");
            TcpListener server = new TcpListener(IPAddress.Any, SkyNetConfiguration.DefaultPort);

            try
            {
                server.Start();

                this.Log($"Starting tcp connection to transmit grep results");
                using (TcpClient client = await server.AcceptTcpClientAsync().WithTimeout(TimeSpan.FromMilliseconds(1500)))
                {
                    using (NetworkStream stream = client.GetStream())
                    {
                        StreamWriter writer = new StreamWriter(stream);

                        try
                        {
                            CmdResult result = CmdUtility.RunGrep(grepExpression, logFilePath);
                            int length = result.Output.Length;
                            writer.WriteLine(length);
                            writer.WriteLine(result.OutputLines);
                            writer.Write(result.Output);
                            this.Log($"Transmitted grep results");
                        }
                        catch (PlatformNotSupportedException)
                        {
                            this.Log("Skipping grep command on non-linux machine...", false);
                        }
                    }
                }

                this.Log("Processed grep request.");
            }
            catch (Exception e)
            {
                this.LogError("Failure due to error: " + e.StackTrace);
            }
            finally
            {
                server.Stop();
            }
        }

        private async Task<string> SendGrepCommand(string grepExpression, SkyNetNodeInfo skyNetNode)
        {
            string results = "";

            try
            {
                using (UdpClient client = new UdpClient())
                {
                    byte[] grepPacket;

                    using (MemoryStream stream = new MemoryStream())
                    {
                        // Send grep
                        SkyNetPacketHeader packetHeader = new SkyNetPacketHeader { PayloadType = PayloadType.Grep, MachineId = machineId };
                        GrepCommand grepCommand = new GrepCommand { Query = grepExpression };

                        Serializer.SerializeWithLengthPrefix(stream, packetHeader, PrefixStyle.Base128);
                        Serializer.SerializeWithLengthPrefix(stream, grepCommand, PrefixStyle.Base128);

                        grepPacket = stream.ToArray();
                    }

                    this.Log("Sending grep packet.");
                    await client.SendAsync(grepPacket, grepPacket.Length, skyNetNode.DefaultEndPoint).WithTimeout(TimeSpan.FromMilliseconds(1000));
                    await Task.Delay(100);

                    using (TcpClient tcpClient = new TcpClient())
                    {
                        await tcpClient.ConnectAsync(skyNetNode.IPAddress, SkyNetConfiguration.DefaultPort).WithTimeout(TimeSpan.FromMilliseconds(1000));
                        this.Log($"Connected to {skyNetNode.HostName} for file transfer.");

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
                this.LogVerbose($"Received {packetHeader.PayloadType.ToString()} packet from {machineId}.");

                if (this.isConnected)
                {
                    switch (packetHeader.PayloadType)
                    {
                        case PayloadType.Grep:
                            GrepCommand grepCommand = Serializer.DeserializeWithLengthPrefix<GrepCommand>(stream, PrefixStyle.Base128);

                            Task.Run(() => ProcessGrepCommand(machineList[machineId], grepCommand.Query));
                            break;

                        case PayloadType.Heartbeat:
                            HeartbeatCommand heartbeatCommand = Serializer.DeserializeWithLengthPrefix<HeartbeatCommand>(stream, PrefixStyle.Base128);
                            //!TODO
                            break;

                        case PayloadType.MembershipJoin:
                            this.ProcessJoinCommand(machineId);
                            break;

                        case PayloadType.MembershipLeave:
                            this.ProcessLeaveCommand(machineId);
                            break;

                        case PayloadType.MembershipUpdate:
                            MembershipUpdateCommand updateCommand = Serializer.DeserializeWithLengthPrefix<MembershipUpdateCommand>(stream, PrefixStyle.Base128);
                            this.ProcessMembershipUpdateCommand(machineId, updateCommand);
                            break;
                    }
                }
            }
        }

        public async Task ReceiveCommand()
        {
            UdpClient server = new UdpClient(SkyNetConfiguration.DefaultPort);

            while (true)
            {
                this.LogVerbose($"Waiting for a connection on port {((IPEndPoint)server.Client.LocalEndPoint).Port}... ");

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
                    Console.WriteLine("[5] Query for log file");

                    string cmd = await ReadConsoleAsync();

                    if (Byte.TryParse(cmd, out byte option))
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
                                bool joined = this.SendJoinCommand(this.GetIntroducer());

                                if (joined && !this.isIntroducer)
                                {
                                    // Wait for membership list to be sent
                                    for (int i = 0; i < 10 && machineList.Count <= 1; i++)
                                    {
                                        await Task.Delay(200);
                                    }

                                    if (machineList.Count <= 1)
                                    {
                                        Console.WriteLine("Join was successful, but no membership list received after 2 seconds.");
                                    }
                                }

                                // UI waits
                                await Task.Delay(TimeSpan.FromMilliseconds(200));
                                break;

                            case 4:
                                if (this.isConnected)
                                {
                                    this.SendLeaveCommand(this.GetIntroducer());

                                    // UI waits
                                    await Task.Delay(TimeSpan.FromMilliseconds(200));
                                }
                                else
                                {
                                    Console.WriteLine("Unable to leave group, machine is not currently joined to group.");
                                }

                                break;

                            case 5:
                                if (this.isConnected)
                                {
                                    Console.Write("Grep expression:");
                                    string grepExpression = await ReadConsoleAsync();

                                    Stopwatch stopWatch = new Stopwatch();
                                    stopWatch.Start();
                                    var distributedGrep = this.DistributedGrep(grepExpression).ConfigureAwait(false).GetAwaiter().GetResult();
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
                                else
                                {
                                    Console.WriteLine("Unable to perform grep, machine is not currently joined to group.");
                                }
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
            // Auto-join
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

            Task[] serverTasks = {
                ReceiveCommand(),
                PromptUser(),
                DisseminateMembershipList(),
            };

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
                SkyNetNodeInfo nodeInfo = new SkyNetNodeInfo(hostName, SkyNetNodeInfo.GetMachineId(this.GetIpAddress(hostName)));
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
            // Handle false-positive and updates regarding self
            var additions = listToMerge.Where(entry => !machineList.ContainsKey(entry.Key));
            var deletions = machineList.Where(entry => !listToMerge.ContainsKey(entry.Key));
            //var updates = listToMerge.Where(entry => machineList.ContainsKey(entry.Key) && entry.Value.LastHeartbeat > machineList[entry.Key].LastHeartbeat);

            foreach (var addition in additions)
            {
                IPAddress addressToAdd = SkyNetNodeInfo.ParseMachineId(addition.Key).Item1;
                SkyNetNodeInfo nodeToAdd = new SkyNetNodeInfo(this.GetHostName(addressToAdd), addition.Key)
                {
                    LastHeartbeat = DateTime.UtcNow.Ticks
                };

                machineList.Add(nodeToAdd.MachineId, nodeToAdd);

                this.Log($"Added {addition.Key} to membership list.");
            }

            foreach (var deletion in deletions)
            {
                machineList.Remove(deletion.Key);

                this.Log($"Removed {deletion.Key} from membership list.");

                if (deletion.Key == this.machineId)
                {
                    // Self was detected as false-positive removal, change state to be disconnected
                    this.isConnected = false;
                    this.machineList.Clear();
                    this.Log($"Disconnected from group.");
                    return;
                }
            }

            //foreach (var update in updates)
            //{
            //    var itemToUpdate = machineList[update.Key];

            //    itemToUpdate.LastHeartbeat = DateTime.UtcNow.Ticks;

            //    this.Log($"Updated {update.Key} last heartbeat to {itemToUpdate.LastHeartbeat}");
            //}
        }

        private bool SendAck(byte ackByte, SkyNetNodeInfo nodeInfo)
        {
            this.Log($"Sending ack {ackByte.ToString("x")} to {nodeInfo.MachineId} ({nodeInfo.HostName})");
            bool ackSuccessful = false;
            int numRetries = 3;

            while (!ackSuccessful && numRetries > 0)
            {
                try
                {
                    UdpClient client = new UdpClient();
                    byte[] ackPacket = { ackByte };
                    client.Send(ackPacket, ackPacket.Length, nodeInfo.DefaultEndPoint);

                    ackSuccessful = true;
                }
                catch (Exception e)
                {
                    ackSuccessful = false;
                    numRetries++;

                    if (numRetries > 0)
                    {
                        this.Log("Retrying send ack...");
                    }
                    else
                    {
                        this.Log($"Sending ack failed after {numRetries} due to exception ({e.Message}): " + e.StackTrace);
                    }
                }
            }

            return ackSuccessful;
        }

        private Task<string> ReadConsoleAsync()
        {
            return Task.Run(() => Console.ReadLine());
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

            return new SkyNetNodeInfo(introducerHostName, SkyNetNodeInfo.GetMachineId(this.GetIpAddress(introducerHostName)));
        }

        private void LogWarning(string line)
        {
            this.Log("[Warning] " + line);
        }

        private void LogError(string line)
        {
            this.Log("[Error] " + line);
        }

        private void LogImportant(string line)
        {
            this.Log("[Important] " + line);
        }

        private void LogVerbose(string line)
        {
            this.Log("[Verbose]" + line, false);
        }

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
    }
}
