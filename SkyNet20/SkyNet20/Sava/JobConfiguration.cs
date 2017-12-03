using System;
using System.Collections.Generic;
using System.Text;
using ProtoBuf;

namespace SkyNet20.Sava
{
    [ProtoContract]
    public class JobConfiguration
    {
        [ProtoMember(1)]
        public Type VertexType { get; set; }
        [ProtoMember(2)]
        public Type GraphReaderType { get; set; }
        [ProtoMember(3)]
        public Type GraphPartitionerType { get; set; }
        [ProtoMember(4)]
        public Dictionary<string, object> CustomValue { get; set; } = new Dictionary<string, object>();
    }
}
