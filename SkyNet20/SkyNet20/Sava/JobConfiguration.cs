using System;
using System.Collections.Generic;
using System.Text;

namespace SkyNet20.Sava
{
    public class JobConfiguration
    {
        public Type VertexType { get; set; }
        public Type GraphReaderType { get; set; }
        public Type GraphPartitionerType { get; set; }
    }
}
