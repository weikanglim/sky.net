using System;
using System.Collections.Generic;
using System.Text;

namespace SkyNet20.SDFS.Requests
{
    class ListRequest : SdfsPayload
    {
        public ListRequest()
        {
            this.PayloadType = SdfsPayloadType.ListRequest;
        }
    }
}
