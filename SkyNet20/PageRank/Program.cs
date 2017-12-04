using ProtoBuf;
using ProtoBuf.Meta;
using SkyNet20;
using SkyNet20.Sava;
using SkyNet20.Sava.UDF;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

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

            if (args.Length >= 1)
            {
                List<Vertex> results = node.SubmitSavaJob(job);

                results.Sort((v1, v2) => ((double)v2.Value.UntypedValue).CompareTo((double)v1.Value.UntypedValue));

                for (int i = 0; i < 25; i++)
                {
                    Console.WriteLine($"{results[i].VertexId}, {results[i].Value.UntypedValue}");
                }
            }
            else
            {
                node.Run();
            }
            Console.ReadLine();
            //Task.Run(() => node.Run());
            //node.RunSavaJob(job).Wait();
        }
    }
}
