// SPDX-License-Identifier: Apache-2.0
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using YamlDotNet.RepresentationModel;

public sealed class ContentSegment
{
    public int Begin { get; set; }
    public int End { get; set; }
    public string Text { get; set; }
}
public sealed class SoundSubtitle
{
    public int Id { get; set; }
    public List<ContentSegment> Content { get; set; } = new();
}
public sealed class VoiceSubtitle
{
    public int Id { get; set; }
    public int Scene { get; set; }
    public List<ContentSegment> Content { get; set; } = new();
}
public sealed class SubtitleRoot
{
    public List<SoundSubtitle> SoundSubtitle { get; set; } = new();
    public List<VoiceSubtitle> VoiceSubtitle { get; set; } = new();
}

// Parse known fields explicitly; no reflection, runtime code generation or type discovery.
public static class Wa2Subtitles
{
    public static SubtitleRoot Parse(string text)
    {
        var yaml = new YamlStream();
        yaml.Load(new StringReader(text));
        if (yaml.Documents.Count != 1 || yaml.Documents[0].RootNode is not YamlMappingNode root)
            throw new InvalidDataException("Subtitle data must contain one YAML mapping.");
        var result = new SubtitleRoot();
        foreach (YamlNode node in Sequence(root, "soundSubtitle"))
        {
            var item = Mapping(node);
            result.SoundSubtitle.Add(new SoundSubtitle { Id = Integer(item, "id"), Content = Content(item) });
        }
        foreach (YamlNode node in Sequence(root, "voiceSubtitle"))
        {
            var item = Mapping(node);
            result.VoiceSubtitle.Add(new VoiceSubtitle { Id = Integer(item, "id"), Scene = Integer(item, "scene"), Content = Content(item) });
        }
        return result;
    }

    private static List<ContentSegment> Content(YamlMappingNode parent)
    {
        var content = new List<ContentSegment>();
        foreach (YamlNode node in Sequence(parent, "content"))
        {
            var item = Mapping(node);
            content.Add(new ContentSegment { Begin = Integer(item, "begin"), End = Integer(item, "end"), Text = Scalar(item, "text") });
        }
        return content;
    }

    private static YamlMappingNode Mapping(YamlNode node) => node as YamlMappingNode
        ?? throw new InvalidDataException("Subtitle entry must be a mapping.");

    private static IEnumerable<YamlNode> Sequence(YamlMappingNode parent, string key)
    {
        if (!parent.Children.TryGetValue(new YamlScalarNode(key), out YamlNode node))
            return System.Array.Empty<YamlNode>();
        return node is YamlSequenceNode sequence ? sequence.Children
            : throw new InvalidDataException($"Subtitle '{key}' must be a sequence.");
    }

    private static string Scalar(YamlMappingNode parent, string key)
    {
        if (parent.Children.TryGetValue(new YamlScalarNode(key), out YamlNode node) && node is YamlScalarNode scalar)
            return scalar.Value ?? "";
        throw new InvalidDataException($"Subtitle entry is missing scalar '{key}'.");
    }

    private static int Integer(YamlMappingNode parent, string key) =>
        int.TryParse(Scalar(parent, key), NumberStyles.Integer, CultureInfo.InvariantCulture, out int number) ? number
            : throw new InvalidDataException($"Subtitle '{key}' must be an integer.");
}
