// SPDX-License-Identifier: Apache-2.0
using System.Collections.Generic;

public sealed record Wa2ContentEntry(string Script, string Japanese, string Chinese, string English)
{
    public string Label(string language) => language switch
    {
        "ja" => Japanese,
        "zh-Hans" or "zh-Hant" => Chinese,
        _ => English
    };
}

public static class Wa2Content
{
    public static string ScriptArchive(bool special, string language) => language == "zh-Hans"
        ? special ? "script_cn.pak" : "ck-gal.pak" : "script.pak";
    public static string SaveProfile(bool special, string language) => special ? "special/" + language : language;

    public static IReadOnlyList<Wa2ContentEntry> SpecialEntries { get; } = new[]
    {
        new Wa2ContentEntry("6001", "かずさ mini-after", "和纱 mini-after", "Kazusa mini-after"),
        new Wa2ContentEntry("6101", "雪菜 mini-after", "雪菜 mini-after", "Setsuna mini-after"),
        new Wa2ContentEntry("4000", "エクストラエピソード", "追加篇章", "Extra episode"),
        new Wa2ContentEntry("5400", "デジタルノベル 1", "数字小说 1", "Digital novel 1"),
        new Wa2ContentEntry("5000", "デジタルノベル 2", "数字小说 2", "Digital novel 2"),
        new Wa2ContentEntry("5300", "デジタルノベル 3", "数字小说 3", "Digital novel 3"),
        new Wa2ContentEntry("5200", "デジタルノベル 4", "数字小说 4", "Digital novel 4"),
        new Wa2ContentEntry("5100", "デジタルノベル 5", "数字小说 5", "Digital novel 5"),
        new Wa2ContentEntry("7300", "デジタルノベル 6", "数字小说 6", "Digital novel 6"),
        new Wa2ContentEntry("7000", "デジタルノベル 7", "数字小说 7", "Digital novel 7"),
        new Wa2ContentEntry("7100", "デジタルノベル 8", "数字小说 8", "Digital novel 8"),
        new Wa2ContentEntry("7200", "デジタルノベル 9", "数字小说 9", "Digital novel 9")
    };
}
