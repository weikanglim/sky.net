using Microsoft.VisualStudio.TestTools.UnitTesting;
using SkyNet20;
using SkyNet20.Utility;
using System;
using System.IO;
using System.Security.Cryptography;
namespace SkyNet20Test
{
    [TestClass]
    public class SkyNetTest
    {
        SkyNetNode node;
        // Rare pattern choices, 1/8 chance
        string[] choices = "dog|cat|world|elephant|cow|mayo|video|man".Split('|');


        [TestMethod]
        public void TestDistributedGrep()
        {
            // Arrange
            this.GenerateLogFiles();

            // Copy test files to all servers
            CmdUtility.RunCmd("../../scripts/copyLogs.sh .");
            
            node = new SkyNetNode();

            // Create directories
            if (Directory.Exists("test"))
            {
                Directory.Delete("test", true);
            }
            Directory.CreateDirectory("test");

            Directory.SetCurrentDirectory("test");
            Directory.CreateDirectory("expected");

            // Run tests
            this.TestRare();
            this.TestFrequent();
            this.TestSomewhatFrequent();
            this.TestSomeLogs();
            this.TestAllLogs();
            this.TestOneLog();
        }

        private void GenerateLogFiles()
        {
            Random rand = new Random();

            for (int i = 1; i <= 10; i++)
            {
                using (StreamWriter writer = File.AppendText($"vm.{i}.log"))
                {
                    for (int lines = 1; lines <= rand.Next(100, 200); lines++)
                    {
                        int selection = rand.Next(choices.Length);

                        if (rand.NextDouble() > 0.5)
                        {
                            writer.WriteLine($"This is a {choices[selection]}.");
                        }
                        else
                        {
                            writer.WriteLine($"This is not a {choices[selection]}.");
                        }
                    }

                    if (i == 5)
                    {
                        writer.WriteLine("MagicNumber5");
                    }

                    if (i > 5)
                    {
                        writer.WriteLine("GreaterThan5");
                    }
                }
            }
        }

        private void TestRare()
        {
            this.TestExpression("dog");
        }

        private void TestFrequent()
        {
            this.TestExpression("is");
        }

        private void TestSomewhatFrequent()
        {
            this.TestExpression("not");
        }

        private void TestSomeLogs()
        {
            this.TestExpression("GreaterThan5");
        }

        private void TestAllLogs()
        {
            this.TestExpression("This");
        }

        private void TestOneLog()
        {
            this.TestExpression("MagicNumber5");
        }

        private void TestExpression(string expr)
        {
            node.DistributedGrep(expr).ConfigureAwait(false).GetAwaiter();
            CmdUtility.RunCmd("../../scripts/grepResults.sh " + expr);
            this.AssertGrepResults();
        }


        private void AssertGrepResults()
        {
            foreach (var file in Directory.EnumerateFiles(Directory.GetCurrentDirectory()))
            {
                MD5 md5 = MD5.Create();
                byte[] expectedHash = File.ReadAllBytes("expected/" + file);
                byte[] hash = md5.ComputeHash(File.ReadAllBytes(file));

                CollectionAssert.AreEqual(expectedHash, hash);
            }
        }
    }
}
