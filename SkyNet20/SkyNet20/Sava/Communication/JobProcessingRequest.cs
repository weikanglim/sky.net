using System;
using System.Collections.Generic;
using System.Text;
using ProtoBuf;

namespace SkyNet20.Sava.Communication
{
    [ProtoContract]
    public class JobProcessingRequest
    {
        [ProtoMember(1)]
        public Job requestJob;
    }
}
