using System;
using System.Collections.Generic;
using System.Text;
using ProtoBuf;

namespace SkyNet20.Sava.UDF
{
    [ProtoContract]
    public class Message
    {
        [ProtoMember(1)]
        public Primitive Value { get; set; }
    }
}
