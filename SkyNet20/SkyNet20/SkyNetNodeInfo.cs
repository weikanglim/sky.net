using System;
using System.Collections.Generic;
using System.Text;
using System.Net;

namespace SkyNet20
{
    class SkyNetNodeInfo
    {
        private IPAddress address;
        private String hostName;


        public IPAddress IPAddress{ get; set; }
        public String HostName { get; set; }
    }
}
