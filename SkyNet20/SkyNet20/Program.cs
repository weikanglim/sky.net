using System;
using SkyNet20.Utility;

namespace SkyNet20
{
    /// <summary>
    /// Runs the <see cref="SkyNet20"></see> program.
    /// </summary>
    public class Program
    {
        static void Main(string[] args)
        {
            SkyNetNode node = new SkyNetNode();
            node.Run();
        }
    }
}