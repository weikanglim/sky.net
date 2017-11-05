using System;
using System.Collections.Generic;
using System.Text;
using ProtoBuf;
using System.Collections.Concurrent;

namespace SkyNet20.Network.Commands
{
    [ProtoContract]
    public class IndexFileCommand
    {
        [ProtoMember(1)]
        public Dictionary<string, Tuple<List<string>, DateTime?, DateTime>> indexFile;
    }
}
