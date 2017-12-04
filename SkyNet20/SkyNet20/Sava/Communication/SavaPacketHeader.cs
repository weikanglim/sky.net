using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Text;

namespace SkyNet20.Sava.Communication
{
    public enum SavaPayloadType
    {
        JobRequest,
        JobRestart,

        Iteration,
        WorkerStart,
        WorkerCompletion,
        VertexMessage,

        ResultsRequest,
        ResultsResponse,
    }

    [ProtoContract]
    public class SavaPacketHeader
    {
        [ProtoMember(1)]
        public SavaPayloadType PayloadType { get; set; }

        [ProtoMember(2)]
        public string MachineId { get; set; }
    }
}
