using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace SkyNet20.Utility
{
    class CmdUtility
    {
        public static CmdResult RunCmd(string cmd, StreamWriter stdOutStream)
        {
            var escapedArgs = cmd.Replace("\"", "\\\"");

            var process = new Process()
            {
                EnableRaisingEvents = true,

                StartInfo = new ProcessStartInfo
                {
                    FileName = "/bin/bash",
                    Arguments = $"-c \"{escapedArgs}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                }
            };

            StringBuilder output = new StringBuilder();
            StringBuilder error = new StringBuilder();
            process.OutputDataReceived += (sender, e) => {
                if (e.Data != null)
                {
                    Console.WriteLine(e.Data);
                    output.AppendLine(e.Data);
                    stdOutStream.WriteLine(e.Data);
                }
            };

            process.ErrorDataReceived += (sender, e) => {
                if (e.Data != null)
                {
                    Console.WriteLine(e.Data);
                    error.Append(e.Data);
                }
            };

            process.Start();
            Console.WriteLine($"Running command: {process.StartInfo.FileName} {process.StartInfo.Arguments}");

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            process.WaitForExit();
            Console.WriteLine("Exited with error code " + process.ExitCode);
            process.CancelOutputRead();
            process.CancelErrorRead();

            CmdResult result = new CmdResult
            {
                Output = output.ToString(),
                Error = error.ToString(),
                ExitCode = process.ExitCode,
            };

            return result;
        }

        public static CmdResult RunGrep(string grepExpression, string fileName, StreamWriter stdOutStream)
        {
            return RunCmd($"rg \"{grepExpression}\" {fileName}", stdOutStream);
        }
    }
}
