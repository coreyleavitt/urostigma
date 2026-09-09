using DnsServerCore.Dns.ZoneManagers;
using Xunit;

namespace DnsServerCore.Tests.Dns.ZoneManagers
{
    public class AuthZoneManagerTests
    {
        [Theory]
        [InlineData("www.example.com", "example.com")]
        [InlineData("example.com", "com")]
        [InlineData("com", null)]
        public void GetParentZone_ReturnsImmediateParent(string domain, string? expectedParent)
        {
            string? parentZone = AuthZoneManager.GetParentZone(domain);

            Assert.Equal(expectedParent, parentZone);
        }
    }
}
