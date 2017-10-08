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
using System.Collections.Concurrent;
using System.Runtime.InteropServices;

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
        private TcpListener grepListener;
        private IPAddress IPAddress
        {
            get
            {
                return this.GetIpAddress(this.hostEntry.HostName);
            }
        }

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

            string machineNumber = this.GetMachineNumber(this.hostEntry.HostName);
            string logPath;

            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // Use bash to get back actual path
                logPath = CmdUtility.RunCmd("echo " + SkyNetConfiguration.LogPath).Output;
                logPath = logPath.TrimEnd('\n');
            }
            else
            {
                logPath = SkyNetConfiguration.LogPath;
            }

            logFilePath = logPath
                + Path.DirectorySeparatorChar
                + $"vm.{machineNumber}.log";
                
            if (!Directory.Exists(logPath))
            {
                Directory.CreateDirectory(logPath);
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
                            Thread.Sleep(200);
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
                        this.LogError($"Unable to send command to {introducer.HostName}, error : " + e.StackTrace);
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
                        this.LogError($"Unable to send command to {introducer.HostName}, error : " + e.StackTrace);
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
                this.LogError("Send membership failure due to exception: " + e.StackTrace);
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

                Serializer.SerializeWithLengthPrefix(stream, header, PrefixStyle.Base128);
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
                this.LogError("Heartbeat failure due to exception: " + e.StackTrace);
                heartbeatSent = false;
            }

            return heartbeatSent;
        }

        private void SendHeartBeats(SortedList<string, SkyNetNodeInfo> ringList)
        {
            foreach (SkyNetNodeInfo predecessor in this.GetHeartbeatSuccessors(ringList))
            {
                SendHeartBeatCommand(predecessor);
            }

            foreach (SkyNetNodeInfo sucessor in this.GetHeartbeatSuccessors(ringList))
            {
                SendHeartBeatCommand(sucessor);
            }
        }

        private void DetectFailures(SortedList<string, SkyNetNodeInfo> ringList)
        {
            HashSet<string> failures = new HashSet<string>();
            // Update self's heartbeat
            if (machineList.TryGetValue(machineId, out SkyNetNodeInfo self))
            {
                self.LastHeartbeat = DateTime.UtcNow.Ticks;
            }

            foreach (var element in this.GetHeartbeatSuccessors(ringList))
            {
                // Check for heartbeats exceeding timeout
                DateTime lastHeartbeat = new DateTime(element.LastHeartbeat);

                if (element.Status == Status.Alive && DateTime.UtcNow - lastHeartbeat > TimeSpan.FromMilliseconds(SkyNetConfiguration.HeartbeatTimeout))
                {
                    failures.Add(element.MachineId);
                }
            }

            foreach (var element in this.GetHeartbeatPredecessors(ringList))
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
        }

        private async Task PeriodicHeartBeat()
        {
            while (true)
            {
                try
                {
                    if (this.isConnected)
                    {
                        SortedList<string, SkyNetNodeInfo> ringList = new SortedList<string, SkyNetNodeInfo>(this.machineList);
                        this.DetectFailures(ringList);

                        this.SendHeartBeats(ringList);
                    }
                }
                catch (Exception ex)
                {
                    this.LogError("Unexpected error: " + ex.StackTrace);
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
                    this.LogError("Unexpected error: " + ex.StackTrace);
                }

                await Task.Delay(SkyNetConfiguration.GossipRoundInterval);
            }
        }

        private void ProcessLeaveCommand(string machineId)
        {
            if (this.isIntroducer)
            {
                if (machineList.TryGetValue(machineId, out SkyNetNodeInfo leftNode))
                {
                    leftNode.Status = Status.Failed;

                    this.LogImportant($"{machineId} has left.");

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

                if (!this.machineList.ContainsKey(machineId))
                {
                    this.machineList.TryAdd(joinedNode.MachineId, joinedNode);
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

        private void ProcessHeartbeatCommand(string machineId)
        {
            if (this.machineId == machineId)
            {
                this.LogError("Received heartbeat from self.");
                return;
            }

            if (this.machineList.TryGetValue(machineId, out SkyNetNodeInfo update))
            {
                update.LastHeartbeat = DateTime.UtcNow.Ticks;
            }
        }

        private async Task ProcessGrepCommand(SkyNetNodeInfo skyNetNode, String grepExpression)
        {
            this.LogVerbose($"Received grep request: {grepExpression}");

            try
            {
                using (TcpClient client = await grepListener.AcceptTcpClientAsync().WithTimeout(TimeSpan.FromMilliseconds(1500)))
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
                        }
                        catch (PlatformNotSupportedException)
                        {
                            this.LogVerbose("Skipping grep command on non-linux machine...");
                        }
                    }
                }

                this.LogVerbose("Processed grep request.");
            }
            catch (Exception e)
            {
                this.LogError("Failure due to error: " + e.StackTrace);
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
                    
                    await client.SendAsync(grepPacket, grepPacket.Length, skyNetNode.DefaultEndPoint).WithTimeout(TimeSpan.FromMilliseconds(1000));
                    await Task.Delay(100);

                    using (TcpClient tcpClient = new TcpClient())
                    {
                        await tcpClient.ConnectAsync(skyNetNode.IPAddress, SkyNetConfiguration.DefaultPort).WithTimeout(TimeSpan.FromMilliseconds(1000));
                        this.LogVerbose($"Connected to {skyNetNode.HostName} for file transfer.");

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
                                this.LogError("ERROR: Unable to write file " + grepLogFile);
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
                            this.ProcessHeartbeatCommand(machineId);
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

                                    grepListener.Stop();
                                    Environment.Exit(0);
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
                                        string[] res = line.Split(":");
                                        if (res.Length >= 2 && Int32.TryParse(res[1].Trim(), out int lineCount))
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
            grepListener = new TcpListener(IPAddress.Any, SkyNetConfiguration.DefaultPort);
            grepListener.Start();

            Task[] serverTasks = {
                ReceiveCommand(),
                PromptUser(),

                DisseminateMembershipList(),
                PeriodicHeartBeat(),
            };

            Task.WaitAll(serverTasks.ToArray());
        }

        public void MergeMembershipList(Dictionary<string, SkyNetNodeInfo> listToMerge)
        {
            // First, detect if self has failed.
            bool selfHasFailed = (listToMerge.ContainsKey(this.machineId) && listToMerge[this.machineId].Status == Status.Failed);

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
            var updates = listToMerge.Where(entry => entry.Key != this.machineId && machineList.ContainsKey(entry.Key) && entry.Value.LastHeartbeat > machineList[entry.Key].LastHeartbeat);

            foreach (var addition in additions)
            {
                IPAddress addressToAdd = SkyNetNodeInfo.ParseMachineId(addition.Key).Item1;
                SkyNetNodeInfo nodeToAdd = new SkyNetNodeInfo(this.GetHostName(addressToAdd), addition.Key)
                {
                    LastHeartbeat = DateTime.UtcNow.Ticks,
                    Status = addition.Value.Status
                };

                machineList.AddOrUpdate(nodeToAdd.MachineId, nodeToAdd, (key, oldValue) =>
                {
                    oldValue.LastHeartbeat = DateTime.UtcNow.Ticks;
                    oldValue.Status = addition.Value.Status;
                    return oldValue;
                });

                this.LogVerbose($"Added {addition.Key} to membership list.");
            }

            foreach (var deletion in deletions)
            {
                machineList.TryRemove(deletion.Key, out SkyNetNodeInfo value);

                this.LogVerbose($"Removed {deletion.Key} from membership list.");
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

                    itemToUpdate.LastHeartbeat = DateTime.UtcNow.Ticks;

                    this.LogVerbose($"Updated {update.Key} last heartbeat to {itemToUpdate.LastHeartbeat}");
                }
            }
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

        private void LogWarning(string line)
        {
            this.Log("[Warning] " + line);
        }

        private void LogError(string line)
        {
            this.Log("[Error] " + line, false);
        }

        private void LogImportant(string line)
        {
            this.Log("[Important] " + line);
        }

        private void LogVerbose(string line)
        {
            this.Log("[Verbose]" + line, false);
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
                sucessorIndex = 0;
            }

            return sortedList.ElementAt(sucessorIndex).Value;
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
