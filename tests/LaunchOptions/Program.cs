// SPDX-License-Identifier: Apache-2.0
using System;
using System.IO;

static void Equal<T>(T expected, T actual) { if (!Equals(expected, actual)) throw new Exception($"Expected {expected}, got {actual}"); }
static void Reject(string[] args) { try { Wa2LaunchOptions.Parse(args, "/resources"); } catch (ArgumentException) { return; } throw new Exception("Invalid options accepted"); }
Equal("ja", Wa2LaunchOptions.Parse(new[] { "jp" }, "/resources").Language);
Equal("zh-Hans", Wa2LaunchOptions.Parse(new[] { "--language", "cn" }, "/resources").Language);
Equal(Path.GetFullPath("/custom game"), Wa2LaunchOptions.Parse(new[] { "--resources", "/custom game" }, "/resources").ResourceDirectory);
Equal("en", Wa2LaunchOptions.Parse(new[] { "--language=en" }, "/resources").Language);
Reject(new[] { "--language", "unknown" });
Reject(new[] { "--resources" });
Reject(new[] { "--language" });
Console.WriteLine("7 launch-option cases passed.");

System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
var encoding = new Wa2Encoding();
Equal(3, encoding.GetByteCount(new[] { 'A', '蹑' }, 0, 2));
Equal("蹑", encoding.GetString(new byte[] { 0, 0x89, 0x7f, 0 }, 1, 2));
Equal(1, encoding.GetCharCount(new byte[] { 0x89, 0x7f }, 0, 2));
Equal(1, encoding.GetString(new byte[] { 0xe0 }).Length);
Equal("日本語", encoding.GetString(System.Text.Encoding.GetEncoding(932).GetBytes("日本語")));
Console.WriteLine("5 encoding cases passed.");

// Reference: the original per-unit decoder, kept here to prove the table decoder is equivalent.
var shiftJis = System.Text.Encoding.GetEncoding(932);
var customBytes = new System.Collections.Generic.Dictionary<int, char>();
foreach (var kv in encoding.CustomMappings) customBytes[kv.Value[0] << 8 | kv.Value[1]] = kv.Key;
string ReferenceDecode(byte[] data)
{
    var sb = new System.Text.StringBuilder();
    for (int i = 0; i < data.Length;)
    {
        byte b1 = data[i];
        bool lead = (b1 >= 0x81 && b1 <= 0x9F) || (b1 >= 0xE0 && b1 <= 0xFE);
        if (i + 1 < data.Length && lead)
        {
            if (customBytes.TryGetValue(b1 << 8 | data[i + 1], out char c)) sb.Append(c);
            else sb.Append(shiftJis.GetChars(data, i, 2));
            i += 2;
        }
        else { sb.Append(shiftJis.GetChars(data, i, 1)); i += 1; }
    }
    return sb.ToString();
}
int pairMismatches = 0;
for (int pair = 0; pair < 0x10000; pair++)
{
    var unit = new[] { (byte)(pair >> 8), (byte)pair };
    if (encoding.GetString(unit) != ReferenceDecode(unit)) pairMismatches++;
    if (encoding.GetCharCount(unit, 0, 2) != ReferenceDecode(unit).Length) pairMismatches++;
}
Equal(0, pairMismatches);
var random = new Random(20261016);
for (int round = 0; round < 2000; round++)
{
    var data = new byte[random.Next(0, 40)];
    random.NextBytes(data);
    Equal(ReferenceDecode(data), encoding.GetString(data));
}
Equal(true, encoding.CustomMappings.Count > 0);
Console.WriteLine("3 table decoder equivalence cases passed (65536 pairs, 2000 random strings).");

