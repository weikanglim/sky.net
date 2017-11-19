using System;
using System.Collections.Generic;
using System.Text;
using ProtoBuf;

namespace SkyNet20.SDFS.Requests
{
    [ProtoContract]
    class PutRequestFile
    {
        [ProtoMember(1)]
        public byte[] payload { get; set; }
    }
}
