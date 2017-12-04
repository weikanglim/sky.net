using ProtoBuf;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace SkyNet20.Sava.Communication
{
    class SavaPacket<T> where T : SavaPayload
    {
        public SavaPacketHeader Header { get; set; }
        public T Payload { get; set; }

        public byte[] ToBytes()
        {
            byte[] packet;

            using (MemoryStream stream = new MemoryStream())
            {
                Header.PayloadType = Payload.PayloadType;

                Serializer.SerializeWithLengthPrefix(stream, Header, PrefixStyle.Base128);
                Serializer.SerializeWithLengthPrefix<T>(stream, Payload, PrefixStyle.Base128);

                packet = stream.ToArray();
            }

            return packet;
        }
    }
}
