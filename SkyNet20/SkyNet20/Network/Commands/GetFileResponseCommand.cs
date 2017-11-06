using System;
using System.Collections.Generic;
using System.Text;
using ProtoBuf;

namespace SkyNet20.Network.Commands
{
    [ProtoContract]
    class GetFileResponseCommand
    {
        [ProtoMember(1)]
        public bool fileExists;

        [ProtoMember(2)]
        public byte [] content;
    }
}
