﻿using System;
using System.Collections.Generic;
using System.Text;
using ProtoBuf;
using System.Collections.Concurrent;

namespace SkyNet20.Network.Commands
{
    [ProtoContract]
    public class FileTransferRequestCommand
    {
        [ProtoMember(1)]
        public string fromMachineId;

        [ProtoMember(2)]
        public string toMachineId;

        [ProtoMember(3)]
        public string filename;
    }
}
