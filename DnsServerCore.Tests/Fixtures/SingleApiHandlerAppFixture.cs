/*
RFC-010 slice 3: publishes RfcApiHandler.Single (see
Fixtures\ApiHandlerApps\RfcApiHandler.Single) once per test class into a temp
directory, staged the same way PoisonedAppFixture stages RfcRepro.App --
publish-shaped output (app DLL + .deps.json) plus dnsApp.config -- so the
discovery test exercises DnsApplication's own production load path.
*/

using System;
using System.IO;

namespace DnsServerCore.Tests.Fixtures
{
    public sealed class SingleApiHandlerAppFixture : IDisposable
    {
        #region variables

        readonly string _tempRoot;

        #endregion

        #region constructor

        public SingleApiHandlerAppFixture()
        {
            _tempRoot = Path.Combine(Path.GetTempPath(), "urostigma-single-api-handler-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempRoot);

            string projectPath = Path.Combine(FixtureAppPublisher.GetFixturesDirectory(), "ApiHandlerApps", "RfcApiHandler.Single", "RfcApiHandler.Single.csproj");

            FixtureAppPublisher.Publish(projectPath, _tempRoot);

            //stage dnsApp.config alongside the published output so the folder matches
            //what DnsApplication expects to find (GetConfigAsync reads this file)
            File.WriteAllText(Path.Combine(_tempRoot, "dnsApp.config"), string.Empty);
        }

        #endregion

        #region public

        /// <summary>The staged, publish-shaped application folder: one app registering one IDnsApplicationApiHandler.</summary>
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
