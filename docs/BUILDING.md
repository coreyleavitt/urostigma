# Building (fork)

Prerequisite: .NET 10 SDK.

1. Provision the sibling `TechnitiumLibrary` clone (pinned tag in
   `build/technitiumlibrary.version`):

   ```
   ./build/get-technitiumlibrary.sh
   ```

2. Build the server (never `dotnet build DnsServer.sln` on Linux — the
   solution also contains Windows-only projects that fail to build here):

   ```
   dotnet build DnsServerApp/DnsServerApp.csproj -c Release
   ```

3. Run the tests:

   ```
   dotnet test DnsServerCore.Tests
   ```
