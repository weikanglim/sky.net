using System;
using System.Collections.Generic;
using System.Text;
using ProtoBuf;

namespace SkyNet20.SDFS.Responses
{

    [ProtoContract]
    class DeleteResponse : SdfsPayload
    {
        public DeleteResponse()
        {
            this.PayloadType = SdfsPayloadType.DeleteResponse;
        }

        [ProtoMember(1)]
        public bool deleteSuccessful;

        [ProtoMember(2)]
        public ErrorCode errorCode;
    }
}
