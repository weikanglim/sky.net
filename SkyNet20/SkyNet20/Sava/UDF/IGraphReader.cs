using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace SkyNet20.Sava.UDF
{
    public interface IGraphReader
    {
        List<Vertex> ReadFile(FileStream stream);
    }
}
