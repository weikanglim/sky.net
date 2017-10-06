using System;
using System.Collections.Generic;
using System.Text;
using ProtoBuf;

namespace SkyNet20.Network
{
    public enum PayloadType
    {
        Heartbeat,
        Grep,
        MembershipUpdate,
        MembershipJoin,
        MembershipLeave,
    }

    [ProtoContract]
    public class SkyNetPacketHeader
    {
        [ProtoMember(1)]
        public PayloadType PayloadType { get; set; }

        [ProtoMember(2)]
        public string MachineId { get; set; }
    }
}
