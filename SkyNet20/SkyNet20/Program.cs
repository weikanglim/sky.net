using System;
using SkyNet20.Utility;

namespace SkyNet20
{
    class Program
    {
        static void Main(string[] args)
        {
            bool runInteractive = false;

            foreach (var arg in args)
            {
                if ("-i".Equals(arg, StringComparison.OrdinalIgnoreCase))
                {
                    runInteractive = true;
                }
            }

            SkyNetNode node = new SkyNetNode();
            if (runInteractive)
            {    
                node.RunInteractive();
            }
            else
            {
                node.Run();
            }
        }
    }
}