byte[] tga = new byte[28];
tga[1] = 1; tga[2] = 1; tga[5] = 2; tga[7] = 32;
tga[12] = 2; tga[14] = 1; tga[16] = 8; tga[17] = 32;
tga[18] = 30; tga[19] = 20; tga[20] = 10; tga[21] = 255;
tga[22] = 60; tga[23] = 50; tga[24] = 40; tga[25] = 128;
tga[26] = 1; tga[27] = 0;
Equal(true, Wa2Tga.TryDecodeIndexed(tga, out int w, out int h, out byte[] rgba));
Equal(2, w); Equal(1, h);
Equal("28-32-3C-80-0A-14-1E-FF", BitConverter.ToString(rgba));
try { Wa2Tga.TryDecodeIndexed(tga[..27], out _, out _, out _); throw new Exception("Truncated TGA accepted"); }
catch (InvalidDataException) { }
Console.WriteLine("5 indexed TGA cases passed.");

Wa2Value integer = 7;
Wa2Value other = 2;
Wa2Value fraction = 2.5f;
Equal(3, (int)(integer / other));
Equal(true, (integer / other).IsInteger);
Equal(2.8f, (float)(integer / fraction));
Equal(false, (integer / fraction).IsInteger);
Equal(9, (int)(integer + other));
Equal(5, (int)(integer - other));
Equal(14, (int)(integer * other));
Equal(1, (int)(integer % other));
Equal(2, (int)(integer & other));
Equal(7, (int)(integer | other));
Equal(true, integer > other);
Equal(true, other == (Wa2Value)2f);
Equal(false, integer == other);
Equal("0007", string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0:D4}", integer));
Equal("test", (string)(Wa2Value)"test");
Equal(3.5f, (float)(other + 1.5f));
Equal(6, (int)(3 * other));
Equal(16777217, (int)(Wa2Value)16777217);
Equal(0, (int)default(Wa2Value));
Equal(true, default(Wa2Value) == (Wa2Value)0);
Equal(2147483647, (int)((Wa2Value)2147483646 + 1));
try { _ = fraction & other; throw new Exception("Fractional bitwise operand accepted"); }
catch (InvalidOperationException) { }
Console.WriteLine("22 typed VM value cases passed.");

var subtitles = Wa2Subtitles.Parse("""
soundSubtitle:
  - id: 17
    content:
      - begin: 100
        end: 250
        text: "测试 / テスト"
voiceSubtitle:
  - id: 23
    scene: 1001
    content: []
""");
Equal(17, subtitles.SoundSubtitle[0].Id);
Equal("测试 / テスト", subtitles.SoundSubtitle[0].Content[0].Text);
Equal(250, subtitles.SoundSubtitle[0].Content[0].End);
Equal(1001, subtitles.VoiceSubtitle[0].Scene);
Equal(0, Wa2Subtitles.Parse("{}").SoundSubtitle.Count);
try { Wa2Subtitles.Parse("soundSubtitle: [{id: bad}]"); throw new Exception("Invalid subtitle ID accepted"); }
catch (InvalidDataException) { }
try { Wa2Subtitles.Parse("[wrong, root]"); throw new Exception("Invalid subtitle root accepted"); }
catch (InvalidDataException) { }
Console.WriteLine("7 static subtitle parser cases passed.");

var patchSubs = Wa2Subtitles.Parse("""
soundEffect: [{id: 1, content: [{begin: 100, end: 200, text: se}]}]
voice: [{scene: 1001, id: 2, content: []}]
voiceDrama: [{id: 80, chapter: 0, content: [{begin: 100, end: 200, text: drama}]}]
song: [{id: 80, chapter: 0, content: [{begin: 150, end: 250, text: song}]}]
""");
Equal(1, patchSubs.SoundSubtitle[0].Id);
Equal(1001, patchSubs.VoiceSubtitle[0].Scene);
Equal(80, patchSubs.VoiceDrama[0].Id);
Equal(0, patchSubs.Song[0].Chapter);
Equal("", Wa2Subtitles.TextAt(patchSubs.SoundSubtitle[0].Content, 99));
Equal("se", Wa2Subtitles.TextAt(patchSubs.SoundSubtitle[0].Content, 100));
Equal("", Wa2Subtitles.TextAt(patchSubs.SoundSubtitle[0].Content, 200));
Equal("drama", Wa2Subtitles.TextAt(patchSubs.VoiceDrama[0].Content, 175));
Equal("song", Wa2Subtitles.TextAt(patchSubs.Song[0].Content, 175));
Equal("", Wa2Subtitles.TextAt(patchSubs.Song[0].Content, 300));
Equal("", Wa2Subtitles.TextAt(patchSubs.Song[0].Content, 0)); // Seek back clears text.
try { Wa2Subtitles.Parse("voice: [{id: 2, content: []}]"); throw new Exception("Missing scene accepted"); }
catch (InvalidDataException) { }
try { Wa2Subtitles.Parse("soundEffect: [{id: 2, content: [{begin: 20, end: 10, text: bad}]}]"); throw new Exception("Invalid timing accepted"); }
catch (InvalidDataException) { }
Console.WriteLine("13 upstream subtitle schema/timeline cases passed.");

