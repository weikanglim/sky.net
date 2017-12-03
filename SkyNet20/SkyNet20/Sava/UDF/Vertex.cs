using System;
using System.Collections.Generic;
using System.Text;
using ProtoBuf;
using System.IO;

namespace SkyNet20.Sava.UDF
{

    [ProtoContract]
    public abstract class Vertex
    {
        [ProtoMember(1)]
        public string VertexId { get; set;  }
        [ProtoMember(2)]
        public Primitive Value { get; set; }
        [ProtoMember(3)]
        public List<Edge> OutEdges { get; set; }

        public abstract void Compute(List<Message> messages);

        public void SendMessageTo(string destinationVertex, Message message)
        {
        }

        public delegate void VoteToHalt();
    }
}
