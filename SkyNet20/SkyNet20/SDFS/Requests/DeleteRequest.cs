using System;
using System.Collections.Generic;
using System.Text;

namespace SkyNet20.SDFS.Requests
{
    class DeleteRequest : SdfsPayload
    {
        public DeleteRequest()
        {
            this.PayloadType = SdfsPayloadType.DeleteRequest;
        }
    }
}
