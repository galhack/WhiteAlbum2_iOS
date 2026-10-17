// Apple port: synchronized, edition-specific upstream subtitles on every client.
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class SubtitleMgr : Node
{
    public SubtitleRoot Data { get; private set; } = new();
    [Export] public Label TextLabel;
    private sealed record Playback(Wa2Audio Audio, AudioStream Stream, IReadOnlyList<ContentSegment> Dialogue,
        IReadOnlyList<ContentSegment> Song, Label Caption, Label Lyrics);
    private readonly Dictionary<Wa2Audio, Playback> _playing = new();

    public override void _Ready()
    {
        var game = Wa2GameData.Current;
        if (game == null || game.Language != "zh-Hans") return;
        string path = "res://assets/subtitles/" + (game.IsSpecialContents ? "special" : "main") + ".zh-Hans.yaml";
        string external = game.Resolve("subtitles/zh-Hans.yaml");
        if (System.IO.File.Exists(external)) path = external;
        using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
        if (file == null) throw new System.IO.IOException("Cannot open subtitle data: " + path);
        Data = Wa2Subtitles.Parse(file.GetAsText());
        GD.Print($"WA2_SUBTITLES edition={(game.IsSpecialContents ? "special" : "main")} se={Data.SoundSubtitle.Count} voice={Data.VoiceSubtitle.Count} drama={Data.VoiceDrama.Count} song={Data.Song.Count}");
    }

    public void ListenVoice(int scene, int id, Wa2Audio audio) => Attach(audio,
        Data.VoiceSubtitle.FirstOrDefault(s => s.Scene == scene && s.Id == id)?.Content);

    public void ListenSe(int id, Wa2Audio audio) => Attach(audio,
        Data.SoundSubtitle.FirstOrDefault(s => s.Id == id)?.Content);

    public void ListenDrama(int id, int chapter, Wa2Audio audio, Label caption, Label lyrics) => Attach(audio,
        Data.VoiceDrama.FirstOrDefault(s => s.Id == id && s.Chapter == chapter)?.Content,
        Data.Song.FirstOrDefault(s => s.Id == id && s.Chapter == chapter)?.Content, caption, lyrics);

    private void Attach(Wa2Audio audio, IReadOnlyList<ContentSegment> dialogue,
        IReadOnlyList<ContentSegment> song = null, Label caption = null, Label lyrics = null)
    {
        StopListening(audio);
        if (dialogue == null && song == null) return;
        _playing[audio] = new Playback(audio, audio.Stream, dialogue ?? Array.Empty<ContentSegment>(),
            song ?? Array.Empty<ContentSegment>(), caption, lyrics);
    }

    public void StopListening(Wa2Audio audio)
    {
        if (!_playing.Remove(audio, out var previous)) return;
        SetText(previous.Caption, "");
        SetText(previous.Lyrics, "");
    }

    private static void SetText(Label label, string text)
    {
        if (GodotObject.IsInstanceValid(label) && label.Text != text) label.Text = text;
    }

    public override void _Process(double delta)
    {
        var global = new List<string>();
        foreach (var entry in _playing.Values.ToArray())
        {
            var audio = entry.Audio;
            if (!GodotObject.IsInstanceValid(audio) || audio.Stream != entry.Stream || (!audio.Playing && !audio.StreamPaused))
            {
                StopListening(audio);
                continue;
            }
            double time = audio.GetPlaybackPosition() * 1000;
            string text = Wa2Subtitles.TextAt(entry.Dialogue, time);
            if (entry.Caption != null) SetText(entry.Caption, text);
            else if (text.Length > 0) global.Add(text);
            SetText(entry.Lyrics, Wa2Subtitles.TextAt(entry.Song, time));
        }
        SetText(TextLabel, string.Join("\n", global));
    }
}
