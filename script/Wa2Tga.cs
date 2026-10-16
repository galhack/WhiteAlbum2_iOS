// SPDX-License-Identifier: Apache-2.0
using System;
using System.IO;

public static class Wa2Tga
{
    // Godot's TGA importer does not decode Leaf's uncompressed indexed atlases.
    public static bool TryDecodeIndexed(byte[] data, out int width, out int height, out byte[] rgba)
    {
        width = height = 0;
        rgba = null;
        if (data.Length < 18 || data[1] != 1 || data[2] != 1) return false;
        width = data[12] | data[13] << 8;
        height = data[14] | data[15] << 8;
        int first = data[3] | data[4] << 8;
        int count = data[5] | data[6] << 8;
        int depth = data[7] / 8;
        if (width == 0 || height == 0 || width > 16384 || height > 16384 || data[16] != 8 || (depth != 3 && depth != 4))
            throw new InvalidDataException("Unsupported indexed TGA dimensions or palette depth.");
        int paletteStart = 18 + data[0];
        int pixelStart = checked(paletteStart + count * depth);
        int pixels = checked(width * height);
        if (pixelStart > data.Length || pixels > data.Length - pixelStart)
            throw new InvalidDataException("Truncated indexed TGA palette or pixel data.");
        rgba = new byte[checked(pixels * 4)];
        for (int y = 0; y < height; y++)
        {
            int destY = (data[17] & 0x20) != 0 ? y : height - 1 - y;
            for (int x = 0; x < width; x++)
            {
                int index = data[pixelStart + y * width + x] - first;
                if (index < 0 || index >= count) throw new InvalidDataException("Indexed TGA pixel exceeds palette.");
                int source = paletteStart + index * depth;
                int destX = (data[17] & 0x10) != 0 ? width - 1 - x : x;
                int dest = (destY * width + destX) * 4;
                rgba[dest] = data[source + 2];
                rgba[dest + 1] = data[source + 1];
                rgba[dest + 2] = data[source];
                rgba[dest + 3] = depth == 4 ? data[source + 3] : (byte)255;
            }
        }
        return true;
    }
}
