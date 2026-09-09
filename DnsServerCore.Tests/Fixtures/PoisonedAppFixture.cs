/*
RFC-010 slice 2: the poisoned-app fixture. Publishes RfcRepro.App (see
Fixtures\PoisonedApp) once per test class into a temp directory so the staged
output is publish-shaped -- app DLL + .deps.json + dnsApp.config -- exactly
like an installed DNS app folder, then deletes RfcRepro.Helper.dll from that
folder. BravoApp's base type lives only in RfcRepro.Helper, so its layout
cannot be resolved once the DLL is gone: this reproduces a per-type load
failure at the same point the DNS server's own type sweep would encounter it
(Assembly.GetTypes()/ExportedTypes), not merely a constructor throw, which
was already isolated before patch 0001 and would prove nothing.

The publish step runs the real `dotnet publish` CLI as a child process: no
existing automation in this repo builds fixture apps, and running the actual
SDK publish pipeline (rather than hand-assembling a fake app folder) is what
makes the staged folder trustworthy as "what an installed app really looks
like" for the production load path under test.
*/

using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;

namespace DnsServerCore.Tests.Fixtures
{
    public sealed class PoisonedAppFixture : IDisposable
    {
        #region variables

        readonly string _tempRoot;

        #endregion

        #region constructor

        public PoisonedAppFixture()
        {
            _tempRoot = Path.Combine(Path.GetTempPath(), "urostigma-poisoned-app-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempRoot);

            string projectPath = Path.Combine(GetFixturesDirectory(), "PoisonedApp", "RfcRepro.App", "RfcRepro.App.csproj");

            Publish(projectPath, _tempRoot);

            //stage dnsApp.config alongside the published output so the folder matches
            //what DnsApplication expects to find (GetConfigAsync reads this file)
            File.WriteAllText(Path.Combine(_tempRoot, "dnsApp.config"), string.Empty);

            //poison the folder: delete the one dependency BravoApp's base type needs.
            //The .deps.json still lists it, exactly like an app zip that shipped without
            //a referenced helper assembly.
            DeleteIfExists(Path.Combine(_tempRoot, "RfcRepro.Helper.dll"));
            DeleteIfExists(Path.Combine(_tempRoot, "RfcRepro.Helper.pdb"));
        }

        #endregion

        #region private

        static string GetFixturesDirectory([CallerFilePath] string thisFilePath = "")
        {
            //this file lives directly in Fixtures\, so its own directory *is* Fixtures\
            return Path.GetDirectoryName(thisFilePath)!;
        }

        static void Publish(string projectPath, string outputDirectory)
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
                    throw new TimeoutException("'dotnet publish' for the poisoned-app fixture timed out.\n" + stdout + stderr);
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

        static void DeleteIfExists(string path)
        {
            if (File.Exists(path))
                File.Delete(path);
        }

        #endregion

        #region public

        /// <summary>
        /// The staged, publish-shaped application folder with RfcRepro.Helper.dll deleted:
        /// AlphaApp and CharlieApp are loadable, BravoApp is not.
        /// </summary>
        public string PoisonedApplicationFolder
        { get { return _tempRoot; } }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_tempRoot))
                    Directory.Delete(_tempRoot, true);
            }
            catch
            {
                //best-effort cleanup; a leftover temp dir is not worth failing the test run over
            }
        }

        #endregion
    }
}