if (args.Length == 1)
{
    foreach (string edition in new[] { "main", "special" })
    {
        var bundled = Wa2Subtitles.Parse(File.ReadAllText(Path.Combine(args[0], edition + ".zh-Hans.yaml")));
        Console.WriteLine($"Bundled {edition}: SE={bundled.SoundSubtitle.Count}, voice={bundled.VoiceSubtitle.Count}, drama={bundled.VoiceDrama.Count}, song={bundled.Song.Count}");
        Equal(true, edition == "main" ? bundled.VoiceSubtitle.Count > 0 : bundled.VoiceDrama.Count > 0);
    }
}

Equal("script_cn.pak", Wa2Content.ScriptArchive(true, "zh-Hans"));
Equal("ck-gal.pak", Wa2Content.ScriptArchive(false, "zh-Hans"));
Equal("script.pak", Wa2Content.ScriptArchive(true, "ja"));
Equal(12, Wa2Content.SpecialEntries.Count);
Equal("6001", Wa2Content.SpecialEntries[0].Script);
Equal("Kazusa mini-after", Wa2Content.SpecialEntries[0].Label("en"));
Equal("special/zh-Hans", Wa2Content.SaveProfile(true, "zh-Hans"));
Equal("ja", Wa2Content.SaveProfile(false, "ja"));
Console.WriteLine("8 edition/content cases passed.");

Equal("write", Wa2LaunchOptions.Parse(new[] { "--play", "--start-script=6101", "--verify-save=write" }, "/resources").SaveProbePhase);
Equal("read", Wa2LaunchOptions.Parse(new[] { "--play", "--start-script=6101", "--verify-save", "read" }, "/resources").SaveProbePhase);
Reject(new[] { "--play", "--start-script=6101", "--verify-save=invalid" });
Reject(new[] { "--verify-save=write" });
Reject(new[] { "--play", "--verify-save=read" });
Console.WriteLine("5 persistence-probe option cases passed.");

// Completion flags, not movie playback or merely starting a route, unlock chapters.
Equal(true, Wa2Progression.CanStart("1001", _ => 0));
Equal(false, Wa2Progression.CanStart("2001", _ => 0));
Equal(false, Wa2Progression.CanStart("3001", _ => 0));
Equal(true, Wa2Progression.CanStart("2001", flag => flag == 0 ? 1 : 0));
Equal(false, Wa2Progression.CanStart("3001", flag => flag == 0 ? 1 : 0));
Equal(true, Wa2Progression.CanStart("3001", flag => flag is 0 or 1 ? 1 : 0));
Equal(false, Wa2Progression.CanStart("3001", flag => flag == 1 ? 1 : 0));
Equal(false, Wa2Progression.CanStart("3001", flag => flag is 202 or 210 or 220 ? 1 : 0));
Equal(false, Wa2Progression.CanStart("2001", flag => flag == 0 ? 2 : 0));
Equal(false, Wa2Progression.CanStart("unknown", _ => 1));
Console.WriteLine("10 chapter progression cases passed.");

