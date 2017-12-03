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

            foreach (Vertex vertex in vertices)
            {
                List<Vertex> partition = results[PartitionNumber(vertex.VertexId, workerCount)];

                if (partition == null)
                {
                    partition = new List<Vertex>();
                }

                partition.Add(vertex);
            }

            return results;
        }

        public int PartitionNumber(string vertexId, int workerCount)
        {
            return vertexId.GetHashCode() % workerCount;
        }
    }
}
