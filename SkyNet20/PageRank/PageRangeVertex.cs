using SkyNet20.Sava.UDF;
using System;
using System.Collections.Generic;
using System.Text;

namespace PageRank
{
    class PageRangeVertex : Vertex
    {
        public override void Compute(List<Message> messages)
        {
            if (this.CurrentIteration == 0)
            {
                Value = Primitive.Create<double>((double)1.0 / GraphInfo.NumberOfVertices);
            }
            else  if (this.CurrentIteration >= 1)
            {
                double sum = 0;

                foreach (Message m in messages)
                {
                    sum += (double)m.Value.UntypedValue;

                    Value.UntypedValue = 0.15 / GraphInfo.NumberOfVertices + 0.85 * sum;
                }
            }

            if (this.CurrentIteration < 20)
            {
                int edgeCount = this.OutEdges.Count;

                foreach (Edge e in this.OutEdges)
                {
                    Message outgoingMessage = new Message
                    {
                        VertexId = e.ToVertex,
                        Value = Primitive.Create<double>((double)Value.UntypedValue / edgeCount),
                    };

                    SendMessageTo(outgoingMessage);
                }
            }
            else
            {
                VoteToHalt();
            }
        }
    }
}
