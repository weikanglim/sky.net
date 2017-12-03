using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Text;

namespace SkyNet20.Sava.Communication
{
    [ProtoContract]
    class IterationPacket : SavaPayload
    {
        public IterationPacket()
        {
            this.PayloadType = SavaPayloadType.Iteration;
        }

        [ProtoMember(1)]
        public int Iteration { get; set; }
    }
}
