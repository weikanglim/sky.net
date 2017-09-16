using System;
using System.Collections.Generic;
using System.Text;
using System.Net.Sockets;
using System.Net;
using System.IO;

namespace SkyNet20
{
    class SkyNetNode
    {
        private Dictionary<string, SkyNetNodeInfo> skyNetNodeDictionary = new Dictionary<string, SkyNetNodeInfo>();
        private String logFilePath;

        public SkyNetNode()
        {
            foreach (var hostName in SkyNetConfiguration.HostNames)
            {
                IPAddress address = Dns.GetHostAddresses(hostName)[0];

                SkyNetNodeInfo nodeInfo = new SkyNetNodeInfo
                {
                    IPAddress = address,
                    HostName = hostName
                };

                skyNetNodeDictionary.Add(hostName, nodeInfo);
            }

            string machineNumber = this.GetMachineNumber(Dns.GetHostName());
            logFilePath = SkyNetConfiguration.LogPath + $"machine.{machineNumber}.log";
        }

        private string GetMachineNumber(string hostname)
        {
            string prefix = "fa17-cs425-g50-";
            string suffix = ".cs.illinois.edu";
            string machineNumber = "00";

            if (hostname.StartsWith(prefix) && hostname.EndsWith(suffix))
            {
                machineNumber = hostname.Substring(prefix.Length, 2);
            }

            return machineNumber;
        }

        private void SendHeartBeat()
        {

        }

        private void ProcessCommand(SkyNetCommand cmd)
        {

        }

        private void ProcessGrepCommand(byte[] packetData)
        {
            // TODO: Neil

            // Receive packet and run local grep
            // ?Sends back results
        }

        private List<string> SendGrepCommand(string grepExpression)
        {
            // TODO: Neil

            // Send packet to each network
            // Will be ran by DistributedGrep for each node in the network

            return null;
        }

        public LogResults DistributedGrep(string grepExp)
        {
            LogResults results;

            foreach (var netNodeInfo in skyNetNodeDictionary.Values)
            {

            }

            // TODO: Wei

            // First run local grep
			// Then, for each node in the network send a grep command and listen for response
			// Wil likely need to update SkyNetNodeInfo status if a node doesn't respond

            return null;
        }

        public void Run()
        {
            TcpListener server = new TcpListener(IPAddress.Loopback, SkyNetConfiguration.DefaultPort);
            server.Start();

            while (true)
            {
                TcpClient client = server.AcceptTcpClient();

                // Treat all packets as grep commands for now
                using (NetworkStream stream = client.GetStream())
                {
                    byte[] buffer = new byte[1024];

                    using (MemoryStream ms = new MemoryStream())
                    {
                        int numBytesRead;
                        while ((numBytesRead = stream.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            ms.Write(buffer, 0, numBytesRead);
                        }

                        ProcessGrepCommand(ms.ToArray());
                    }
                }
            }
        }

        public void RunInteractive()
        {

        }

    }
}
