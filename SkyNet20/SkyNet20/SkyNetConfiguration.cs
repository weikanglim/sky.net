using System;
using System.Collections.Generic;
using System.Text;

namespace SkyNet20
{
    class SkyNetConfiguration
    {
        // TODO: should there be a class for host
        private List<string> hostNames = null;

        public SkyNetConfiguration(long heartBeatInterval)
        {
            this.HeartBeatInterval = heartBeatInterval;
        }

        public long HeartBeatInterval { get; private set; }
    }
}
