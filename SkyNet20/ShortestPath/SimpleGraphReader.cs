using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using SkyNet20.Sava.UDF;

namespace ShortestPath
{
    class SimpleGraphReader : IGraphReader
    {
        public List<Vertex> ReadFile(FileStream stream)
        {
            List<Vertex> vertices = new List<Vertex>();

            using (StreamReader reader = new StreamReader(stream))
            {
                string line = reader.ReadLine();
                var variables = line.Split(" ");

                ShortestPathVertex vertex = new ShortestPathVertex()
                {
                    VertexId = variables[0],
                };

                vertices.Add(vertex);
            }

            return vertices;
        }
    }
}
