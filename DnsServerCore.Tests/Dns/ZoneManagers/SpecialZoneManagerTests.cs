using DnsServerCore.Dns.ZoneManagers;
using Xunit;

namespace DnsServerCore.Tests.Dns.ZoneManagers
{
    public class SpecialZoneManagerTests
    {
        [Theory]
        //locally-served-side forward names (apexes)
        [InlineData("localhost", true)]
        [InlineData("home.arpa", true)]
        [InlineData("resolver.arpa", true)]
        [InlineData("service.arpa", true)]
        //non-existent-side forward names (apexes)
        [InlineData("test", true)]
        [InlineData("invalid", true)]
        [InlineData("local", true)]
        [InlineData("onion", true)]
        //subdomains
        [InlineData("tracker.local", true)]
        [InlineData("foo.home.arpa", true)]
        //case-insensitivity
        [InlineData("LOCALHOST", true)]
        [InlineData("Printer.LOCAL", true)]
        //reverse zone: not in the forward-only set
        [InlineData("1.0.0.127.in-addr.arpa", false)]
        //ordinary name
        [InlineData("example.com", false)]
        //no dot boundary: containment without suffix match must not match
        [InlineData("notlocal", false)]
        [InlineData("mytest", false)]
        public void IsForwardSpecialUseName_ClassifiesQname(string qname, bool expected)
        {
            bool actual = SpecialZoneManager.IsForwardSpecialUseName(qname);

            Assert.Equal(expected, actual);
        }
    }
}
