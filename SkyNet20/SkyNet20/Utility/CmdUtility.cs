using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace SkyNet20.Utility
{
    class CmdUtility
    {
        public static CmdResult RunCmd(string cmd)
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
                    output.Append(e.Data);
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

        public static string RunGrep(string grepExpression, string fileName)
        {
            // Might need to be stream based, to enable writing of data faster
            // TODO: What is the intended output format?
            CmdResult cmdResult = RunCmd($"rg \"{grepExpression}\" {fileName}");
            string result =  cmdResult.Output;

            return result;
        }
    }
}
