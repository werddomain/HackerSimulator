using System;
using System.Collections.Generic;
using HackerOs.OS.Shell;

namespace HackerOs.Shell.Tests
{
    /// <summary>
    /// Quick test program to verify advanced shell operator functionality
    /// </summary>
    public class AdvancedOperatorTest
    {
        public static void Main()
        {
            Console.WriteLine("Testing Enhanced AST Parser with Advanced Operators");
            Console.WriteLine("=================================================");

            // Test cases for various operators
            var testCases = new[]
            {
                // Basic pipe
                "cat file.txt | grep pattern",
                
                // AND operator
                "ls -la && echo 'Success'",
                "false && echo 'This should not run'",
                
                // OR operator
                "true || echo 'This should not run'", 
                "false || echo 'This should run'",
                
                // Semicolon operator
                "echo 'First'; echo 'Second'; echo 'Third'",
                
                // Complex combinations
                "cat file.txt | grep pattern && echo 'Found' || echo 'Not found'",
                "ls -la; cat file.txt | head -5; echo 'Done'",
                
                // With quotes
                "echo 'Hello && World' && echo 'Success'"
            };

            foreach (var testCase in testCases)
            {
                Console.WriteLine($"\nTesting: {testCase}");
                Console.WriteLine(new string('-', 50));
                
                try
                {
                    var ast = CommandParser.ParseCommandLineToAST(testCase);
                    
                    Console.WriteLine($"Commands: {ast.Commands.Count}");
                    for (int i = 0; i < ast.Commands.Count; i++)
                    {
                        var cmd = ast.Commands[i];
                        Console.WriteLine($"  [{i}] {cmd.Command} {string.Join(" ", cmd.Arguments)}");
                    }
                    
                    Console.WriteLine($"Operators: {ast.Operators.Count}");
                    for (int i = 0; i < ast.Operators.Count; i++)
                    {
                        Console.WriteLine($"  [{i}] {ast.Operators[i]}");
                    }
                    
                    Console.WriteLine("✅ Parsed successfully");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Error: {ex.Message}");
                }
            }
            
            Console.WriteLine("\n=================================================");
            Console.WriteLine("Test completed. Press any key to exit...");
            Console.ReadKey();
        }
    }
}
