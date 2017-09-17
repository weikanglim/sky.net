using System;
using System.Collections.Generic;
using System.Text;

namespace SkyNet20
{
    class LogResults
    {
        private Dictionary<string, string> results;

        public void AddResult(string fileName, string fileContents)
        {
            results.Add(fileName, fileContents);
        }
    }
}
