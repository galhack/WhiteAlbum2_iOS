// SPDX-License-Identifier: Apache-2.0
using Godot;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Collections.Generic;

public partial class Wa2TvStorage : Node
{
#if GODOT_TVOS
    static Wa2TvStorage()
    {
        // NativeAOT treats __Internal as a library name unless explicitly resolved.
        // The storage bridge is linked into the app executable, not the C# dylib.
        NativeLibrary.SetDllImportResolver(typeof(Wa2TvStorage).Assembly,
            (name, assembly, searchPath) => name == "__Internal" ? NativeLibrary.GetMainProgramHandle() : IntPtr.Zero);
    }

    [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl)] private static extern IntPtr wa2_tv_read(out int length);
    [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl)] private static extern int wa2_tv_write(byte[] bytes, int length);
    [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl)] private static extern void wa2_tv_free(IntPtr bytes);
    private static bool _restoreFailed;
    private static string _lastSnapshotHash;
#endif
    public static bool IsEnabled => OS.GetName() == "tvOS";
    private static Label _status;
    private double _elapsed;

    public static string Restore()
    {
#if GODOT_TVOS
        if (!IsEnabled) return "";
        IntPtr pointer = wa2_tv_read(out int length);
        try
        {
            if (length < 0) throw new InvalidDataException("Apple TV local progress could not be read.");
            if (pointer == IntPtr.Zero) return "";
            var bytes = new byte[length];
            Marshal.Copy(pointer, bytes, 0, length);
            // Validate the whole archive before changing any cached save file.
            var files = Wa2LocalSnapshot.Unpack(bytes);
            string root = ProjectSettings.GlobalizePath("user://");
            foreach (var entry in files)
            {
                string path = Path.Combine(root, entry.Key);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllBytes(path + ".restore", entry.Value);
                File.Move(path + ".restore", path, true);
            }
            return "";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _restoreFailed = true;
            GD.PrintErr("WA2 local restore: " + ex);
            return "Apple TV 本地进度恢复失败，已保留原备份。 / " + ex.Message;
        }
        finally { if (pointer != IntPtr.Zero) wa2_tv_free(pointer); }
#else
        return "";
#endif
    }

    public static void Ensure(Window root)
    {
        if (!IsEnabled || root.GetNodeOrNull("TvLocalStorage") != null) return;
        var storage = new Wa2TvStorage { Name = "TvLocalStorage" };
        root.AddChild(storage);
        var layer = new CanvasLayer { Layer = 101 };
        storage.AddChild(layer);
        _status = new Label { Position = new Vector2(28, 24), Size = new Vector2(1220, 80),
            AutowrapMode = TextServer.AutowrapMode.WordSmart, MouseFilter = Control.MouseFilterEnum.Ignore,
            Theme = new Theme { DefaultFont = ResourceLoader.Load<Font>("res://assets/ui_fonts/NotoSansCJKsc-Regular.otf") } };
        _status.AddThemeColorOverride("font_color", new Color(1, 0.8f, 0.2f));
        layer.AddChild(_status);
    }

    public override void _Process(double delta)
    {
        _elapsed += delta;
        if (_elapsed < 30) return;
        _elapsed = 0;
        TryPersist();
    }

    public override void _Notification(int what)
    {
        if (what == NotificationApplicationFocusOut || what == NotificationWMCloseRequest) TryPersist();
    }

    private static void WriteSnapshot(string candidate = null, string destination = null, string excluded = null)
    {
#if GODOT_TVOS
        if (!IsEnabled) return;
        if (_restoreFailed) throw new IOException("本地备份恢复失败，已停止覆盖。请先在 Mac 备份并检查数据。 / Local restore failed; backup retained.");
        Wa2EngineMain.Engine?.SysSav?.Flush();
        string root = ProjectSettings.GlobalizePath("user://");
        var files = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        var directory = new DirectoryInfo(Path.Combine(root, "saves"));
        if (directory.Exists)
        {
            foreach (var file in directory.EnumerateFiles("*", SearchOption.AllDirectories))
            {
                string relative = Path.GetRelativePath(root, file.FullName).Replace(Path.DirectorySeparatorChar, '/');
                if (Wa2LocalSnapshot.IsSavePath(relative) && file.FullName != destination && relative != excluded)
                    files.Add(relative, File.ReadAllBytes(file.FullName));
            }
        }
        if (File.Exists(Path.Combine(root, "launcher.cfg"))) files["launcher.cfg"] = File.ReadAllBytes(Path.Combine(root, "launcher.cfg"));
        if (candidate != null)
        {
            string relative = Path.GetRelativePath(root, destination).Replace(Path.DirectorySeparatorChar, '/');
            if (!Wa2LocalSnapshot.IsSavePath(relative)) throw new InvalidDataException("Unsupported local save destination.");
            files[relative] = File.ReadAllBytes(candidate);
        }
        // Periodic persistence runs on the main thread; skip compression and the
        // preferences write when the save files are byte-for-byte unchanged.
        string hash = Wa2LocalSnapshot.ContentHash(files);
        if (hash == _lastSnapshotHash) return;
        var snapshot = Wa2LocalSnapshot.Pack(files, candidate == null ? Wa2LocalSnapshot.ProgressBudget : Wa2LocalSnapshot.SaveBudget);
        if (wa2_tv_write(snapshot, snapshot.Length) != 1) throw new IOException("Apple TV 本地持久存储已满，原备份仍保留。 / Local persistent storage is full.");
        _lastSnapshotHash = hash;
#endif
    }

    public static void CommitSave(string pending, string destination)
    {
        try
        {
            WriteSnapshot(pending, destination);
            File.Move(pending, destination, true);
            if (_status != null) _status.Text = "";
        }
        finally { if (File.Exists(pending)) File.Delete(pending); }
    }

    public static void DeleteSave(string relative)
    {
        if (!Wa2LocalSnapshot.IsSavePath(relative) || !Path.GetFileName(relative).StartsWith("sav", StringComparison.Ordinal))
            throw new InvalidDataException("Only manual save slots can be removed.");
        WriteSnapshot(excluded: relative);
        File.Delete(Path.Combine(ProjectSettings.GlobalizePath("user://"), relative));
    }

    public static void TryPersist()
    {
        if (!IsEnabled) return;
        try { WriteSnapshot(); if (_status != null) _status.Text = ""; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            GD.PrintErr("WA2 local progress: " + ex.Message);
            if (_status != null) _status.Text = ex.Message;
        }
    }
}
