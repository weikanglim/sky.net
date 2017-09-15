using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace SkyNet20.Utility
{
    class CmdUtility
    {
        public static string RunCmd(string cmd)
        {
            var escapedArgs = cmd.Replace("\"", "\\\"");

            var process = new Process()
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "/bin/bash",
                    Arguments = $"-c \"{escapedArgs}\"",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                }
            };

            process.Start();
            string result = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            return result;
        }

        public static string RunGrep(string grepExpression, string fileName)
        {
            // TODO: What is the intended output format?
            return RunCmd($"grep {grepExpression} {fileName}");
        }
    }
}
