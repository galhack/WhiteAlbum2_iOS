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
        new Wa2ContentEntry("5400", "彼の神様、あいつの救世主", "他的神明、那家伙的救世主", "His God, Her Savior"),
        new Wa2ContentEntry("5000", "雪が解け、そして雪が降るまで", "雪融化，然后直到再次飘落时", "Until the Snow Melts and Falls Again"),
        new Wa2ContentEntry("5300", "祭りの後～雪菜の三十分～", "祭典之后～雪菜的三十分钟～", "After the Festival ~ Setsuna's Thirty Minutes ~"),
        new Wa2ContentEntry("5200", "Twinkle Snow～夢想～", "Twinkle Snow～梦想～", "Twinkle Snow ~ Reverie ~"),
        new Wa2ContentEntry("5100", "歌を忘れた偶像（アイドル）", "忘却歌声的偶像", "The Idol Who Forgot How to Sing"),
        new Wa2ContentEntry("7300", "届かない恋、届いた", "传达不到的爱恋，传达到了", "An Unreachable Love, Reached"),
        new Wa2ContentEntry("7000", "雪菜姫の受難と大臣の悪巧み", "雪菜公主的受难与大臣的计谋", "Princess Setsuna's Ordeal and Her Minister's Schemes"),
        new Wa2ContentEntry("7100", "台風一過の小春日和", "台风过后的小春日和", "Koharu's Fair Weather After the Typhoon"),
        new Wa2ContentEntry("7200", "もう、ホワイトアルバムの季節じゃない", "已经不再是白色相簿的季节了", "It's No Longer the Season of White Album")
    };
}
