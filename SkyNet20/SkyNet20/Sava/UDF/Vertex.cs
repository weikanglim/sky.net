using System;
using System.Collections.Generic;
using System.Text;
using ProtoBuf;
using System.IO;

namespace SkyNet20.Sava.UDF
{
    [ProtoContract]
    public abstract class Vertex<TVertexValue, TEdgeValue, TMessageValue> : IVertex
    {
        [ProtoMember(1)]
        public string VertexId { get; }
        [ProtoMember(2)]
        public TVertexValue Value { get; set; }
        [ProtoMember(3)]
        public List<Tuple<string, TEdgeValue>> OutEdges { get; }
        public long Step { get; }

        // Delegates
        public delegate void SendMessageDelegate(string destinationVertex, TMessageValue message);

        public abstract void Compute(List<TMessageValue> messages);

        public void SendMessageTo(string destinationVertex, TMessageValue message)
        {
            byte[] serializedMessage;
            using (MemoryStream ms = new MemoryStream())
            {
                Serializer.Serialize<TMessageValue>(ms, message);
                serializedMessage = ms.GetBuffer();
            }

            Messaging.SendMessage(this.VertexId, destinationVertex, serializedMessage);
        }

        // !TODO: Implement
        public abstract void VoteToHalt();
        
    }
}
