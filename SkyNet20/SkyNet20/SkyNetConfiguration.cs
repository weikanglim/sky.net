using System;
using System.Collections.Generic;
using System.Text;
using System.Configuration;
using SkyNet20.Configuration;

namespace SkyNet20
{
    /// <summary>
    /// Manages all configuration settings for <see cref="SkyNet20"/>.
    /// </summary>
    public class SkyNetConfiguration
    {
        public static long HeartBeatInterval {
            get
            {
                return Convert.ToInt64(ConfigurationManager.AppSettings["HeartbeatInterval"]);
            }
        }

        /// <summary>
        /// Collection of machines in the connected SkyNet network.
        /// </summary>
        public static Dictionary<string, SkyNetMachine> Machines
        {
            get
            {
                var config = ConfigurationManager.GetSection("machines") as SkyNetConfig;
                Dictionary<string, SkyNetMachine> result = new Dictionary<string, SkyNetMachine>();

                foreach (SkyNetMachine machine in config.Instances)
                {
                    result.Add(machine.HostName, machine);
                }

                return result;
            }
        }

        /// <summary>
        /// List of hostnames of machines in the connected SkyNet network.
        /// </summary>
        public static List<String> HostNames
        {
            get
            {
                var config = ConfigurationManager.GetSection("machines") as SkyNetConfig;
                List<String> result = new List<String>();

                foreach (SkyNetMachine machine in config.Instances)
                {
                    result.Add(machine.HostName);
                }

                return result;
            }
        }

        /// <summary>
        /// The default listening server port for <see cref="SkyNetNode"/> instances.
        /// </summary>
        public static int DefaultPort
        {
            get
            {
                return Convert.ToInt32(ConfigurationManager.AppSettings["DefaultPort"]);
            }
        }

        /// <summary>
        /// The SkyNet installation path.
        /// </summary>
        public static string ProgramPath
        {
            get
            {
                return ConfigurationManager.AppSettings["ProgramPath"];
            }
        }

        /// <summary>
        /// The SkyNet log path.
        /// </summary>
        public static string LogPath
        {
            get
            {
                return ConfigurationManager.AppSettings["LogPath"];
            }
        }

        /// <summary>
        /// Duration of a gossip round, in milliseconds.
        /// </summary>
        public static int GossipRoundInterval
        {
            get
            {
                return Int32.Parse(ConfigurationManager.AppSettings["GossipRoundInterval"]);
            }
        }

        /// <summary>
        /// Number of targets per gossip round.
        /// </summary>
        public static int GossipRoundTargets
        {
            get
            {
                return Int32.Parse(ConfigurationManager.AppSettings["GossipRoundTargets"]);
            }
        }
    }
}
