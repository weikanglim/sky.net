using System;
using System.Collections.Generic;
using System.Text;
using ProtoBuf;

namespace SkyNet20.Sava.Communication
{
    class WorkerStartPacket : SavaPayload
    {
        public WorkerStartPacket()
        {
            this.PayloadType = SavaPayloadType.WorkerStart;
        }

        [ProtoMember(1)]
        public Job job{ get; set; }

        [ProtoMember(2)]
        public int partition { get; set; }
    }
}
