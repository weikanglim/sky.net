using System;
using System.Collections.Generic;
using System.Text;
using SkyNet20.Utility;
using System.IO;

namespace SkyNet20.SDFS
{
    public enum StorageOperationType
    {
        Put,
        Get,
        Delete,
        List,
    }

    public class Storage
    {
        private static readonly string storageDirectory = SkyNetConfiguration.ProgramPath + Path.DirectorySeparatorChar + "fs";
        private static readonly string stagingDirectory = SkyNetConfiguration.ProgramPath + Path.DirectorySeparatorChar + "stage";

        public static IEnumerable<string> ListStoredFiles()
        {
            return Directory.EnumerateFiles(storageDirectory);
        }

        public static void MoveStagingToStorage(string filename)
        {
            string stagedFile = stagingDirectory + Path.DirectorySeparatorChar + filename;

            if (!File.Exists(stagedFile))
            {
                throw new FileNotFoundException($"File {filename} does not exist.");
            }

            Directory.Move(
                stagedFile,
                storageDirectory + Path.DirectorySeparatorChar + filename);
        }

        public string StagingDirectory
        {
            get
            {
                return stagingDirectory;
            }
        }

        public string StoragetDirectory
        {
            get
            {
                return storageDirectory;
            }
        }
    }
}
