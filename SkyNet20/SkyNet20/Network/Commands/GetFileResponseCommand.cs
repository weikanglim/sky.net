using System;
using System.Collections.Generic;
using System.Text;
using ProtoBuf;

namespace SkyNet20.Network.Commands
{
    public enum GetFileResponse
    {
        OK,
        DoesNotExist,
        NotUpToDate,
    }

    [ProtoContract]
    class GetFileResponseCommand
    {

        [ProtoMember(1)]
        public GetFileResponse response;

        [ProtoMember(2)]
        public byte [] content;
    }
}
