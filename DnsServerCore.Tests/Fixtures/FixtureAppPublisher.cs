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
        #region variables

        //RFC-010 slice 4: a second test class (DnsAppApiDispatcherTests) started sharing fixture
        //projects that a first class already owned (SingleApiHandlerAppFixture,
        //AmbiguousApiHandlerAppFixture, PoisonedAppFixture) once IClassFixture instances for both
        //classes exist concurrently, two `dotnet publish` child processes race on the same fixture
        //project's shared obj\ intermediate directory and corrupt each other's ref-assembly copy.
        //Each fixture stages its own _tempRoot output directory, so serializing only the publish
        //step -- not the tests themselves -- is sufficient and keeps the fix in the one place that
        //owns the child process.
        static readonly object _publishLock = new object();

        #endregion

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
            //serialize the child process, not the caller: two IClassFixture instances for the same
            //fixture project (one per consuming test class) must not run `dotnet publish` against
            //that project's shared obj\ directory at the same time
            lock (_publishLock)
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
        }

        public static void DeleteIfExists(string path)
        {
            if (File.Exists(path))
                File.Delete(path);
        }

        #endregion
    }
}
