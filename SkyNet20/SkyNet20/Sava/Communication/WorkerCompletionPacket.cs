using System;
using System.Collections.Generic;
using System.Text;
using ProtoBuf;

namespace SkyNet20.Sava.Communication
{
    [ProtoContract]
    class WorkerCompletionPacket : SavaPayload
    {
        public WorkerCompletionPacket()
        {
            this.PayloadType = SavaPayloadType.WorkerCompletion;
        }

        [ProtoMember(1)]
        public int ActiveVertices { get; set; }

        [ProtoMember(2)]
        public string MachineId { get; set; }
    }
}
