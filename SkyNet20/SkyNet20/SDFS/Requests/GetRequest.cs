using System;
using System.Collections.Generic;
using System.Text;
using ProtoBuf;

namespace SkyNet20.SDFS.Requests
{
    [ProtoContract]
    class GetRequest : SdfsPayload
    {
        public GetRequest()
        {
            this.PayloadType = SdfsPayloadType.GetRequest;
        }

        [ProtoMember(1)]
        public string FileName { get; set; }
    }
}
