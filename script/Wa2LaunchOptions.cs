// SPDX-License-Identifier: Apache-2.0
using System;
using System.IO;

public sealed class Wa2LaunchOptions
{
    public string ResourceDirectory { get; private set; }
    public string Language { get; private set; } = "ja";
    public bool AutoStart { get; private set; }
    public string StartScript { get; private set; }
    public string SaveProbePhase { get; private set; }

    public static Wa2LaunchOptions Parse(string[] args, string defaultDirectory)
    {
        var options = new Wa2LaunchOptions { ResourceDirectory = Path.GetFullPath(defaultDirectory) };
        for (int i = 0; i < args.Length; i++)
        {
            string key = args[i];
            if (key == "jp") { options.Language = "ja"; continue; }
            if (key == "--play") { options.AutoStart = true; continue; }
            int equals = key.IndexOf('=');
            string value = equals >= 0 ? key[(equals + 1)..] : null;
            if (equals >= 0) key = key[..equals];
            if (key != "--resources" && key != "--language" && key != "--start-script" && key != "--verify-save")
                throw new ArgumentException($"Unknown game option: {key}");
            if (value == null)
            {
                if (++i >= args.Length || args[i].StartsWith("--", StringComparison.Ordinal))
                    throw new ArgumentException($"Missing value for {key}");
                value = args[i];
            }
            if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException($"Missing value for {key}");
            if (key == "--resources") options.ResourceDirectory = Path.GetFullPath(value);
            else if (key == "--start-script") options.StartScript = value;
            else if (key == "--verify-save")
                options.SaveProbePhase = value is "write" or "read" ? value : throw new ArgumentException("--verify-save must be write or read.");
            else options.Language = NormalizeLanguage(value);
        }
        if (options.SaveProbePhase != null && (!options.AutoStart || options.StartScript == null))
            throw new ArgumentException("--verify-save requires --play and --start-script.");
        return options;
    }

    public static string NormalizeLanguage(string language) => language.ToLowerInvariant() switch
    {
        "ja" or "jp" => "ja",
        "cn" or "zh" or "zh-cn" or "zh-hans" => "zh-Hans",
        "en" => "en",
        "zh-tw" or "zh-hant" => "zh-Hant",
        _ => throw new ArgumentException($"Unsupported language: {language}")
    };
}
