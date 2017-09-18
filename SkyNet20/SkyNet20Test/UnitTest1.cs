using Microsoft.VisualStudio.TestTools.UnitTesting;
using SkyNet20;
using SkyNet20.Utility;
using System;
using System.IO;
using System.Security.Cryptography;
namespace SkyNet20Test
{
    [TestClass]
    public class UnitTest1
    {
        SkyNetNode node;

        [TestMethod]
        public async void TestMethod1()
        {
            string[] choices = "dog|cat|world|elephant|cow|mayo|video|man".Split('|');
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

            // Copy files to all servers
            CmdUtility.RunCmd("../../scripts/copyLogs.sh .");
            
            node = new SkyNetNode();

            // All logs
            if (Directory.Exists("test"))
            {
                Directory.Delete("test", true);
            }
            Directory.CreateDirectory("test");

            Directory.SetCurrentDirectory("test");
            Directory.CreateDirectory("expected");

            this.testRare();
            this.testFrequent();
            this.testSomewhatFrequent();
            this.testSomeLogs();
            this.testAllLogs();
            this.testOneLog();
        }

        private async void testRare()
        {
            this.testExpression("dog");
        }

        private async void testFrequent()
        {
            this.testExpression("is");
        }

        private async void testSomewhatFrequent()
        {
            this.testExpression("not");
        }

        private async void testSomeLogs()
        {
            this.testExpression("GreaterThan5");
        }

        private async void testAllLogs()
        {
            this.testExpression("This");
        }

        private async void testOneLog()
        {
            this.testExpression("MagicNumber5");
        }

        private async void testExpression(string expr)
        {
            await node.DistributedGrep(expr);
            CmdUtility.RunCmd("../../scripts/grepResults.sh " + expr);
            this.assertGrepResults();
        }


        private void assertGrepResults()
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
