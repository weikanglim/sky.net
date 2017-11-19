using System;
using System.Collections.Generic;
using System.Text;
using ProtoBuf;

namespace SkyNet20.SDFS
{
    public enum SdfsPayloadType
    {
        GetRequest,
        PutRequest,
        DeleteRequest,
        ListRequest,

        ListResponse,
        DeleteResponse,
        PutResponse,
        GetResponse
    }

    public enum ErrorCode
    {
        FileNotFound,
        RequestTimedOut,
        UnexpectedError,
        None
    }

    [ProtoContract]
    public class SdfsPacketHeader
    {
        [ProtoMember(1)]
        public SdfsPayloadType PayloadType { get; set; }

        [ProtoMember(2)]
        public string MachineId { get; set; }
    }
}
