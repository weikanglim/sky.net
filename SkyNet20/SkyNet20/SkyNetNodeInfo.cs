using System;
using System.Collections.Generic;
using System.Text;
using System.Net;

namespace SkyNet20
{
    public enum Status
    {
        Alive,
        Dead
    };

    /// <summary>
    /// Stores information about a <see cref="SkyNetNode"/>.
    /// </summary>
    public class SkyNetNodeInfo
    {
        public IPAddress IPAddress{ get; set; }
        public String HostName { get; set; }
        public IPEndPoint EndPoint { get; set; }
        public Status Status { get; set; }

        public static string GetMachineId(IPAddress address)
        {
            return $"{address.ToString()};{DateTime.UtcNow}";
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
