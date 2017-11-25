using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace SkyNet20.Sava.UDF
{
    public interface IGraphReader<TVertexValue, TEdgeValue, TMessageValue>
    {
        List<Vertex<TVertexValue, TEdgeValue, TMessageValue>> ReadFile(FileStream stream);
    }
}