var focusItems = new[] {
    new Wa2FocusRect(0, 0, 100, 40), new Wa2FocusRect(120, 0, 100, 40),
    new Wa2FocusRect(0, 60, 100, 40), new Wa2FocusRect(120, 60, 100, 40)
};
Equal(1, Wa2FocusNavigation.Next(focusItems, 0, 1, 0));
Equal(2, Wa2FocusNavigation.Next(focusItems, 0, 0, 1));
Equal(0, Wa2FocusNavigation.Next(focusItems, 2, 0, -1));
Equal(2, Wa2FocusNavigation.Next(focusItems, 3, -1, 0));
Equal(0, Wa2FocusNavigation.Next(focusItems, 0, -1, 0));
Equal(0, Wa2FocusNavigation.Next(focusItems, -1, 0, 1));
Equal(-1, Wa2FocusNavigation.Next(Array.Empty<Wa2FocusRect>(), -1, 0, 1));
Equal(1, Wa2FocusNavigation.Next(new[] {
    new Wa2FocusRect(0, 0, 300, 40), new Wa2FocusRect(0, 100, 300, 40),
    new Wa2FocusRect(340, 55, 40, 40)
}, 0, 0, 1));
Console.WriteLine("8 spatial focus cases passed.");

var localFiles = new System.Collections.Generic.Dictionary<string, byte[]> {
    ["saves/ja/sys.sav"] = new byte[0x26A000],
    ["saves/special/zh-Hans/sav00.sav"] = new byte[] { 1, 2, 3, 4 },
    ["launcher.cfg"] = System.Text.Encoding.UTF8.GetBytes("[game]\nlanguage=\"ja\"\n")
};
var snapshot = Wa2LocalSnapshot.Pack(localFiles);
var restored = Wa2LocalSnapshot.Unpack(snapshot);
Equal(3, restored.Count);
Equal(0x26A000, restored["saves/ja/sys.sav"].Length);
Equal("01-02-03-04", BitConverter.ToString(restored["saves/special/zh-Hans/sav00.sav"]));
Equal(false, Wa2LocalSnapshot.IsSavePath("saves/ja/../sys.sav"));
Equal(false, Wa2LocalSnapshot.IsSavePath("Wa2Res/script.pak"));
Equal(false, Wa2LocalSnapshot.IsSavePath("saves/ja/sav00.sav.pending"));
Equal(true, Wa2LocalSnapshot.IsSavePath("saves/zh-Hant/SYSTEM.ini"));
try { Wa2LocalSnapshot.Pack(new System.Collections.Generic.Dictionary<string, byte[]> { ["../file"] = new byte[1] }); throw new Exception("Invalid snapshot path accepted"); }
catch (InvalidDataException) { }
var noise = new byte[8192]; new Random(42).NextBytes(noise);
try { Wa2LocalSnapshot.Pack(new System.Collections.Generic.Dictionary<string, byte[]> { ["saves/ja/sav00.sav"] = noise }, 128); throw new Exception("Snapshot quota exceeded without error"); }
catch (IOException) { }
try { Wa2LocalSnapshot.Unpack(snapshot[..(snapshot.Length / 2)]); throw new Exception("Truncated snapshot accepted"); }
catch (InvalidDataException) { }
Console.WriteLine("10 local snapshot cases passed.");

var lru = new Wa2LruCache<string>(100);
lru.Add("a", "A", 40); lru.Add("b", "B", 40);
Equal(true, lru.TryGet("a", out _));
lru.Add("c", "C", 40);
Equal(false, lru.TryGet("b", out _));
Equal(true, lru.TryGet("a", out string cachedA)); Equal("A", cachedA);
Equal(80L, lru.Bytes);
lru.Add("huge", "H", 101);
Equal(false, lru.TryGet("huge", out _));
lru.Add("a", "A2", 10);
Equal(true, lru.TryGet("a", out string replaced)); Equal("A2", replaced);
Equal(50L, lru.Bytes);
lru.Clear();
Equal(0, lru.Count);
Console.WriteLine("9 texture cache cases passed.");

