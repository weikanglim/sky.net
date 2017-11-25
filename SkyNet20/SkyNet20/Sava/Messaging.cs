using System;
using System.Collections.Generic;
using System.Text;

namespace SkyNet20.Sava
{
    public class Messaging
    {
        public static Action<string, string, byte[]> SendMessageDelegate;

        public static void SendMessage(string sourceVertex, string destinationVertex, byte[] message)
        {
            SendMessageDelegate(sourceVertex, destinationVertex, message);
        }
    }
}
