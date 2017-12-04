using SkyNet20.Sava.UDF;
using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Linq;

namespace PageRank
{
    class SimpleGraphReader : IGraphReader
    {
        public List<Vertex> ReadFile(FileStream stream)
        {
            Dictionary<string, Vertex> vertices = new Dictionary<string, Vertex>();

            using (StreamReader reader = new StreamReader(stream))
            {
                while (reader.Peek() > 0)
                {
                    string line = reader.ReadLine();

                    if (line.StartsWith("#"))
                    {
                        continue;
                    }

                    var tuple = line.Split("\t");

                    string fromId = tuple[0];
                    string toId = tuple[1];
                    List<Edge> edges;

                    if (!vertices.ContainsKey(fromId))
                    {
                        PageRangeVertex newVertex = new PageRangeVertex
                        {
                            VertexId = fromId,
                            OutEdges = new List<Edge>(),
                        };

                        vertices.Add(newVertex.VertexId, newVertex);
                        edges = newVertex.OutEdges;
                    }
                    else
                    {
                        edges = vertices[fromId].OutEdges;
                    }

                    edges.Add(new Edge
                    {
                        ToVertex = toId,
                    });
                }
            }

            return vertices.Values.ToList<Vertex>();
        }
    }
}
