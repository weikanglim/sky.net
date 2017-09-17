using System;
using System.Collections.Generic;
using System.Text;

namespace SkyNet20.Utility
{
    public class CmdResult
    {
        public String Output { get; set; }
        public String Error { get; set; }
        public int OutputLines { get; set; }
        public int ExitCode { get; set; }
    }
}
