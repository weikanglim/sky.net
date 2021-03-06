﻿using System;
using System.Collections.Generic;
using System.Text;
using ProtoBuf;

namespace SkyNet20.Sava.Communication
{
    [ProtoContract]
    class JobProcessingRequest : SavaPayload
    {
        public JobProcessingRequest()
        {
            this.PayloadType = SavaPayloadType.JobRequest;
        }

        [ProtoMember(1)]
        public Job requestJob;
    }
}
