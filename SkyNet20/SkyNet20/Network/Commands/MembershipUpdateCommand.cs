using System;
using System.Collections.Generic;
using System.Text;
using ProtoBuf;
using System.Collections.Concurrent;

namespace SkyNet20.Network.Commands
{
    [ProtoContract]
    public class MembershipUpdateCommand
    {
        [ProtoMember(1)]
        public Dictionary<string, SkyNetNodeInfo> machineList;
    }
}
