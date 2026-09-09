/*
RFC-010 slice 5: DnsApplicationManager.LoadApplicationAsync is private, reachable
only through DnsApplicationManager's public install/update/uninstall/startup-load
methods, and DnsApplicationManager itself hard-requires a concrete DnsServer (it
wraps it in InternalDnsServer, a concrete-typed field -- not IDnsServer). There is
no seam below a real DnsServer for exercising the production install/uninstall
path, so quirk 1's zero-registration and uninstall-recovery behaviors are driven
through one.

A DnsServer's constructor does not bind sockets or start any listener -- that
happens in StartAsync, which these tests never call -- so construction is just
in-memory manager setup plus config-folder-relative directory creation, and
DisposeAsync()/StopAsync() are no-ops when the server was never started. This
makes a real, never-started DnsServer a usable (if heavier than a fake) test
harness for DnsApplicationManager.

isPortableApp must be true: false points LogManager at a fixed system log
directory (e.g. /var/log/technitium) that a test process is not expected to
have write access to.
*/

using System;
using System.IO;
using DnsServerCore.Dns;

namespace DnsServerCore.Tests.Fixtures
{
    internal static class TestDnsServerFactory
    {
        /// <summary>
        /// Creates a real, never-started DnsServer rooted at a fresh temp config folder.
        /// Callers own the returned server (dispose it, e.g. via <c>await using</c>) and are
        /// responsible for deleting <paramref name="configFolder"/> afterwards.
        /// </summary>
        public static DnsServer Create(string configFolder)
        {
            Directory.CreateDirectory(configFolder);

            LogManager log = new LogManager(true, configFolder);

            return new DnsServer(configFolder, configFolder, log, "test.example");
        }

        /// <summary>Allocates a fresh, non-existent temp directory path for a test's config folder.</summary>
        public static string CreateTempConfigFolderPath()
        {
            return Path.Combine(Path.GetTempPath(), "urostigma-dns-server-" + Guid.NewGuid().ToString("N"));
        }
    }
}
