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
        /// A flag to indicate whether the current instance is running as a coordinator.
        /// </summary>
        public static bool IsCoordinator
        {
            get
            {
                return Boolean.Parse(ConfigurationManager.AppSettings["IsCoordinator"]);
            }
        }

        /// <summary>
        /// Duration of a gossip round, in milliseconds.
        /// </summary>
        public static long GossipRoundInterval
        {
            get
            {
                return Int64.Parse(ConfigurationManager.AppSettings["GossipRoundInterval"]);
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
