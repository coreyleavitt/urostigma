/*
RFC-010 slice 5: publishes RfcZero.App (see
Fixtures\ZeroRegistrationApp\RfcZero.App) once per test class into a temp
directory, staged the same way PoisonedAppFixture / SingleApiHandlerAppFixture
stage their fixtures -- publish-shaped output (app DLL + .deps.json) plus
dnsApp.config -- so the zero-registration test exercises
DnsApplicationManager's own production load path.
*/

using System;
using System.IO;

namespace DnsServerCore.Tests.Fixtures
{
    public sealed class ZeroRegistrationAppFixture : IDisposable
    {
        #region variables

        readonly string _tempRoot;

        #endregion

        #region constructor

        public ZeroRegistrationAppFixture()
        {
            _tempRoot = Path.Combine(Path.GetTempPath(), "urostigma-zero-registration-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempRoot);

            string projectPath = Path.Combine(FixtureAppPublisher.GetFixturesDirectory(), "ZeroRegistrationApp", "RfcZero.App", "RfcZero.App.csproj");

            FixtureAppPublisher.Publish(projectPath, _tempRoot);

            //stage dnsApp.config alongside the published output so the folder matches
            //what DnsApplication expects to find (GetConfigAsync reads this file)
            File.WriteAllText(Path.Combine(_tempRoot, "dnsApp.config"), string.Empty);
        }

        #endregion

        #region public

        /// <summary>The staged, publish-shaped application folder: an assembly registering zero DNS application types.</summary>
        public string ApplicationFolder
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
