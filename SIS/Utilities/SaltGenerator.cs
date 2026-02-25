using System.Security.Cryptography;
using System.Text;

namespace SIS.Utilities
{
    /// <summary>
    /// Utility class to generate secure salt for hashing
    /// Run this once to generate your ResultHashSalt value
    /// </summary>
    public static class SaltGenerator
    {
        /// <summary>
        /// Generate a cryptographically secure random salt
        /// </summary>
        /// <param name="length">Length of the salt (default: 64)</param>
        /// <returns>Base64 encoded salt string</returns>
        public static string GenerateSalt(int length = 64)
        {
            using (var rng = RandomNumberGenerator.Create())
            {
                var saltBytes = new byte[length];
                rng.GetBytes(saltBytes);
                return Convert.ToBase64String(saltBytes);
            }
        }

        /// <summary>
        /// Generate a URL-safe salt (no special characters)
        /// </summary>
        public static string GenerateUrlSafeSalt(int length = 64)
        {
            var salt = GenerateSalt(length);
            // Replace URL-unsafe characters
            return salt.Replace("+", "-").Replace("/", "_").Replace("=", "");
        }

        /// <summary>
        /// Generate multiple salts for different purposes
        /// </summary>
        public static Dictionary<string, string> GenerateMultipleSalts()
        {
            return new Dictionary<string, string>
            {
                { "ResultHashSalt", GenerateSalt(64) },
                { "AssessmentHashSalt", GenerateSalt(64) },
                { "AuditHashSalt", GenerateSalt(64) }
            };
        }

        /// <summary>
        /// Main method to run as console app for salt generation
        /// </summary>
        public static void Main(string[] args)
        {
            Console.WriteLine("=== Secure Salt Generator for SIS Results Management ===\n");
            Console.WriteLine("Generated Salt Values (Add to appsettings.json):\n");

            var salts = GenerateMultipleSalts();

            foreach (var kvp in salts)
            {
                Console.WriteLine($"\"{kvp.Key}\": \"{kvp.Value}\"");
            }

            Console.WriteLine("\n=== IMPORTANT SECURITY NOTES ===");
            Console.WriteLine("1. Keep these salts SECRET - never commit to source control");
            Console.WriteLine("2. Use different salts for Development, Staging, and Production");
            Console.WriteLine("3. Store Production salts in Azure Key Vault or secure vault");
            Console.WriteLine("4. If salts are compromised, regenerate and rehash all data");
            Console.WriteLine("5. Add appsettings.json to .gitignore");

            Console.WriteLine("\n=== Configuration Example ===");
            Console.WriteLine("{");
            Console.WriteLine("  \"Security\": {");
            foreach (var kvp in salts)
            {
                Console.WriteLine($"    \"{kvp.Key}\": \"{kvp.Value}\",");
            }
            Console.WriteLine("    \"EnableIntegrityChecks\": true,");
            Console.WriteLine("    \"EnableAuditLogging\": true");
            Console.WriteLine("  }");
            Console.WriteLine("}");

            Console.WriteLine("\nPress any key to exit...");
            Console.ReadKey();
        }
    }
}

/* 
 * TO RUN THIS SALT GENERATOR:
 * 
 * Option 1 - Create a console app:
 * 1. Create a new console project: dotnet new console -n SaltGenerator
 * 2. Copy this code into Program.cs
 * 3. Run: dotnet run
 * 
 * Option 2 - Add to existing project:
 * 1. Create this file in your Utilities folder
 * 2. Run from your IDE or use reflection to call Main()
 * 
 * Option 3 - Use online tool or PowerShell:
 * PowerShell command:
 * [Convert]::ToBase64String((1..64 | ForEach-Object { Get-Random -Maximum 256 }))
 */