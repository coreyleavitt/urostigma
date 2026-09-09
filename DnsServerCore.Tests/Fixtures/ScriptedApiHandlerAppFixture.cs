/*
RFC-010 slice 4: publishes RfcApiHandler.Scripted (see
Fixtures\ApiHandlerApps\RfcApiHandler.Scripted) once per test class into a
temp directory, staged the same way SingleApiHandlerAppFixture stages
RfcApiHandler.Single, so DnsAppApiDispatcher tests exercise a real
DnsApplication/IDnsApplicationApiHandler pair loaded through the production
sweep, with a handler whose behavior the test can select via request path.
*/

using System;
using System.IO;

namespace DnsServerCore.Tests.Fixtures
{
    public sealed class ScriptedApiHandlerAppFixture : IDisposable
    {
        #region variables

        readonly string _tempRoot;

        #endregion

        #region constructor

        public ScriptedApiHandlerAppFixture()
        {
            _tempRoot = Path.Combine(Path.GetTempPath(), "urostigma-scripted-api-handler-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempRoot);

            string projectPath = Path.Combine(FixtureAppPublisher.GetFixturesDirectory(), "ApiHandlerApps", "RfcApiHandler.Scripted", "RfcApiHandler.Scripted.csproj");

            FixtureAppPublisher.Publish(projectPath, _tempRoot);

            //stage dnsApp.config alongside the published output so the folder matches
            //what DnsApplication expects to find (GetConfigAsync reads this file)
            File.WriteAllText(Path.Combine(_tempRoot, "dnsApp.config"), string.Empty);
        }

        #endregion

        #region public

        /// <summary>The staged, publish-shaped application folder: one app registering one path-driven IDnsApplicationApiHandler.</summary>
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
