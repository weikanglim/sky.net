using System;
using System.Collections.Generic;
using System.Text;

namespace SkyNet20.Sava
{
    public static class Configuration
    {
        private static Dictionary<string, JobConfiguration> jobs = new Dictionary<string, JobConfiguration>();


        /// <summary>
        /// Jobs and their associated configurations registered.
        /// </summary>
        public static Dictionary<string, JobConfiguration> Jobs
        {
            get
            {
                return jobs;
            }
        }
    }
}
