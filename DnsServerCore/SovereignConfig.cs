/*
RFC-010 slice 7 (quirk 2b): fork-owned settings do not persist in dns.config.
dns.config is a positional binary format with a single linear version byte that
upstream owns and bumps for its own settings; a fork-persisted field claiming the
next version number would collide with whatever upstream lands on that same
number next, causing a positional misparse against the operator's real config on
the first post-merge load.

Fork-owned settings persist here instead: a small JSON sidecar, "sovereign.config",
written in the same config folder as dns.config, loaded and saved independently of
it. This class is expected to grow additional properties as future fork settings
are added; unknown fields in the JSON are ignored by System.Text.Json by default,
so an older build can still read a sidecar written by a newer one (and vice versa,
for fields both sides already know about).

A missing sidecar is not an error -- it means "defaults," so a fresh install or a
config folder from before this setting existed both load cleanly.
*/

using System;
using System.IO;
using System.Text.Json;

namespace DnsServerCore
{
    public sealed class SovereignConfig
    {
        public bool SpecialUseNamesDeferToBlocking { get; set; }

        private static string GetFilePath(string configFolder)
        {
            return Path.Combine(configFolder, "sovereign.config");
        }

        public static SovereignConfig Load(string configFolder, LogManager log)
        {
            string filePath = GetFilePath(configFolder);

            try
            {
                if (!File.Exists(filePath))
                    return new SovereignConfig();

                string json = File.ReadAllText(filePath);

                SovereignConfig config = JsonSerializer.Deserialize<SovereignConfig>(json);

                return config ?? new SovereignConfig();
            }
            catch (Exception ex)
            {
                log?.Write("Failed to load sovereign config file; using defaults: " + filePath, ex);

                return new SovereignConfig();
            }
        }

        public void Save(string configFolder, LogManager log)
        {
            string filePath = GetFilePath(configFolder);

            try
            {
                string json = JsonSerializer.Serialize(this);

                File.WriteAllText(filePath, json);
            }
            catch (Exception ex)
            {
                log?.Write("Failed to save sovereign config file: " + filePath, ex);
            }
        }
    }
}
