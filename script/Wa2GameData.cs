// SPDX-License-Identifier: Apache-2.0
using Godot;
using System;
using System.IO;
using System.Text;

public sealed class Wa2GameData
{
    public static Wa2GameData Current { get; private set; }
    public string Directory { get; }
    public string Language { get; }
    public bool IsSpecialContents { get; }
    public string SaveProfile => Wa2Content.SaveProfile(IsSpecialContents, Language);
    public string ScriptArchive { get; private set; }
    public string FontArchive { get; private set; }
    public Encoding TextEncoding { get; private set; }

    public Wa2GameData(string directory, string language)
    {
        Directory = Path.GetFullPath(directory);
        Language = Wa2LaunchOptions.NormalizeLanguage(language);
        IsSpecialContents = !System.IO.Directory.Exists(Resolve("IC"));
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        ScriptArchive = Wa2Content.ScriptArchive(IsSpecialContents, Language);
        FontArchive = Language == "zh-Hans" ? "fon.pak" : "fnt.pak";
        TextEncoding = Language == "zh-Hans" ? new Wa2Encoding() : Encoding.GetEncoding(932);

        string profilePath = Path.Combine(Directory, "languages", Language + ".cfg");
        if (System.IO.File.Exists(profilePath))
        {
            var profile = new ConfigFile();
            if (profile.Load(profilePath) != Error.Ok)
                throw new InvalidDataException($"Cannot read language profile: {profilePath}");
            ScriptArchive = profile.GetValue("language", "script_archive", ScriptArchive).AsString();
            FontArchive = profile.GetValue("language", "font_archive", FontArchive).AsString();
            string encoding = profile.GetValue("language", "text_encoding", "shift_jis").AsString();
            TextEncoding = encoding == "ck-gal" ? new Wa2Encoding() : Encoding.GetEncoding(encoding);
        }
        else if (Language is "en" or "zh-Hant")
            throw new InvalidDataException($"Provide your translation and languages/{Language}.cfg before selecting {Language}.");
    }

    public string Resolve(string relative)
    {
        string candidate = Path.GetFullPath(Path.Combine(Directory, relative));
        if (!candidate.StartsWith(Directory + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new InvalidDataException($"Resource path escapes game directory: {relative}");
        string current = Directory;
        foreach (string part in Path.GetRelativePath(Directory, candidate).Split(Path.DirectorySeparatorChar))
        {
            string exact = Path.Combine(current, part);
            if (System.IO.File.Exists(exact) || System.IO.Directory.Exists(exact)) { current = exact; continue; }
            string match = null;
            if (System.IO.Directory.Exists(current))
                foreach (string item in System.IO.Directory.EnumerateFileSystemEntries(current))
                    if (string.Equals(Path.GetFileName(item), part, StringComparison.OrdinalIgnoreCase))
                    {
                        if (match != null) throw new InvalidDataException($"Ambiguous resource path: {relative}");
                        match = item;
                    }
            current = match ?? exact;
        }
        return current;
    }

    public void Mount()
    {
        string[] archives = IsSpecialContents
            ? new[] { "BGM.PAK", "bak.pak", "grp.pak", "char.pak", "VOICE.PAK", "SE.PAK", ScriptArchive, FontArchive }
            : new[] { "BGM.PAK", "IC/BGM.PAK", "IC/bak.pak", "IC/grp.pak",
            "IC/char.pak", "IC/VOICE.PAK", "IC/SE.PAK", "bak.pak", "grp.pak", "char.pak",
            "VOICE.PAK", "SE.PAK", ScriptArchive, FontArchive };
        foreach (string archive in archives)
            if (!System.IO.File.Exists(Resolve(archive)))
                throw new FileNotFoundException($"Missing game resource: {archive}", Resolve(archive));
        Wa2Resource.FileDic.Clear();
        Wa2Resource.ClearCaches();
        Wa2Resource.ResPath = Directory + Path.DirectorySeparatorChar;
        foreach (string archive in archives) Wa2Resource.LoadPak(Resolve(archive));
        if (IsSpecialContents)
        {
            foreach (string patch in new[] { "patch.pak", Language == "zh-Hans" ? "gcn.pak" : "" })
                if (patch.Length > 0 && System.IO.File.Exists(Resolve(patch))) Wa2Resource.LoadPak(Resolve(patch));
            // Fonts and scripts always take precedence over optional image patches.
            Wa2Resource.LoadPak(Resolve(ScriptArchive));
            Wa2Resource.LoadPak(Resolve(FontArchive));
        }
        foreach (string chapter in IsSpecialContents ? new[] { "6001", "6101", "4000" } : new[] { "1001", "2001", "3001" })
            if (!Wa2Resource.FileDic.ContainsKey(chapter + ".bnr") || !Wa2Resource.FileDic.ContainsKey(chapter + ".txt"))
                throw new InvalidDataException($"Selected script archive is missing chapter {chapter}.");
        Current = this;
    }
}
