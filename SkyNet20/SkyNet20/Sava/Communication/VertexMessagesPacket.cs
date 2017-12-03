using System;
using System.Collections.Generic;
using System.Text;
using ProtoBuf;
using SkyNet20.Sava.UDF;

namespace SkyNet20.Sava.Communication
{
    [ProtoContract]
    class VertexMessagesPacket : SavaPayload
    {
        public VertexMessagesPacket()
        {
            PayloadType = SavaPayloadType.VertexMessage;
        }

        [ProtoMember(1)]
        public Message[] messages;
    }
}
