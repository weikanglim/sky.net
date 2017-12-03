using System;
using System.Collections.Generic;
using System.Text;

namespace SkyNet20.Sava.UDF
{
    public interface IGraphPartitioner
    {
        List<List<Vertex>> Partition(IEnumerable<Vertex> vertices, int workerCount);
    }

}
