using System;
using System.Collections.Generic;
using System.Text;

namespace SkyNet20.Sava
{
    public static class Configuration
    {
        private static List<Job> jobConfiguration = new List<Job>();
        private static Queue<Job> jobQueue = new Queue<Job>();

        /// <summary>
        /// Jobs and their associated configurations registered.
        /// </summary>
        public static List<Job> JobConfiguration
        {
            get
            {
                return jobConfiguration;
            }
        }

        public static void QueueJob(Job job)
        {
            jobQueue.Enqueue(job);
        }

        public static Job GetNextJob()
        {
            return jobQueue.Dequeue();
        }
    }
}
