using System;
using System.Threading.Tasks;
using HackerOs.IO.Tests;

namespace HackerOs.IO.TestRunner
{
    /// <summary>
    /// Console application to run IO module tests.
    /// </summary>
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("HackerOS IO Module Test Runner");
            Console.WriteLine("==============================");
            Console.WriteLine();

            try
            {
                var success = await IOModuleTests.RunAllTestsAsync();
                
                Console.WriteLine();
                if (success)
                {
                    Console.WriteLine("🎉 All tests completed successfully!");
                    Environment.Exit(0);
                }
                else
                {
                    Console.WriteLine("❌ Some tests failed.");
                    Environment.Exit(1);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"💥 Test runner crashed: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                Environment.Exit(1);
            }
        }
    }
}
