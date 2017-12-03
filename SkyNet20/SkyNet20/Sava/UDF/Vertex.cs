using System;
using System.Collections.Generic;
using System.Text;
using ProtoBuf;
using System.IO;

namespace SkyNet20.Sava.UDF
{

    public class SendMessageArgs
    {
        public string destinationVertex;
        public Message message;
    }

    [ProtoContract]
    public abstract class Vertex
    {
        [ProtoMember(1)]
        public string VertexId { get; set;  }
        [ProtoMember(2)]
        public Primitive Value { get; set; }
        [ProtoMember(3)]
        public List<Edge> OutEdges { get; set; }
        public GraphInfo GraphInfo { get; set; }
        public JobConfiguration jobConfig { get; set; }

        public event EventHandler OnVoteToHalt;
        public event EventHandler<Message> OnSendMessageTo;
        public int CurrentIteration { get; set; }

        public abstract void Compute(List<Message> messages);

        public void SendMessageTo(Message message)
        {
            OnSendMessageTo(this, message);
        }

        public void VoteToHalt()
        {
            OnVoteToHalt(this, EventArgs.Empty);
        }
    }
}
