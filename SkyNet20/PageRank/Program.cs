using ProtoBuf;
using ProtoBuf.Meta;
using SkyNet20;
using SkyNet20.Sava;
using SkyNet20.Sava.UDF;
using System;

namespace PageRank
{
    class Program
    {
        static void Main(string[] args)
        {
            RuntimeTypeModel.Default[typeof(Vertex)].AddSubType(400, typeof(PageRangeVertex));
            
            Job job = new Job
            {
                JobName = "PageRank",
                InputFile = "graph.txt",
                Configuration = new JobConfiguration
                {
                    VertexType = typeof(PageRangeVertex),
                    GraphReaderType = typeof(SimpleGraphReader),
                },
            };
            Configuration.JobConfiguration.Add(job);

            SkyNetNode node = new SkyNetNode();
            //node.Run();
            node.RunSavaJob(job);
        }
    }
}
