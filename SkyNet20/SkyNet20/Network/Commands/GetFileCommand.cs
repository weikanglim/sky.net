using System;
using System.Collections.Generic;
using System.Text;
using ProtoBuf;

namespace SkyNet20.Network.Commands
{
    [ProtoContract]
    public class GetFileCommand
    {
        [ProtoMember(1)]
        public string filename;
    }
}
