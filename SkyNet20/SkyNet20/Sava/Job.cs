using SkyNet20.Sava.UDF;
using SkyNet20.Sava.Defaults;

using System;
using System.Collections.Generic;
using System.Text;

namespace SkyNet20.Sava
{
    public class Job
    {

        public string JobName { get; set; }
        public string InputFile { get; set; }
        public JobConfiguration Configuration { get; set; }

        public Vertex Vertex
        {
            get
            {
                return (Vertex) Activator.CreateInstance(Configuration.VertexType);
            }
        }

        public IGraphReader GraphReader
        {
            get
            {
                return (IGraphReader)Activator.CreateInstance(Configuration.GraphReaderType);
            }
        }

        public IGraphPartitioner GraphPartitioner
        {
            get
            {
                if (Configuration.GraphPartitionerType == null)
                {
                    return new RandomGraphPartitioner();
                }

                return (IGraphPartitioner)Activator.CreateInstance(Configuration.GraphPartitionerType);
            }
        }
    }
}
