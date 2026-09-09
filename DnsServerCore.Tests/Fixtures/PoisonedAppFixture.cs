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
using System.IO;

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

            string projectPath = Path.Combine(FixtureAppPublisher.GetFixturesDirectory(), "PoisonedApp", "RfcRepro.App", "RfcRepro.App.csproj");

            FixtureAppPublisher.Publish(projectPath, _tempRoot);

            //stage dnsApp.config alongside the published output so the folder matches
            //what DnsApplication expects to find (GetConfigAsync reads this file)
            File.WriteAllText(Path.Combine(_tempRoot, "dnsApp.config"), string.Empty);

            //poison the folder: delete the one dependency BravoApp's base type needs.
            //The .deps.json still lists it, exactly like an app zip that shipped without
            //a referenced helper assembly.
            FixtureAppPublisher.DeleteIfExists(Path.Combine(_tempRoot, "RfcRepro.Helper.dll"));
            FixtureAppPublisher.DeleteIfExists(Path.Combine(_tempRoot, "RfcRepro.Helper.pdb"));
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
