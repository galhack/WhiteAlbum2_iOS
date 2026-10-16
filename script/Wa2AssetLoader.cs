// SPDX-License-Identifier: Apache-2.0
using Godot;
using System;
using System.IO;

// Resolve legacy scene references directly from user-owned archives, before scenes load.
public partial class Wa2AssetLoader : ResourceFormatLoader
{
    private static bool Owns(string path) => path.StartsWith("res://assets/grp/", StringComparison.Ordinal)
        || path.StartsWith("res://assets/fonts/cn/", StringComparison.Ordinal)
        || path.StartsWith("res://assets/fonts/jp/", StringComparison.Ordinal)
        || path.StartsWith("res://assets/se/", StringComparison.Ordinal);

    public override string[] _GetRecognizedExtensions() => new[] { "png", "wav", "WAV" };
    public override bool _RecognizePath(string path, StringName type) => Owns(path);
    public override bool _Exists(string path) => Owns(path);
    public override bool _HandlesType(StringName type) => type == "Texture2D" || type == "AudioStream" || type == "";
    public override string _GetResourceType(string path) => !Owns(path) ? "" : path.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ? "Texture2D" : "AudioStream";

    public override Variant _Load(string path, string originalPath, bool useSubThreads, int cacheMode)
    {
        string name = Path.GetFileName(path);
        if (path.EndsWith(".wav", StringComparison.OrdinalIgnoreCase))
        {
            byte[] sound = Wa2Resource.LoadFileBuffer(name);
            return sound == null ? (Variant)(int)Error.FileNotFound : AudioStreamWav.LoadFromBuffer(sound);
        }

        string fileName = Path.GetFileNameWithoutExtension(name) + ".tga";
        byte[] bytes = null;
        if (path.StartsWith("res://assets/grp/", StringComparison.Ordinal) && Wa2GameData.Current.Language == "zh-Hans")
        {
            string loose = Wa2GameData.Current.Resolve("grp/" + fileName);
            if (System.IO.File.Exists(loose)) bytes = System.IO.File.ReadAllBytes(loose);
        }
        bytes ??= Wa2Resource.LoadFileBuffer(fileName);
        if (bytes == null) return (int)Error.FileNotFound;
        Image image = Wa2Resource.DecodeTga(bytes);
        return ImageTexture.CreateFromImage(image);
    }
}
