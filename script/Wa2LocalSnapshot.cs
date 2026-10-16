// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

// A bounded, local-only archive for tvOS UserDefaults. Never contains game assets.
public static class Wa2LocalSnapshot
{
    public const int SaveBudget = 384 * 1024;
    public const int ProgressBudget = 448 * 1024;
    private const int MaxFileBytes = 16 * 1024 * 1024;
    private const int MaxTotalBytes = 128 * 1024 * 1024;
    private const int MaxFiles = 820;
    private const string Magic = "WA2LOCAL1";

    public static bool IsSavePath(string path)
    {
        if (path == "launcher.cfg") return true;
        var parts = path.Split('/');
        if (parts.Length != 3 && parts.Length != 4) return false;
        if (parts[0] != "saves" || (parts.Length == 4 && parts[1] != "special")) return false;
        if (parts[^2] is not ("ja" or "zh-Hans" or "en" or "zh-Hant")) return false;
        string name = parts[^1];
        return name is "sys.sav" or "SYSTEM.ini" || (name.Length == 9 && name.StartsWith("sav", StringComparison.Ordinal)
            && name.EndsWith(".sav", StringComparison.Ordinal) && name[3] is >= '0' and <= '9' && name[4] is >= '0' and <= '9');
    }

    // Order-independent content digest used to skip rewriting an unchanged snapshot.
    public static string ContentHash(IReadOnlyDictionary<string, byte[]> files)
    {
        using var hash = System.Security.Cryptography.IncrementalHash.CreateHash(System.Security.Cryptography.HashAlgorithmName.SHA256);
        foreach (var entry in files.OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            byte[] name = Encoding.UTF8.GetBytes(entry.Key);
            hash.AppendData(BitConverter.GetBytes(name.Length));
            hash.AppendData(name);
            hash.AppendData(BitConverter.GetBytes(entry.Value.Length));
            hash.AppendData(entry.Value);
        }
        return Convert.ToHexString(hash.GetHashAndReset());
    }

    public static byte[] Pack(IReadOnlyDictionary<string, byte[]> files, int budget = SaveBudget)
    {
        if (files.Count > MaxFiles) throw new InvalidDataException("Too many local save files.");
        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, CompressionLevel.SmallestSize, true))
        using (var writer = new BinaryWriter(gzip, Encoding.UTF8, true))
        {
            writer.Write(Encoding.ASCII.GetBytes(Magic));
            writer.Write(files.Count);
            long total = 0;
            foreach (var entry in files.OrderBy(p => p.Key, StringComparer.Ordinal))
            {
                byte[] name = Encoding.UTF8.GetBytes(entry.Key);
                if (!IsSavePath(entry.Key) || name.Length > 160) throw new InvalidDataException("Invalid local save path: " + entry.Key);
                total += entry.Value.Length;
                if (entry.Value.Length > MaxFileBytes || total > MaxTotalBytes) throw new InvalidDataException("Local save data exceeds snapshot bounds.");
                writer.Write(name.Length);
                writer.Write(name);
                writer.Write(entry.Value.Length);
                writer.Write(entry.Value);
            }
        }
        if (output.Length > budget)
            throw new IOException("Apple TV 本地持久存储已满，请覆盖现有存档槽。原存档仍保留。 / Local save storage is full; overwrite an existing slot.");
        return output.ToArray();
    }

    public static Dictionary<string, byte[]> Unpack(byte[] snapshot)
    {
        if (snapshot.Length > ProgressBudget) throw new InvalidDataException("Local snapshot exceeds storage limit.");
        try
        {
            using var input = new MemoryStream(snapshot, false);
            using var gzip = new GZipStream(input, CompressionMode.Decompress);
            using var reader = new BinaryReader(gzip, new UTF8Encoding(false, true));
            if (Encoding.ASCII.GetString(ReadExactly(reader, Magic.Length)) != Magic)
                throw new InvalidDataException("Unrecognized local snapshot.");
            int count = reader.ReadInt32();
            if (count < 0 || count > MaxFiles) throw new InvalidDataException("Invalid local snapshot count.");
            long total = 0;
            var result = new Dictionary<string, byte[]>(StringComparer.Ordinal);
            for (int i = 0; i < count; i++)
            {
                int nameSize = reader.ReadInt32();
                if (nameSize < 1 || nameSize > 160) throw new InvalidDataException("Invalid local snapshot path size.");
                string name = new UTF8Encoding(false, true).GetString(ReadExactly(reader, nameSize));
                if (!IsSavePath(name) || result.ContainsKey(name)) throw new InvalidDataException("Invalid or duplicate local save path.");
                int size = reader.ReadInt32();
                total += size;
                if (size < 0 || size > MaxFileBytes || total > MaxTotalBytes) throw new InvalidDataException("Invalid local save size.");
                result.Add(name, ReadExactly(reader, size));
            }
            if (gzip.ReadByte() != -1) throw new InvalidDataException("Trailing local snapshot data.");
            return result;
        }
        catch (EndOfStreamException ex) { throw new InvalidDataException("Truncated local snapshot.", ex); }
        catch (DecoderFallbackException ex) { throw new InvalidDataException("Invalid local snapshot text.", ex); }
    }

    private static byte[] ReadExactly(BinaryReader reader, int count)
    {
        byte[] bytes = reader.ReadBytes(count);
        if (bytes.Length != count) throw new EndOfStreamException();
        return bytes;
    }
}
