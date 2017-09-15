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
        // TODO: Should the key be a string or a class called host
        private Dictionary<string, SkyNetNodeInfo> skyNetNodeDictionary = new Dictionary<string, SkyNetNodeInfo>();
        private long heartBeatInterval = -1;

        public SkyNetNode(SkyNetConfiguration configuration)
        {
            heartBeatInterval = configuration.HeartBeatInterval;

            foreach (var hostName in configuration.HostNames)
            {
                IPAddress address = Dns.GetHostAddresses(hostName)[0];

                SkyNetNodeInfo nodeInfo = new SkyNetNodeInfo
                {
                    IPAddress = address,
                    HostName = hostName
                };

                skyNetNodeDictionary.Add(hostName, nodeInfo);
            }
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
