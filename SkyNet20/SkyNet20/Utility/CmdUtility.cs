using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace SkyNet20.Utility
{
    /// <summary>
    /// Utility class for running bash commands.
    /// </summary>
    public class CmdUtility
    {
        /// <summary>
        /// Runs a bash command.
        /// </summary>
        /// <param name="cmd">
        /// A cmd to run.
        /// </param>
        /// <returns>
        /// A <see cref="CmdResult"/> that contains the results of the command.
        /// </returns>
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
            int outputLines = 0;
            process.OutputDataReceived += (sender, e) => {
                if (e.Data != null)
                {
                    output.AppendLine(e.Data);
                    outputLines++;
                }
            };

            process.ErrorDataReceived += (sender, e) => {
                if (e.Data != null)
                {
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
                OutputLines = outputLines,
                Error = error.ToString(),
                ExitCode = process.ExitCode,
            };

            return result;
        }

        /// <summary>
        /// Runs a grep command.
        /// </summary>
        /// <param name="grepExpression">
        /// A regualr expression that is valid for GNU grep.
        /// </param>
        /// <param name="fileName">
        /// A filename to perform the grep command.
        /// </param>
        /// <returns>
        /// A <see cref="CmdResult"/> that contains the results of the command.
        /// </returns>
        /// <seealso cref="CmdUtility.RunCmd(string)"/>
        public static CmdResult RunGrep(string grepExpression, string fileName)
        {
            return RunCmd($"grep \"{grepExpression}\" {fileName}");
        }
    }
}
