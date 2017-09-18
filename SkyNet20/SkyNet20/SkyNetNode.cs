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

namespace SkyNet20
{
    class SkyNetNode
    {
        private Dictionary<string, SkyNetNodeInfo> skyNetNodeDictionary = new Dictionary<string, SkyNetNodeInfo>();
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

                skyNetNodeDictionary.Add(hostName, nodeInfo);
            }

            string machineNumber = this.GetMachineNumber(Dns.GetHostName());
            logFilePath = SkyNetConfiguration.LogPath
                + Path.DirectorySeparatorChar
                + $"vm.{machineNumber}.log";
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

        private void ProcessCommand(SkyNetCommand cmd)
        {

        }

        private void ProcessGrepCommand(NetworkStream stream, String grepExpression)
        {
            Console.WriteLine($"Received grep request: {grepExpression}");
            
            using (StreamWriter writer = new StreamWriter(stream))
            {
                CmdResult result =  CmdUtility.RunGrep(grepExpression, logFilePath);
                int length = result.Output.Length;
                writer.WriteLine(length);
                writer.WriteLine(result.OutputLines);

                writer.Write(result.Output);
            }

            Console.WriteLine("Processed grep request.");
        }

        private async Task<string> SendGrepCommand(string grepExpression, SkyNetNodeInfo skyNetNode)
        {
            string results = "";

            try
            {
                using (TcpClient client = new TcpClient())
                {

                    if (!client.ConnectAsync(skyNetNode.EndPoint.Address, skyNetNode.EndPoint.Port).Wait(5000))
                    {
                        throw new SocketException();
                    }

                    using (NetworkStream stream = client.GetStream())
                    {
                        // Send grep
                        StreamReader reader = new StreamReader(stream);
                        StreamWriter writer = new StreamWriter(stream);
                        await writer.WriteLineAsync(grepExpression);
                        writer.Flush();

                        // Process grep
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
                            Console.WriteLine("ERROR: Unable to write file " + grepLogFile);
                            Debugger.Log((int)TraceLevel.Error, Debugger.DefaultCategory, e.ToString());
                        }

                        results = $"{skyNetNode.HostName} : {lineCount}";
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
            TcpListener server = new TcpListener(IPAddress.Any, SkyNetConfiguration.DefaultPort);
            server.Start();

            while (true)
            {
                Console.WriteLine($"Waiting for a connection on port {((IPEndPoint)server.LocalEndpoint).Port}... ");

                try
                {
                    using (TcpClient client = server.AcceptTcpClient())
                    {

                        Console.WriteLine("Connected to: " + ((IPEndPoint)client.Client.RemoteEndPoint).Address.ToString());

                        // Treat all packets as grep commands for now
                        using (NetworkStream stream = client.GetStream())
                        {
                            StreamReader reader = new StreamReader(stream);
                            string request = reader.ReadLine();

                            ProcessGrepCommand(stream, request);
                        }

                        Console.WriteLine("Closing client connection");
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine("Exception: " + e);
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
                    if (Int32.TryParse(line.Split(":")[1].Trim(), out lineCount))
                    {
                        totalLength += lineCount;
                    }
                }

                Console.WriteLine("Total: " + totalLength);
            }

        }

    }
}
