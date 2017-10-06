using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using ProtoBuf;

namespace SkyNet20
{
    /// <summary>
    /// Stores information about a <see cref="SkyNetNode"/>.
    /// </summary>
    [ProtoContract]
    public class SkyNetNodeInfo
    {
        public SkyNetNodeInfo(string hostName, string machineId)
        {
            this.HostName = hostName;
            this.MachineId = machineId;
            this.IPAddress = SkyNetNodeInfo.ParseMachineId(this.MachineId).Item1;
        }

        public SkyNetNodeInfo()
        {
        }

        public String HostName { get; set; }
        [ProtoMember(1)]
        public string MachineId { get; set; }
        [ProtoMember(2)]
        public long LastHeartbeat { get; set; }
        public IPAddress IPAddress { get; set; }

        public IPEndPoint DefaultEndPoint
        {
            get
            {
                return new IPEndPoint(this.IPAddress, SkyNetConfiguration.DefaultPort);
            }
        }


        public static string GetMachineId(IPAddress address)
        {
            return $"{address.ToString()};{DateTime.UtcNow.ToString("o")}";
        }


        public static Tuple<IPAddress, DateTime> ParseMachineId(string machineId)
        {
            string[] segments = machineId.Split(";");

            if (segments.Length != 2)
            {
                throw new ArgumentException($"{machineId} is not a valid machine ID.");
            }

            return new Tuple<IPAddress, DateTime>(IPAddress.Parse(segments[0]), DateTime.Parse(segments[1]));
        }
    }
}
