// Apple port: select optional subtitles by edition and language, without AOT reflection.
using System.Collections.Generic;
using Godot;

public partial class SubtitleMgr : Node
{
  public List<SoundSubtitle> SoundSubtitleList = new();
  public List<VoiceSubtitle> VoiceSubtitleList = new();
  public Wa2Audio ListenAudio;
  public List<ContentSegment> ListenContent;
  [Export]
  public Label TextLabel;
  public override void _Ready()
  {
    if (Wa2GameData.Current != null && Wa2GameData.Current.Language != "zh-Hans") return;
    string subtitlePath = "res://assets/sub.yaml";
    if (Wa2GameData.Current != null)
    {
      string external = Wa2GameData.Current.Resolve("subtitles/" + Wa2GameData.Current.Language + ".yaml");
      if (System.IO.File.Exists(external)) subtitlePath = external;
      else if (Wa2GameData.Current.IsSpecialContents) return;
    }
    if (!FileAccess.FileExists(subtitlePath)) return;
    var file = FileAccess.Open(subtitlePath, FileAccess.ModeFlags.Read);
    string yamlText = file.GetAsText();
    file.Close();

    var result = Wa2Subtitles.Parse(yamlText);
    SoundSubtitleList = result.SoundSubtitle ?? new();
    VoiceSubtitleList = result.VoiceSubtitle ?? new();

    // foreach (var se in VoiceSubtitleList)
    // {
    //   GD.Print($"SoundEffect ID: {se.Id}");
    //   foreach (var segment in se.Content)
    //   {
    //     GD.Print($"  {segment.Begin}-{segment.End}: {segment.Text}");
    //   }
    // }
  }
  public void ListenVoice(int scene, int id, Wa2Audio audio)
  {
    for (int i = 0; i < VoiceSubtitleList.Count; i++)
    {
      if (VoiceSubtitleList[i].Id == id && VoiceSubtitleList[i].Scene == scene)
      {
        ListenContent = VoiceSubtitleList[i].Content;
        ListenAudio = audio;
        return;
      }
    }
    if (ListenAudio == null)
    {
      TextLabel.Text = "";
      ListenAudio = null;
      ListenContent = null;
    }
  }
  public void ListenSe(int id, Wa2Audio audio)
  {
    for (int i = 0; i < SoundSubtitleList.Count; i++)
    {
      if (SoundSubtitleList[i].Id == id)
      {
        ListenContent = SoundSubtitleList[i].Content;
        ListenAudio = audio;
        return;
      }
    }
    if (ListenAudio == null)
    {
      TextLabel.Text = "";
      ListenAudio = null;
      ListenContent = null;
    }

  }
  public override void _Process(double delta)
  {
    if (ListenAudio != null && ListenAudio.Stream != null && ListenAudio.Playing && ListenContent != null)
    {
      foreach (ContentSegment segment in ListenContent)
      {
        if (ListenAudio.GetPlaybackPosition() * 1000 >= segment.Begin && ListenAudio.GetPlaybackPosition() * 1000 <= segment.End)
        {
          TextLabel.Text = segment.Text;
        }
      }
    }
    else
    {
      TextLabel.Text = "";
      ListenAudio = null;
      ListenContent = null;
    }
  }
}
