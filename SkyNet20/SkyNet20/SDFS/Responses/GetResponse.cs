using System;
using System.Collections.Generic;
using System.Text;
using ProtoBuf;

namespace SkyNet20.SDFS.Responses
{
    [ProtoContract]
    class GetResponse : SdfsPayload
    {
        public GetResponse()
        {
            this.PayloadType = SdfsPayloadType.GetResponse;
        }

        [ProtoMember(1)]
        public bool getSuccessful;

        [ProtoMember(2)]
        public ErrorCode errorCode;

        [ProtoMember(3)]
        public byte[] content;
    }
}
