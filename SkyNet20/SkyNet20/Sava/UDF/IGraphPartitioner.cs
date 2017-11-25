using System;
using System.Collections.Generic;
using System.Text;

namespace SkyNet20.Sava.UDF
{
    public interface IGraphPartitioner
    {
        List<List<IVertex>> Partition(IEnumerable<IVertex> vertices, int workerCount);
    }


    public interface IGraphPartitioner<TVertexValue, TEdgeValue, TMessageValue>
    {
        List<List<Vertex<TVertexValue, TEdgeValue, TMessageValue>>> Partition(IEnumerable<Vertex<TVertexValue, TEdgeValue, TMessageValue>> vertices, int workerCount);
    }
}
