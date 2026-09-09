/*
RFC-010 slice 5 fixture: a class deliberately unrelated to
DnsServerCore.ApplicationCommon.IDnsApplication (or any of its sibling
interfaces). The published assembly loads cleanly -- Assembly.GetTypes()
never throws -- but the constructor sweep in DnsApplication finds nothing
to register, reproducing the "nothing implements the interfaces"
zero-registration case.
*/

namespace RfcZero.App
{
    public class Placeholder
    {
        public string Greeting => "This assembly registers no DNS application types.";
    }
}
