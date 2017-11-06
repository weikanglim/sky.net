using System;
using System.Collections.Generic;
using System.Text;
using ProtoBuf;

namespace SkyNet20.SDFS.Requests
{
    [ProtoContract]
    class PutRequest : SdfsPayload
    {
        public PutRequest()
        {
            this.PayloadType = SdfsPayloadType.PutRequest;
        }

        [ProtoMember(1)]
        public string FileName { get; set; }
    }
}
