using System;
using System.Collections.Generic;
using System.Text;
using System.Net;

namespace SkyNet20
{
    public enum Status
    {
        Alive,
        Dead
    };

    public class SkyNetNodeInfo
    {
        public IPAddress IPAddress{ get; set; }
        public String HostName { get; set; }
        public IPEndPoint EndPoint { get; set; }
        public Status Status { get; set; }
    }
}
