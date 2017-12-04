using System;
using System.Collections.Generic;
using System.Text;
using System.Security.Cryptography;
using SkyNet20.Sava.UDF;

namespace SkyNet20.Sava.Defaults
{
    class RandomGraphPartitioner : IGraphPartitioner
    {
        public List<List<Vertex>> Partition(IEnumerable<Vertex> vertices, int workerCount)
        {
            List<List<Vertex>> results = new List<List<Vertex>>(workerCount);

            for (int i = 0; i < workerCount; i++)
            {
                results.Add(new List<Vertex>());
            }


            foreach (Vertex vertex in vertices)
            {
                int index = PartitionNumber(vertex.VertexId, workerCount);
                List<Vertex> partition = results[index];

                partition.Add(vertex);
            }

            return results;
        }

        public int PartitionNumber(string vertexId, int workerCount)
        {
            return Math.Abs(vertexId.GetHashCode()) % workerCount;
        }
    }
}
