using ProtoBuf;
using SkyNet20.Sava.UDF;
using SkyNet20.SDFS;
using System;
using System.Collections.Generic;
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
        private Job job;
        private List<Vertex> vertices;

        private Dictionary<string, List<Message>> currentMessages = new Dictionary<string, List<Message>>();
        private Dictionary<string, List<Message>> messageQueue = new Dictionary<string, List<Message>>();

        public Worker(SkyNetNode node, int partitionNumber, Job job)
        {
            this.node = node;
            this.partitionNumber = partitionNumber;
            this.currentIteration = 0;
            this.job = job;
        }

        public async Task ProcessNewIteration(int newIteration)
        {
            if (newIteration == 1)
            {
                await Task.Run(() => Initialize());
            }
            else
            {
                currentMessages = messageQueue;
                messageQueue = new Dictionary<string, List<Message>>();
            }


            foreach (Vertex v in vertices)
            {
                v.Compute(currentMessages[v.VertexId]);
            }
        }


        private string PartitionFile
        {
            get
            {
                return $"{job.JobName}.{partitionNumber}";
            }
        }

        public void QueueMessage(string vertex, Message m)
        {
            List<Message> messagesForVertex;

            if (!messageQueue.ContainsKey(vertex))
            {
                messagesForVertex = new List<Message>();
                messageQueue[vertex] = messagesForVertex;
            }
            else
            {
                messagesForVertex = messageQueue[vertex];
            }

            messagesForVertex.Add(m);
        }

        public void Initialize()
        {
            if (!Storage.Exists(PartitionFile))
            {
                throw new FileNotFoundException($"{PartitionFile} not found.");
            }


            using (FileStream fs = Storage.Read(PartitionFile))
            {
                vertices = Serializer.DeserializeWithLengthPrefix<List<Vertex>>(fs, PrefixStyle.Base128);
            }
        }
    }
}
