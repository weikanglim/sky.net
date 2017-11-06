using ProtoBuf;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using SkyNet20.SDFS.Requests;

namespace SkyNet20.SDFS
{
    class SdfsPacket<T> where T : SdfsPayload
    {
        public SdfsPacketHeader Header { get; set; }
        public T Payload { get; set; }

        public byte[] ToBytes()
        {
            byte[] packet;

            using (MemoryStream stream = new MemoryStream())
            {
                Header.PayloadType = Payload.PayloadType;

                Serializer.SerializeWithLengthPrefix(stream, Header, PrefixStyle.Base128);
                Serializer.SerializeWithLengthPrefix(stream, Payload, PrefixStyle.Base128);

                packet = stream.ToArray();
            }

            return packet;
        }

        //public static SDFSPacket<T> FromBytes(byte [] packet)
        //{
        //    using (MemoryStream stream = new MemoryStream(packet))
        //    {
        //        SdfsPacketHeader header = Serializer.DeserializeWithLengthPrefix<SdfsPacketHeader>(stream, PrefixStyle.Base128);

        //        switch (header.PayloadType)
        //        {
        //            case 
        //        }

        //        packet = stream.ToArray();
        //    }

        //    return packet;
        //}
    }
}
