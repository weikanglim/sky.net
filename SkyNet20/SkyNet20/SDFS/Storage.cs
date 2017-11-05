using System;
using System.Collections.Generic;
using System.Text;
using SkyNet20.Utility;
using System.IO;
using System.Threading.Tasks;

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


        public static void Initialize()
        {
            if (!Directory.Exists(storageDirectory))
            {
                Directory.CreateDirectory(storageDirectory);
            }

            if (!Directory.Exists(stagingDirectory))
            {
                Directory.CreateDirectory(stagingDirectory);
            }
        }

        public static IEnumerable<string> ListStoredFiles()
        {
            return Directory.EnumerateFiles(storageDirectory);
        }

        public static void MoveStagingToStorage(string filename)
        {
            string stagedFile = GetStagingFilePath(filename);

            if (!File.Exists(stagedFile))
            {
                throw new FileNotFoundException($"File {filename} does not exist.");
            }

            Directory.Move(
                stagedFile,
                GetStoredFilePath(filename));
        }

        public static async Task WriteAsync(byte[] payload, string filename)
        {
            using (FileStream fs = File.Create(GetStoredFilePath(filename)))
            {
                await fs.WriteAsync(payload, 0, payload.Length);
            }
        }

        public static async Task StageAsync(byte[] payload, string filename)
        {
            using (FileStream fs = File.Create(GetStagingFilePath(filename)))
            {
                await fs.WriteAsync(payload, 0, payload.Length);
            }
        }


        public static bool Exists(string filename)
        {
            return File.Exists(GetStoredFilePath(filename));
        }

        public static FileStream Read(string filename)
        {
            return File.OpenRead(GetStoredFilePath(filename));
        }

        public static async Task<byte[]> ReadContentAsync(string filename)
        {
            return await File.ReadAllBytesAsync(GetStoredFilePath(filename));
        }

        public static long FileBytes(string filename)
        {
            return new FileInfo(GetStoredFilePath(filename)).Length;
        }

        public static void Delete(string filename)
        {
            File.Delete(GetStoredFilePath(filename));
        }

        private static string GetStoredFilePath(string filename)
        {
            return StorageDirectory + Path.DirectorySeparatorChar + filename;
        }

        public static DateTime LastModified(string filename)
        {
            return new FileInfo(GetStoredFilePath(filename)).LastWriteTime;
        }

        private static string GetStagingFilePath(string filename)
        {
            return StagingDirectory + Path.DirectorySeparatorChar + filename;
        }

        public static string StagingDirectory
        {
            get
            {
                return stagingDirectory;
            }
        }

        public static string StorageDirectory
        {
            get
            {
                return storageDirectory;
            }
        }
    }
}
