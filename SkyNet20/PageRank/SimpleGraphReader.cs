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

                    addEdge(vertices, fromId, toId);
                    addEdge(vertices, toId, fromId);
                }

                return vertices.Values.ToList<Vertex>();
            }
        }

        private void addEdge(Dictionary<string, Vertex> vertices, string vertexFrom, string vertexTo)
        {
            List<Edge> edges;

            if (!vertices.ContainsKey(vertexFrom))
            {
                PageRangeVertex newVertex = new PageRangeVertex
                {
                    VertexId = vertexFrom,
                    OutEdges = new List<Edge>(),
                };

                vertices.Add(newVertex.VertexId, newVertex);
                edges = newVertex.OutEdges;
            }
            else
            {
                edges = vertices[vertexFrom].OutEdges;
            }

            edges.Add(new Edge
            {
                ToVertex = vertexTo,
            });
        }
    }
}
