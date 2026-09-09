/*
RFC-010 slice 2 fixture: ported from technitium-content-filter's
patches/dns-server/pr1-repro (RfcRepro.Helper/MissingBase.cs), unchanged.
*/

namespace RfcRepro.Helper
{
    // This assembly is intentionally OMITTED from the staged app folder (see
    // PoisonedAppFixture, which deletes it after publish). BravoApp derives from this class,
    // so resolving BravoApp's base type during Assembly.GetTypes()/ExportedTypes fails with a
    // TypeLoadException (wrapped in ReflectionTypeLoadException) because the type's LAYOUT
    // depends on this base type -- not merely a method body reference, which would only fail at
    // JIT time, well after type enumeration succeeds.
    public abstract class MissingBase
    {
        public virtual string Marker => "helper-present";
    }
}
