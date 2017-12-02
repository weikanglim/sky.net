using SkyNet20;
using SkyNet20.Sava;
using SkyNet20.Sava.UDF;
using System;
using ProtoBuf;
using ProtoBuf.Meta;
using System.IO;

namespace ShortestPath
{
    class Program
    {
        static void Main(string[] args)
        {
            //RuntimeTypeModel.Default.Add(typeof(VertexValue), false).SetSurrogate(typeof(VertexValueSurrogate));
            RuntimeTypeModel.Default[typeof(Vertex)].AddSubType(455, typeof(ShortestPathVertex));
            ShortestPathVertex v = new ShortestPathVertex()
            {
                VertexId = "123",
                Value = Primitive.Create<int>(456),
                Step = 5,
            };

            byte[] result;
            using (MemoryStream ms = new MemoryStream())
            {
                Serializer.SerializeWithLengthPrefix<ShortestPathVertex>(ms, v, PrefixStyle.Base128);
                result = ms.ToArray();
            }

            using (MemoryStream ms = new MemoryStream(result))
            {
                Vertex vOut = Serializer.DeserializeWithLengthPrefix<ShortestPathVertex>(ms, PrefixStyle.Base128);
            }

            //JobConfiguration jobConfiguration = new JobConfiguration();
            //jobConfiguration.VertexType = typeof(ShortestPathVertex);
            //Job job = new Job
            //{
            //    JobName = "ShortestPath",
            //    JobConfiguration = jobConfiguration,
            //};
            //Configuration.JobConfiguration.Add(job);
            //Configuration.QueueJob(job);

            //SkyNetNode node = new SkyNetNode();
            //node.Run();
        }
    }
}
