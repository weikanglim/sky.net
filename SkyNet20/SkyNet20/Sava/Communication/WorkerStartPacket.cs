using System;
using System.Collections.Generic;
using System.Text;
using ProtoBuf;

namespace SkyNet20.Sava.Communication
{
    [ProtoContract]
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

        [ProtoMember(3)]
        public List<string> WorkerPartitions { get; set; }

        [ProtoMember(4)]
        public GraphInfo GraphInfo{ get; set; }
    }
}
