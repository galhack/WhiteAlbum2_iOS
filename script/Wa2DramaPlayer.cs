// SPDX-License-Identifier: Apache-2.0
using Godot;
using System;
using System.Collections.Generic;

// Special Contents radio dramas are separate BGM chapter files, not VM scripts.
public partial class Wa2DramaPlayer : PanelContainer
{
    private readonly List<(int Id, int Chapter, string File)> _chapters = new();
    private readonly Wa2Audio _audio = new() { Name = "DramaAudio", Bus = "BGM" };
    private Label _dialogue, _lyrics, _status;
    private int _current = -1;

    public override void _Ready()
    {
        Name = "DramaPlayer";
        Theme = Wa2UiMetrics.CreateTheme();
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var margin = new MarginContainer();
        foreach (string edge in new[] { "left", "top", "right", "bottom" }) margin.AddThemeConstantOverride("margin_" + edge, 32);
        AddChild(margin);
        var column = new VBoxContainer(); margin.AddChild(column);
        column.AddChild(new Label { Text = "Special Contents · 广播剧 / Radio dramas", HorizontalAlignment = HorizontalAlignment.Center });
        var body = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill }; column.AddChild(body);
        var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(380, 0) }; body.AddChild(scroll);
        var chapters = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; scroll.AddChild(chapters);
        for (int id = 80; id <= 85; id++)
        {
            for (int chapter = 0; chapter < 26; chapter++)
            {
                string file = $"bgm_{id:D3}_{(char)('a' + chapter)}.ogg";
                if (chapter == 0 && Wa2Resource.FileDic.ContainsKey($"bgm_{id:D3}.ogg")) file = $"bgm_{id:D3}.ogg";
                if (!Wa2Resource.FileDic.ContainsKey(file)) break;
                int index = _chapters.Count;
                _chapters.Add((id, chapter, file));
                AddButton(chapters, $"Drama {id - 79} · 第 {chapter + 1} 章", () => PlayChapter(index), $"Drama{id}Chapter{chapter}");
            }
        }
        var captions = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, Alignment = BoxContainer.AlignmentMode.Center };
        captions.AddThemeConstantOverride("separation", 30); body.AddChild(captions);
        _status = Caption(); captions.AddChild(_status);
        _dialogue = Caption(); _dialogue.Name = "DramaCaption"; captions.AddChild(_dialogue);
        _lyrics = Caption(); _lyrics.Name = "SongCaption"; _lyrics.AddThemeColorOverride("font_color", new Color(0.4f, 0.8f, 1)); captions.AddChild(_lyrics);
        var controls = new HBoxContainer(); column.AddChild(controls);
        AddButton(controls, "Pause / 继续·暂停", TogglePause, "PauseDrama");
        AddButton(controls, "Restart / 重播", () => { if (_current >= 0) PlayChapter(_current); }, "RestartDrama");
        AddButton(controls, "Next / 下一章", Next, "NextDrama");
        AddButton(controls, "Back / 返回", Close, "BackDrama");
        AddChild(_audio); _audio.Finished += Next;
        Hide();
    }

    private static Label Caption() => new() { AutowrapMode = TextServer.AutowrapMode.WordSmart,
        SizeFlagsHorizontal = SizeFlags.ExpandFill, HorizontalAlignment = HorizontalAlignment.Center, CustomMinimumSize = new Vector2(0, 100) };

    private static void AddButton(Container parent, string text, Action action, string name)
    {
        var button = new Button { Text = text, Name = name, CustomMinimumSize = Wa2UiMetrics.ButtonSize, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        button.Pressed += action; parent.AddChild(button);
    }

    public void Open()
    {
        Wa2EngineMain.Engine.SoundMgr.StopAll();
        Wa2EngineMain.Engine.UiMgr.UiQueue.Push(this);
        Show();
    }

    public void PlayChapter(int index)
    {
        if (index < 0 || index >= _chapters.Count) return;
        var chapter = _chapters[index];
        // Decode before replacing the current track so failure leaves playback intact.
        var stream = Wa2Resource.GetOggStream(chapter.File);
        if (stream == null) { _status.Text = "无法读取广播剧音轨 / Cannot load drama audio."; return; }
        _audio.StreamPaused = false;
        _audio.StopStream(0);
        _audio.Stream = stream;
        _audio.SetVolume(Wa2EngineMain.Engine.Prefs.GetConfig("bgm_vol"), 0);
        _audio.Play(); _current = index;
        Wa2EngineMain.Engine.SubtitleMgr.ListenDrama(chapter.Id, chapter.Chapter, _audio, _dialogue, _lyrics);
        _status.Text = $"Drama {chapter.Id - 79} · 第 {chapter.Chapter + 1} 章";
        if (Wa2GameData.Current.Language == "zh-Hans" &&
            !Wa2EngineMain.Engine.SubtitleMgr.Data.VoiceDrama.Exists(s => s.Id == chapter.Id && s.Chapter == chapter.Chapter))
            _status.Text += "\n原补丁未提供本章字幕 / No captions supplied for this chapter";
    }

    public void TogglePause() { if (_current >= 0) _audio.StreamPaused = !_audio.StreamPaused; }

    private void Next()
    {
        if (_current >= 0 && _current + 1 < _chapters.Count && _chapters[_current + 1].Id == _chapters[_current].Id)
            PlayChapter(_current + 1);
        else { _audio.StopStream(0); _status.Text = "播放完毕 / Finished"; }
    }

    public void Close()
    {
        _audio.StreamPaused = false; _audio.StopStream(0);
        Wa2EngineMain.Engine.SubtitleMgr.StopListening(_audio);
        Wa2EngineMain.Engine.UiMgr.ReturnScene();
        QueueFree();
    }
}
