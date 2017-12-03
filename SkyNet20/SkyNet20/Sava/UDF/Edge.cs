using System;
using System.Collections.Generic;
using System.Text;
using ProtoBuf;

namespace SkyNet20.Sava.UDF
{
    [ProtoContract]
    public class Edge
    {
        [ProtoMember(1)]
        public string ToVertex { get; set; }

        [ProtoMember(2)]
        public Primitive Value { get; set; }
    }
}
