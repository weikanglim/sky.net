using System;
using System.Collections.Generic;
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

namespace SkyNet20
{
    public class SkyNetNode
    {
        private Dictionary<string, SkyNetNodeInfo> skyNetNodeDictionary = new Dictionary<string, SkyNetNodeInfo>();
        private StreamWriter logFileWriter;
        private String logFilePath;

        /// <summary>
        /// Initializes the instance of <see cref="SkyNetNode"/> class.
        /// </summary>
        public SkyNetNode()
        {
            foreach (var hostName in SkyNetConfiguration.HostNames)
            {
                IPAddress address = Dns.GetHostAddresses(hostName)[0];

                SkyNetNodeInfo nodeInfo = new SkyNetNodeInfo
                {
                    IPAddress = address,
                    HostName = hostName,
                    EndPoint = new IPEndPoint(address, SkyNetConfiguration.DefaultPort),
                };

                skyNetNodeDictionary.Add(address.ToString(), nodeInfo);
            }

            string machineNumber = this.GetMachineNumber(Dns.GetHostName());
            logFilePath = SkyNetConfiguration.LogPath
                + Path.DirectorySeparatorChar
                + $"vm.{machineNumber}.log";

            
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

        private void ProcessGrepCommand(SkyNetNodeInfo skyNetNode, String grepExpression)
        {
            this.Log($"Received grep request: {grepExpression}");
            byte[] ack = { 0x02 };
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
                    byte[] grepPacket;
                    IPEndPoint endPoint = skyNetNode.EndPoint;

                    using (MemoryStream stream = new MemoryStream())
                    {
                        // Send grep
                        SkyNetPacketHeader packetHeader = new SkyNetPacketHeader { PayloadType = PayloadType.Grep };
                        GrepCommand grepCommand = new GrepCommand { Query = grepExpression };

                        Serializer.SerializeWithLengthPrefix(stream, packetHeader, PrefixStyle.Base128);
                        Serializer.SerializeWithLengthPrefix(stream, grepCommand, PrefixStyle.Base128);

                        grepPacket = stream.ToArray();
                    }

                    await client.SendAsync(grepPacket, grepPacket.Length, endPoint);
                    byte [] awk = client.Receive(ref endPoint);

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
                skyNetNode.Status = Status.Dead;
                results = $"No response from {skyNetNode.HostName}";
            }
            catch (IOException)
            {
                skyNetNode.Status = Status.Dead;
                results = $"No response from {skyNetNode.HostName}";
            }
            catch (AggregateException ae)
            {
                if (ae.InnerException is SocketException)
                {
                    skyNetNode.Status = Status.Dead;
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

            foreach (var netNodeInfo in skyNetNodeDictionary.Values)
            {
                if (netNodeInfo.Status == Status.Alive)
                {
                    tasks.Add(SendGrepCommand(grepExp, netNodeInfo));
                }
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

        /// <summary>
        /// Runs the <see cref="SkyNetNode"/> as a server node.
        /// </summary>
        public void Run()
        {
            UdpClient server = new UdpClient(SkyNetConfiguration.DefaultPort);
            IPEndPoint groupEP = new IPEndPoint(IPAddress.Any, SkyNetConfiguration.DefaultPort);

            while (true)
            {
                this.Log($"Waiting for a connection on port {((IPEndPoint)server.Client.LocalEndPoint).Port}... ");

                try
                {
                    byte[] received = server.Receive(ref groupEP);
                    //this.Log("Connected to: " + ((IPEndPoint)client.Client.RemoteEndPoint).Address.ToString());

                    // Treat all packets as grep commands for now
                    using (MemoryStream stream = new MemoryStream(received))
                    {
                        SkyNetPacketHeader packetHeader = Serializer.DeserializeWithLengthPrefix<SkyNetPacketHeader>(stream, PrefixStyle.Base128);
                        var qualifiedMachineId = SkyNetNodeInfo.ParseMachineId(packetHeader.MachineId);
                        var (machineId, timestamp) = qualifiedMachineId;

                        this.Log($"Received {packetHeader.PayloadType.ToString()} packet from {machineId}.");

                        switch (packetHeader.PayloadType)
                        {
                            case PayloadType.Grep:
                                GrepCommand grepCommand = Serializer.DeserializeWithLengthPrefix<GrepCommand>(stream, PrefixStyle.Base128);
                                
                                ProcessGrepCommand(skyNetNodeDictionary[machineId.ToString()], grepCommand.Query);
                                break;

                            case PayloadType.Heartbeat:
                                HeartbeatCommand heartbeatCommand = Serializer.DeserializeWithLengthPrefix<HeartbeatCommand>(stream, PrefixStyle.Base128);
                                //!TODO
                                break;

                            case PayloadType.MembershipJoin:
                                if (SkyNetConfiguration.IsCoordinator)
                                {
                                    MembershipJoinCommand joinCommand = Serializer.DeserializeWithLengthPrefix<MembershipJoinCommand>(stream, PrefixStyle.Base128);
                                }
                                else
                                {
                                    this.LogWarning($"Not a coordinator, but received request to join.");
                                }

                                break;

                            case PayloadType.MembershipLeave:
                                if (SkyNetConfiguration.IsCoordinator)
                                {
                                    MembershipLeaveCommand leaveCommand = Serializer.DeserializeWithLengthPrefix<MembershipLeaveCommand>(stream, PrefixStyle.Base128);
                                }
                                else
                                {
                                    this.LogWarning($"Not a coordinator, but received request to leave.");
                                }
                                break;

                            case PayloadType.MembershipUpdate:
                                MembershipUpdateCommand updateCommand = Serializer.DeserializeWithLengthPrefix<MembershipUpdateCommand>(stream, PrefixStyle.Base128);

                                break;
                        }

                    }

                    this.Log("Closing client connection");
                }
                catch (Exception e)
                {
                    this.Log("Exception: " + e);
                }
            }
        }

        /// <summary>
        /// Runs the <see cref="SkyNetNode"/> in interactive mode, acts as a client for debugging purposes.
        /// </summary>
        public void RunInteractive()
        {
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


        public void LogWarning(string line)
        {
            this.Log("[Warning] " + line);
        }

        public void LogError(string line)
        {
            this.Log("[Error] " + line);
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