var filesA = new System.Collections.Generic.Dictionary<string, byte[]> { ["saves/ja/sys.sav"] = new byte[] { 1, 2 }, ["launcher.cfg"] = new byte[] { 3 } };
var filesB = new System.Collections.Generic.Dictionary<string, byte[]> { ["launcher.cfg"] = new byte[] { 3 }, ["saves/ja/sys.sav"] = new byte[] { 1, 2 } };
Equal(Wa2LocalSnapshot.ContentHash(filesA), Wa2LocalSnapshot.ContentHash(filesB));
filesB["saves/ja/sys.sav"] = new byte[] { 1, 3 };
Equal(false, Wa2LocalSnapshot.ContentHash(filesA) == Wa2LocalSnapshot.ContentHash(filesB));
var filesC = new System.Collections.Generic.Dictionary<string, byte[]> { ["saves/ja/sys.sav"] = new byte[] { 1 }, ["launcher.cfg"] = new byte[] { 2, 3 } };
Equal(false, Wa2LocalSnapshot.ContentHash(filesA) == Wa2LocalSnapshot.ContentHash(filesC));
Console.WriteLine("3 snapshot change-detection cases passed.");

var small = new Wa2FocusRect(100, 100, 48, 24);
var grown = Wa2TouchInput.Expanded(small);
Equal(new Wa2FocusRect(92, 80, 64, 64), grown);
Equal(new Wa2FocusRect(0, 0, 100, 80), Wa2TouchInput.Expanded(new Wa2FocusRect(0, 0, 100, 80)));
var none = Array.Empty<Wa2FocusRect>();
Equal(true, Wa2TouchInput.Hit(small, 110, 110, none));
Equal(true, Wa2TouchInput.Hit(small, 95, 85, none));
Equal(false, Wa2TouchInput.Hit(small, 91, 85, none));
// A neighbour directly below keeps its whole real area.
var below = new Wa2FocusRect(100, 124, 48, 24);
Equal(false, Wa2TouchInput.Hit(small, 110, 130, new[] { below }));
Equal(true, Wa2TouchInput.Hit(small, 110, 90, new[] { below }));
float accumulated = 0;
Equal(0, Wa2TouchInput.DragSteps(ref accumulated, 30));
Equal(-1, Wa2TouchInput.DragSteps(ref accumulated, 30));
Equal(4f, accumulated);
accumulated = 0;
Equal(2, Wa2TouchInput.DragSteps(ref accumulated, -120));
Console.WriteLine("11 touch target and drag cases passed.");

var gesture = new Wa2DialogueTouch();
gesture.Begin(500, 610);
Equal(true, gesture.End(504, 608)); // Finger jitter still counts as one tap.
Equal(false, gesture.Active);
gesture.Begin(500, 610);
Equal(false, gesture.Move(503, 640)); // Wait for a deliberate downward swipe.
Equal(true, gesture.Move(505, 658));
Equal(false, gesture.Move(505, 680)); // Open history at most once per press.
Equal(false, gesture.End(505, 680)); // A swipe never also advances the text.
gesture.Begin(500, 610);
Equal(false, gesture.Move(550, 610));
Equal(false, gesture.Move(550, 710)); // A horizontal drag cannot become a swipe.
Equal(false, gesture.End(550, 710));
gesture.Begin(500, 610);
Equal(false, gesture.Move(500, 580));
Equal(false, gesture.Move(500, 680)); // Nor can an upward drag reverse into one.
Equal(false, gesture.End(500, 680));
gesture.Begin(500, 610);
gesture.Cancel(); // Cancellation, another finger or long-press skip.
Equal(false, gesture.Move(500, 680));
Equal(false, gesture.End(500, 610));
gesture.Begin(500, 610);
gesture.Move(520, 610);
Equal(false, gesture.End(500, 610)); // Moving away then back is not a tap.
gesture.Begin(500, 610);
gesture.Reset(); // A menu or focus change ends the pending gesture.
Equal(false, gesture.End(500, 610));
gesture.Begin(500, 610);
Equal(true, gesture.End(500, 610)); // A fresh gesture works after reset.
gesture.Begin(500, 300);
Equal(false, gesture.Move(500, 240));
Equal(false, gesture.End(500, 240)); // Upward release cannot open history or tap.
gesture.Begin(500, 300);
Equal(true, gesture.Move(500, 348)); // Same distance works above the dialogue box.
Equal(false, gesture.End(500, 348));
Console.WriteLine("21 dialogue gesture cases passed.");
