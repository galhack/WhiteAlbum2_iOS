// SPDX-License-Identifier: Apache-2.0
using System;

public static class Wa2Progression
{
    // Original scripts 1013 and 2413 write these persistent completion flags.
    // Movie playback flags (202/210/220) do not represent chapter completion.
    public const int IntroductoryComplete = 0;
    public const int ClosingComplete = 1;

    public static bool CanStart(string script, Func<int, int> readFlag) => script switch
    {
        "1001" => true,
        "2001" => readFlag(IntroductoryComplete) == 1,
        "3001" => readFlag(IntroductoryComplete) == 1 && readFlag(ClosingComplete) == 1,
        _ => false
    };
}
