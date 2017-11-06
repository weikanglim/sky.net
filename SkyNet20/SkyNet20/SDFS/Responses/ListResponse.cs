using System;
using System.Collections.Generic;
using System.Text;
using ProtoBuf;

namespace SkyNet20.SDFS.Responses
{
    [ProtoContract]
    class ListResponse : SdfsPayload
    {
        public ListResponse()
        {
            this.PayloadType = SdfsPayloadType.ListResponse;
        }

        [ProtoMember(1)]
        public List<string> machines;

        [ProtoMember(2)]
        public ErrorCode errorCode;

    }
}
