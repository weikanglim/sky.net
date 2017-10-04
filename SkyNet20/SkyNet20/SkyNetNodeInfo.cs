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
        public SkyNetNodeInfo(string hostName, string machineId, IPEndPoint endPoint)
        {
            this.EndPoint = endPoint;
            this.IPAddress = endPoint.Address;
            this.HostName = hostName;
            this.MachineId = machineId;
        }

        public IPAddress IPAddress{ get; private set; }
        public String HostName { get; private set; }
        public IPEndPoint EndPoint { get; private set; }
        [ProtoMember(1)]
        public string MachineId { get; private set; }
        [ProtoMember(2)]
        public long LastHeartbeat { get; set; }


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
