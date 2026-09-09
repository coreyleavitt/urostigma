/*
RFC-010 slice 3: shared `dotnet publish` helper for fixture DNS apps. Factored
out of PoisonedAppFixture (slice 2) once a second fixture app appeared --
publishing a fixture project as publish-shaped output (DLL + .deps.json) via
the real SDK CLI, rather than hand-assembling a fake app folder, is what makes
the staged folder trustworthy for tests that exercise DnsApplication's own
production load path.
*/

using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;

namespace DnsServerCore.Tests.Fixtures
{
    internal static class FixtureAppPublisher
    {
        #region public

        /// <summary>
        /// The <c>Fixtures</c> directory this file lives in -- callers combine this with a
        /// project's relative path to locate its <c>.csproj</c>.
        /// </summary>
        public static string GetFixturesDirectory([CallerFilePath] string thisFilePath = "")
        {
            //this file lives directly in Fixtures\, so its own directory *is* Fixtures\
            return Path.GetDirectoryName(thisFilePath)!;
        }

        /// <summary>
        /// Publishes the project at <paramref name="projectPath"/> (Release, self-contained
        /// output layout: DLL + .deps.json) into <paramref name="outputDirectory"/> via a child
        /// `dotnet publish` process.
        /// </summary>
        public static void Publish(string projectPath, string outputDirectory)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo("dotnet")
            {
                ArgumentList =
                {
                    "publish",
                    projectPath,
                    "-c", "Release",
                    "-o", outputDirectory,
                    "--nologo",
                    "-v", "quiet"
                },
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };

            using (Process process = Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start 'dotnet publish'."))
            {
                string stdout = process.StandardOutput.ReadToEnd();
                string stderr = process.StandardError.ReadToEnd();

                if (!process.WaitForExit(180000))
                {
                    process.Kill(true);
                    throw new TimeoutException("'dotnet publish' for a fixture app timed out.\n" + stdout + stderr);
                }

                if (process.ExitCode != 0)
                {
                    StringBuilder message = new StringBuilder();
                    message.AppendLine($"'dotnet publish {projectPath}' failed with exit code {process.ExitCode}.");
                    message.AppendLine(stdout);
                    message.AppendLine(stderr);

                    throw new InvalidOperationException(message.ToString());
                }
            }
        }

        public static void DeleteIfExists(string path)
        {
            if (File.Exists(path))
                File.Delete(path);
        }

        #endregion
    }
}
