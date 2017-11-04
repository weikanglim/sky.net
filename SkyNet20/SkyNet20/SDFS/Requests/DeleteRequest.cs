using System;
using System.Collections.Generic;
using System.Text;
using ProtoBuf;

namespace SkyNet20.SDFS.Requests
{
    [ProtoContract]
    class DeleteRequest : SdfsPayload
    {
        public DeleteRequest()
        {
            this.PayloadType = SdfsPayloadType.DeleteRequest;
        }

        [ProtoMember(1)]
        public string FileName { get; set; }
    }
}
