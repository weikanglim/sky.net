using System;
using System.Collections.Generic;
using System.Text;
using ProtoBuf;

namespace SkyNet20.SDFS.Responses
{
    [ProtoContract]
    class PutResponse : SdfsPayload
    {
        public PutResponse()
        {
            this.PayloadType = SdfsPayloadType.PutResponse;
        }

        [ProtoMember(1)]
        public bool putSuccessful;

        [ProtoMember(2)]
        public bool putConfirmationRequired;

        [ProtoMember(3)]
        public ErrorCode errorCode;

    }
}
