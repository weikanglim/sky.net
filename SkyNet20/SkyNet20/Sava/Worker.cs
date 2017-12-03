using ProtoBuf;
using SkyNet20.Sava.UDF;
using SkyNet20.SDFS;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace SkyNet20.Sava
{
    public class Worker
    {
        private SkyNetNode node;
        private int currentIteration;
        private int partitionNumber;
        private int partitions;
        private Job job;
        private List<Vertex> vertices;
        private GraphInfo graphInfo;

        private Dictionary<string, List<Message>> currentMessages = new Dictionary<string, List<Message>>();
        private Dictionary<string, List<Message>> messageQueue = new Dictionary<string, List<Message>>();

        private Dictionary<string, bool> activeVertices = new Dictionary<string, bool>();
        private Dictionary<string, bool> queuedActiveVertices = new Dictionary<string, bool>();

        private List<Queue<Message>> outgoingMessages = new List<Queue<Message>>();

        private static readonly int MESSAGE_BUFFER = 1000;

        public Worker(SkyNetNode node, int partitionNumber, Job job, int partitions, GraphInfo graphInfo)
        {
            this.node = node;
            this.partitionNumber = partitionNumber;
            this.currentIteration = -1;
            this.job = job;
            this.partitions = partitions;
            this.graphInfo = graphInfo;
        }

        public void ProcessNewIteration(int newIteration)
        {
            if (newIteration != currentIteration + 1)
            {
                node.LogError("Unexpected iteration");
            }


            if (newIteration == 0)
            {
                Initialize();
            }
            else
            {
                currentMessages = messageQueue;
                messageQueue = new Dictionary<string, List<Message>>();

                foreach (var kvp in activeVertices)
                {
                    if (kvp.Value == false && queuedActiveVertices.ContainsKey(kvp.Key) && queuedActiveVertices[kvp.Key] == true)
                    {
                        // Re-activate vertices
                        activeVertices[kvp.Key] = true;
                    }
                }
                queuedActiveVertices = new Dictionary<string, bool>();
            }

            foreach (Vertex v in vertices)
            {
                v.CurrentIteration = newIteration;
                v.Compute(currentMessages[v.VertexId]);
            }

            currentIteration++;
            node.SendWorkerCompletion(activeVertices.Values.Where(x => x == true).Count<bool>());
        }


        private string PartitionFile
        {
            get
            {
                return $"{job.JobName}.{partitionNumber}";
            }
        }

        public void QueueIncomingMessage(Message m)
        {
            List<Message> messagesForVertex;

            if (!messageQueue.ContainsKey(m.VertexId))
            {
                messagesForVertex = new List<Message>();
                messageQueue.Add(m.VertexId, messagesForVertex);
            }
            else
            {
                messagesForVertex = messageQueue[m.VertexId];
            }

            messagesForVertex.Add(m);

            if (!queuedActiveVertices.ContainsKey(m.VertexId))
            {
                queuedActiveVertices.Add(m.VertexId, true);
            }
        }

        public void QueueIncomingMessages(Message[] messages)
        {
            foreach (Message m in messages)
            {
                QueueIncomingMessage(m);
            }
        }

        public void QueueOutgoingMessage(Message m, int partitionNumber)
        {
            var destinationPartition = outgoingMessages[partitionNumber];
            destinationPartition.Enqueue(m);

            if (destinationPartition.Count > MESSAGE_BUFFER)
            {
                Message[] sendMessages = new Message[destinationPartition.Count];
                destinationPartition.CopyTo(sendMessages, 0);
                node.SendVertexMessages(sendMessages, partitionNumber);
            }
        }

        public void InactivateVertex(object o, EventArgs eventArgs)
        {
            if (o is Vertex v)
            {
                activeVertices[v.VertexId] = false;
            }
        }

        public void SendMessageTo(object o, Message m)
        {
            if (o is Vertex v)
            {
                var destinationPartition = job.GraphPartitioner.PartitionNumber(m.VertexId, partitions);
                if (destinationPartition == partitionNumber)
                {
                    QueueIncomingMessage(m);
                }
                else
                {
                    QueueOutgoingMessage(m, destinationPartition);
                }
            }
        }

        public void Initialize()
        {
            if (!Storage.Exists(PartitionFile))
            {
                node.LogVerbose($"{PartitionFile} not found.");
                throw new FileNotFoundException($"{PartitionFile} not found.");
            }

            using (FileStream fs = Storage.Read(PartitionFile))
            {
                vertices = Serializer.DeserializeWithLengthPrefix<List<Vertex>>(fs, PrefixStyle.Base128);
                foreach (Vertex v in vertices)
                {
                    activeVertices.Add(v.VertexId, true);
                    v.GraphInfo = graphInfo;
                    v.jobConfig = job.Configuration;
                    v.OnVoteToHalt += InactivateVertex;
                    v.OnSendMessageTo += SendMessageTo;
                }   
            }

            for (int i = 0; i < partitions; i++)
            {
                outgoingMessages.Add(new Queue<Message>());
            }
        }
    }
}
