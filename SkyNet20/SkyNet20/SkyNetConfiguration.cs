using System;
using System.Collections.Generic;
using System.Text;
using System.Configuration;
using SkyNet20.Configuration;

namespace SkyNet20
{
    class SkyNetConfiguration
    {
        public static long HeartBeatInterval {
            get
            {
                return Convert.ToInt64(ConfigurationManager.AppSettings["HeartbeatInterval"]);
            }
        }

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

        public static int DefaultPort
        {
            get
            {
                return Convert.ToInt32(ConfigurationManager.AppSettings["DefaultPort"]);
            }
        }

        public static string ProgramPath
        {
            get
            {
                return ConfigurationManager.AppSettings["LogPath"];
            }
        }

        public static string LogPath
        {
            get
            {
                return ConfigurationManager.AppSettings["ProgramPath"];
            }
        }
    }
}
