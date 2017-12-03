using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using ProtoBuf;

namespace SkyNet20
{
    public enum Status
    {
        Alive,
        Failed,
    };

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
            this.Status = Status.Alive;
            this.LeaveFailSdfsProcessed = false;
        }

        public SkyNetNodeInfo()
        {
        }

        public String HostName { get; set; }
        [ProtoMember(1)]
        public string MachineId { get; set; }
        [ProtoMember(2)]
        public long LastHeartbeat { get; set; }
        [ProtoMember(3)]
        public Status Status { get; set; }
        [ProtoMember(4)]
        public long HeartbeatCounter { get; set; }
        [ProtoMember(5)]
        public bool IsMaster { get; set; }
        [ProtoMember(6)]
        public bool LeaveFailSdfsProcessed { get; set; }

        public IPAddress IPAddress { get; set; }

        public IPEndPoint DefaultEndPoint
        {
            get
            {
                return new IPEndPoint(this.IPAddress, SkyNetConfiguration.DefaultPort);
            }
        }

        public IPEndPoint TimeStampEndPoint
        {
            get
            {
                return new IPEndPoint(this.IPAddress, SkyNetConfiguration.TimeStampPort);
            }
        }

        public IPEndPoint NodeToNodeEndPoint
        {
            get
            {
                return new IPEndPoint(this.IPAddress, SkyNetConfiguration.NodeToNodePort);
            }
        }

        public IPEndPoint FileTransferRequestEndPoint
        {
            get
            {
                return new IPEndPoint(this.IPAddress, SkyNetConfiguration.FileTransferPort);
            }
        }

        public IPEndPoint FileIndexTransferRequestEndPoint
        {
            get
            {
                return new IPEndPoint(this.IPAddress, SkyNetConfiguration.FileIndexTransferPort);
            }
        }

        public IPEndPoint StorageFileTransferEndPoint
        {
            get
            {
                return new IPEndPoint(this.IPAddress, SkyNetConfiguration.StorageFileTransferPort);
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
