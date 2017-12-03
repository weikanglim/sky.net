using System;
using System.Collections.Generic;
using System.Text;
using ProtoBuf;

namespace SkyNet20.Sava
{
    [ProtoContract]
    public class GraphInfo
    {
        [ProtoMember(1)]
        public int NumberOfVertices { get; set; }
    }
}
