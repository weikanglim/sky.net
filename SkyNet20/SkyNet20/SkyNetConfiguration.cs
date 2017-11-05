using System;
using System.Collections.Generic;
using System.Text;
using System.Configuration;
using SkyNet20.Configuration;
using System.Runtime.InteropServices;
using SkyNet20.Utility;
using System.IO;

namespace SkyNet20
{
    /// <summary>
    /// Manages all configuration settings for <see cref="SkyNet20"/>.
    /// </summary>
    public class SkyNetConfiguration
    {

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
        /// The default port used by <see cref="SkyNetNode"/> instances.
        /// </summary>
        public static int DefaultPort
        {
            get
            {
                return Convert.ToInt32(ConfigurationManager.AppSettings["DefaultPort"]);
            }
        }

        public static int FileTransferPort
        {
            get
            {
                return Convert.ToInt32(ConfigurationManager.AppSettings["FileTransferPort"]);
            }
        }

        public static int FileIndexTransferPort
        {
            get
            {
                return Convert.ToInt32(ConfigurationManager.AppSettings["FileIndexTransferPort"]);
            }
        }

        public static int TimeStampPort
        {
            get
            {
                return Convert.ToInt32(ConfigurationManager.AppSettings["TimeStampPort"]);
            }
        }

        /// <summary>
        /// The default port used by <see cref="SkyNetNode"/> instances
        /// </summary>
        public static int SecondaryPort
        {
            get
            {
                return Convert.ToInt32(ConfigurationManager.AppSettings["SecondaryPort"]);
            }
        }


        private static string programPath;

        /// <summary>
        /// The SkyNet installation path.
        /// </summary>
        public static string ProgramPath
        {
            get
            {
                if (String.IsNullOrEmpty(programPath))
                {
                    if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    {
                        // Use bash to get back actual path
                        programPath = CmdUtility.RunCmd("echo " + SkyNetConfiguration.ProgramPath).Output;
                        programPath = programPath.TrimEnd('\n');
                    }
                    else
                    {
                        programPath = ConfigurationManager.AppSettings["ProgramPath"];
                    }
                }

                return programPath;
            }
        }

        /// <summary>
        /// The SkyNet log path.
        /// </summary>
        public static string LogPath
        {
            get
            {
                return ProgramPath + Path.DirectorySeparatorChar + "log";
            }
        }

        /// <summary>
        /// Duration between each heartbeat, in milliseconds.
        /// </summary>
        public static int HeartbeatInterval
        {
            get
            {
                return Int32.Parse(ConfigurationManager.AppSettings["HeartbeatInterval"]);
            }
        }

        /// <summary>
        /// Number of heartbeat monitors that are predecessors.
        /// </summary>
        public static int HeartbeatPredecessors
        {
            get
            {
                return Int32.Parse(ConfigurationManager.AppSettings["HeartbeatPredecessors"]);
            }
        }

        /// <summary>
        /// Number of heartbeat monitors that are successors.
        /// </summary>
        public static int HeartbeatSuccessors
        {
            get
            {
                return Int32.Parse(ConfigurationManager.AppSettings["HeartbeatSuccessors"]);
            }
        }

        /// <summary>
        /// Number of milliseconds after which a machine is considered failed if a heartbeat has not yet been received.
        /// </summary>
        public static int HeartbeatTimeout
        {
            get
            {
                return Int32.Parse(ConfigurationManager.AppSettings["HeartbeatTimeout"]);
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


        /// <summary>
        /// Port used by distributed file system storage.
        /// </summary>
        public static int StorageFileTransferPort
        {
            get
            {
                return Int32.Parse(ConfigurationManager.AppSettings["StorageFileTransferPort"]);
            }
        }
    }
}
