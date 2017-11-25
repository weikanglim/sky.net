using System;
using System.Collections.Generic;
using System.Text;
using System.Security.Cryptography;
using SkyNet20.Sava.UDF;

namespace SkyNet20.Sava.Defaults
{
    class RandomGraphPartitioner : IGraphPartitioner
    {
        public List<List<IVertex>> Partition(IEnumerable<IVertex> vertices, int workerCount)
        {
            List<List<IVertex>> results = new List<List<IVertex>>(workerCount);

            foreach (IVertex vertex in vertices)
            {
                List<IVertex> partition = results[vertex.VertexId.GetHashCode() % workerCount];

                if (partition == null)
                {
                    partition = new List<IVertex>();
                }

                partition.Add(vertex);
            }

            return results;
        }
    }
}
