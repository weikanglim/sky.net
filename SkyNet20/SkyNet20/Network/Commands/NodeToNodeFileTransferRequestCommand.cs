using System;
using System.Collections.Generic;
using System.Text;
using ProtoBuf;
using System.Collections.Concurrent;

namespace SkyNet20.Network.Commands
{
    [ProtoContract]
    public class NodeToNodeFileTransferRequestCommand
    {
        [ProtoMember(1)]
        public string filename;

        [ProtoMember(2)]
        public byte[] content;
    }
}
