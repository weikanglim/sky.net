using System;
using System.Collections.Generic;
using System.Text;

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
            // TODO: Wei

            // First run local grep
			// Then, for each node in the network send a grep command and listen for response
			// Wil likely need to update SkyNetNodeInfo status if a node doesn't respond

            return null;
        }

        public void Run()
        {
            // TODO: Wei

            // Runs the main-loop of the node, that will listen and respond to commands
			// Wait for incoming packet, and then call ProcessGrepCommand

        }

        public void RunInteractive()
        {

        }

    }
}
