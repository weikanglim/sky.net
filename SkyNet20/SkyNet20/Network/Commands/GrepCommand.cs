using System;
using System.Collections.Generic;
using System.Text;
using ProtoBuf;

namespace SkyNet20.Network.Commands
{
    [ProtoContract]
    public class GrepCommand
    {
        [ProtoMember(1, IsRequired = true)]
        public string Query { get; set; }
    }
}
