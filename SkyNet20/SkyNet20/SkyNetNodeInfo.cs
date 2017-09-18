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

    /// <summary>
    /// Stores information about a <see cref="SkyNetNode"/>.
    /// </summary>
    public class SkyNetNodeInfo
    {
        public IPAddress IPAddress{ get; set; }
        public String HostName { get; set; }
        public IPEndPoint EndPoint { get; set; }
        public Status Status { get; set; }
    }
}
