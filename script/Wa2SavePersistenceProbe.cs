// SPDX-License-Identifier: Apache-2.0
using Godot;
using System;
using System.Security.Cryptography;
using System.Text;

// Opt-in development probe using the real save reader/writer across processes.
public partial class Wa2SavePersistenceProbe : Node
{
    public Wa2EngineMain Game { get; set; }
    public string Phase { get; set; }
    private const int Slot = 9000;
    private const string MarkerPath = "user://port-save-expectation.cfg";
    private const string ResultPath = "user://port-save-result.cfg";
    private double _elapsed;
    private bool _finished;

    public override void _Process(double delta)
    {
        if (_finished) return;
        _elapsed += delta;
        try
        {
            if (!OS.IsDebugBuild()) throw new InvalidOperationException("Persistence probes require a debug build.");
            if (Phase == "read")
            {
                ReadAndVerify();
                Finish("passed");
            }
            else if (Game.AdvMain.State == Wa2AdvMain.AdvState.WAIT_CLICK
                && !string.IsNullOrEmpty(Game.AdvMain.TextLabel.Text)
                && !Game.WaitTimer.IsActive() && !Game.AnimatorMgr.WaitAnimation())
            {
                WriteExpectation();
                Finish("written");
            }
            else if (_elapsed > 300) throw new TimeoutException("No stable dialogue reached within 300 seconds.");
        }
        catch (Exception ex) { Finish("failed", ex.Message); }
    }

    private string SavePath => Game.SavPath + "sav9000.sav";
    private static string TextHash(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));

    private void WriteExpectation()
    {
        if (FileAccess.FileExists(SavePath) || FileAccess.FileExists(MarkerPath))
            throw new InvalidOperationException("Reserved probe save or marker already exists; refusing to overwrite it.");
        var expected = new ConfigFile();
        expected.SetValue("save", "profile", Wa2GameData.Current.SaveProfile);
        expected.SetValue("save", "script", Game.Script.ScriptName);
        expected.SetValue("save", "position", (long)Game.Script.ScriptPos);
        expected.SetValue("save", "text_hash", TextHash(Game.AdvMain.TextLabel.Text));
        expected.SetValue("save", "original_local", Game.Script.GloInts[25]);
        Game.Script.GloInts[25] = 12345;
        Game.GameSav.SaveData(Slot);
        if (!FileAccess.FileExists(SavePath)) throw new InvalidOperationException("Save writer did not create the probe file.");
        Error error = expected.Save(MarkerPath);
        if (error != Error.Ok) throw new InvalidOperationException("Cannot write save expectation: " + error);
    }

    private void ReadAndVerify()
    {
        var expected = new ConfigFile();
        Error error = expected.Load(MarkerPath);
        if (error != Error.Ok) throw new InvalidOperationException("Cannot read save expectation: " + error);
        if (expected.GetValue("save", "profile").AsString() != Wa2GameData.Current.SaveProfile)
            throw new InvalidOperationException("Probe save belongs to a different edition/language profile.");
        if (!FileAccess.FileExists(SavePath)) throw new InvalidOperationException("Probe save is missing after restart.");
        Game.GameSav.LoadData(Slot);
        if (Game.Script.ScriptName != expected.GetValue("save", "script").AsString()
            || Game.Script.ScriptPos != expected.GetValue("save", "position").AsInt64()
            || TextHash(Game.AdvMain.TextLabel.Text) != expected.GetValue("save", "text_hash").AsString()
            || Game.Script.GloInts[25] != 12345)
            throw new InvalidOperationException("Restarted save did not restore script, position, dialogue and local state.");
        Game.Script.GloInts[25] = expected.GetValue("save", "original_local").AsInt32();
        if (DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(SavePath)) != Error.Ok
            || DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(MarkerPath)) != Error.Ok)
            throw new InvalidOperationException("Save verification passed but probe cleanup failed.");
    }

    private void Finish(string status, string message = "")
    {
        _finished = true;
        var result = new ConfigFile();
        result.SetValue("test", "status", status);
        result.SetValue("test", "phase", Phase);
        result.SetValue("test", "message", message);
        result.SetValue("test", "profile", Wa2GameData.Current.SaveProfile);
        result.SetValue("test", "process", OS.GetProcessId());
        Error error = result.Save(ResultPath);
        if (error != Error.Ok) { GD.PrintErr("Cannot write persistence result: " + error); status = "failed"; }
        GD.Print($"WA2_PERSISTENCE {Phase}: {status} {message}");
        GetTree().Quit(status == "failed" ? 1 : 0);
    }
}
