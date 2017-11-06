using System;
using System.Collections.Generic;
using System.Text;
using ProtoBuf;

namespace SkyNet20.SDFS.Requests
{
    [ProtoContract]
    class ListRequest : SdfsPayload
    {
        public ListRequest()
        {
            this.PayloadType = SdfsPayloadType.ListRequest;
        }

        [ProtoMember(1)]
        public string FileName { get; set; }
    }
}
