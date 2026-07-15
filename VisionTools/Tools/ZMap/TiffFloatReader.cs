using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace VisionTools.Tools.ZMap
{
    /// <summary>
    /// 托管TIFF浮点高度图解码器（HALCON解码崩溃时的兜底方案）。
    /// 仅支持ZMAP常见的导出格式：单通道、32位IEEE浮点(SampleFormat=3)、
    /// 无压缩(Compression=1)，条带(strip)或分块(tile)存储，
    /// 兼容classic TIFF与BigTIFF、小端(II)与大端(MM)。
    /// 同时提供仅解析元数据的诊断能力，用于在解码失败时报告文件结构。
    /// </summary>
    public static class TiffFloatReader
    {
        // TIFF标签号（仅列出解码与诊断所需）
        private const int TagImageWidth = 256;
        private const int TagImageLength = 257;
        private const int TagBitsPerSample = 258;
        private const int TagCompression = 259;
        private const int TagPhotometric = 262;
        private const int TagStripOffsets = 273;
        private const int TagSamplesPerPixel = 277;
        private const int TagRowsPerStrip = 278;
        private const int TagStripByteCounts = 279;
        private const int TagTileWidth = 322;
        private const int TagTileLength = 323;
        private const int TagTileOffsets = 324;
        private const int TagTileByteCounts = 325;
        private const int TagSampleFormat = 339;

        /// <summary>解析出的TIFF首个IFD结构信息（值为0表示标签缺失，按TIFF默认值解释）。</summary>
        private sealed class TiffInfo
        {
            public bool IsBigTiff;
            public bool IsBigEndian;
            public long Width;
            public long Height;
            public long BitsPerSample;
            public long Compression = 1;
            public long Photometric = -1;
            public long SamplesPerPixel = 1;
            public long SampleFormat = 1;
            public long RowsPerStrip;
            public long TileWidth;
            public long TileLength;
            public ulong[] StripOffsets;
            public ulong[] StripByteCounts;
            public ulong[] TileOffsets;
            public ulong[] TileByteCounts;
        }

        /// <summary>
        /// 尝试用托管代码解码浮点TIFF高度图。仅在HALCON读取失败时调用。
        /// 成功返回true并输出行优先float数组；失败返回false及原因。
        /// </summary>
        public static bool TryReadHeightData(
            string filePath,
            out int width,
            out int height,
            out float[] pixels,
            out string error)
        {
            width = 0;
            height = 0;
            pixels = null;

            TiffInfo info;
            byte[] fileBytes;
            if (!TryParse(filePath, out info, out fileBytes, out error))
                return false;

            if (info.Compression != 1)
            {
                error = "托管解码仅支持无压缩TIFF，当前Compression=" + info.Compression;
                return false;
            }
            if (info.SamplesPerPixel != 1)
            {
                error = "托管解码仅支持单通道TIFF，当前SamplesPerPixel=" + info.SamplesPerPixel;
                return false;
            }
            if (info.BitsPerSample != 32 || info.SampleFormat != 3)
            {
                error = string.Format(
                    CultureInfo.InvariantCulture,
                    "托管解码仅支持32位IEEE浮点TIFF，当前BitsPerSample={0}, SampleFormat={1}",
                    info.BitsPerSample, info.SampleFormat);
                return false;
            }
            if (info.Width <= 0 || info.Height <= 0 || info.Width * info.Height > int.MaxValue)
            {
                error = "TIFF尺寸无效或超过处理上限";
                return false;
            }

            width = (int)info.Width;
            height = (int)info.Height;
            pixels = new float[width * height];

            bool isTiled = info.TileOffsets != null && info.TileOffsets.Length > 0;
            bool ok = isTiled
                ? TryCopyTiles(info, fileBytes, pixels, out error)
                : TryCopyStrips(info, fileBytes, pixels, out error);
            if (!ok)
            {
                pixels = null;
                return false;
            }

            return true;
        }

        /// <summary>
        /// 生成TIFF结构摘要（诊断用途）：不解码像素，仅报告首个IFD的关键标签，
        /// 便于定位HALCON崩溃的文件格式原因。解析失败时返回失败原因。
        /// </summary>
        public static string DescribeFile(string filePath)
        {
            try
            {
                TiffInfo info;
                byte[] fileBytes;
                string parseError;
                if (!TryParse(filePath, out info, out fileBytes, out parseError))
                    return "无法解析TIFF结构(" + parseError + ")";

                return string.Format(
                    CultureInfo.InvariantCulture,
                    "{0}, {1}, width={2}, height={3}, bitsPerSample={4}, sampleFormat={5}, " +
                    "compression={6}, samplesPerPixel={7}, photometric={8}, 存储={9}",
                    info.IsBigTiff ? "BigTIFF" : "classic TIFF",
                    info.IsBigEndian ? "大端MM" : "小端II",
                    info.Width, info.Height, info.BitsPerSample, info.SampleFormat,
                    info.Compression, info.SamplesPerPixel, info.Photometric,
                    (info.TileOffsets != null && info.TileOffsets.Length > 0)
                        ? "tile(" + info.TileWidth + "x" + info.TileLength + ")"
                        : "strip(rowsPerStrip=" + info.RowsPerStrip + ")");
            }
            catch (Exception ex)
            {
                return "无法解析TIFF结构(" + ex.Message + ")";
            }
        }

        /// <summary>读取整个文件并解析首个IFD。ZMAP文件通常几十MB内，直接读入内存换取实现简单可靠。</summary>
        private static bool TryParse(string filePath, out TiffInfo info, out byte[] fileBytes, out string error)
        {
            info = null;
            fileBytes = null;
            error = null;

            fileBytes = File.ReadAllBytes(filePath);
            if (fileBytes.Length < 16)
            {
                error = "文件过小，不是有效TIFF";
                return false;
            }

            bool bigEndian;
            if (fileBytes[0] == 0x49 && fileBytes[1] == 0x49)
                bigEndian = false;
            else if (fileBytes[0] == 0x4D && fileBytes[1] == 0x4D)
                bigEndian = true;
            else
            {
                error = "缺少TIFF字节序标记(II/MM)";
                return false;
            }

            int magic = (int)ReadUInt(fileBytes, 2, 2, bigEndian);
            bool bigTiff;
            long ifdOffset;
            if (magic == 42)
            {
                bigTiff = false;
                ifdOffset = (long)ReadUInt(fileBytes, 4, 4, bigEndian);
            }
            else if (magic == 43)
            {
                bigTiff = true;
                if (ReadUInt(fileBytes, 4, 2, bigEndian) != 8)
                {
                    error = "BigTIFF偏移尺寸异常";
                    return false;
                }
                ifdOffset = (long)ReadUInt(fileBytes, 8, 8, bigEndian);
            }
            else
            {
                error = "TIFF魔数无效: " + magic;
                return false;
            }

            var result = new TiffInfo { IsBigTiff = bigTiff, IsBigEndian = bigEndian };
            int entrySize = bigTiff ? 20 : 12;
            long entryCount = bigTiff
                ? (long)ReadUInt(fileBytes, ifdOffset, 8, bigEndian)
                : (long)ReadUInt(fileBytes, ifdOffset, 2, bigEndian);
            long entriesStart = ifdOffset + (bigTiff ? 8 : 2);
            if (entryCount <= 0 || entryCount > 4096 ||
                entriesStart + entryCount * entrySize > fileBytes.Length)
            {
                error = "IFD条目数量或位置无效";
                return false;
            }

            for (long i = 0; i < entryCount; i++)
            {
                long entryOffset = entriesStart + i * entrySize;
                int tag = (int)ReadUInt(fileBytes, entryOffset, 2, bigEndian);
                int type = (int)ReadUInt(fileBytes, entryOffset + 2, 2, bigEndian);
                long count = bigTiff
                    ? (long)ReadUInt(fileBytes, entryOffset + 4, 8, bigEndian)
                    : (long)ReadUInt(fileBytes, entryOffset + 4, 4, bigEndian);
                long valueField = entryOffset + (bigTiff ? 12 : 8);

                switch (tag)
                {
                    case TagImageWidth: result.Width = ReadFirstValue(fileBytes, type, count, valueField, bigTiff, bigEndian); break;
                    case TagImageLength: result.Height = ReadFirstValue(fileBytes, type, count, valueField, bigTiff, bigEndian); break;
                    case TagBitsPerSample: result.BitsPerSample = ReadFirstValue(fileBytes, type, count, valueField, bigTiff, bigEndian); break;
                    case TagCompression: result.Compression = ReadFirstValue(fileBytes, type, count, valueField, bigTiff, bigEndian); break;
                    case TagPhotometric: result.Photometric = ReadFirstValue(fileBytes, type, count, valueField, bigTiff, bigEndian); break;
                    case TagSamplesPerPixel: result.SamplesPerPixel = ReadFirstValue(fileBytes, type, count, valueField, bigTiff, bigEndian); break;
                    case TagRowsPerStrip: result.RowsPerStrip = ReadFirstValue(fileBytes, type, count, valueField, bigTiff, bigEndian); break;
                    case TagSampleFormat: result.SampleFormat = ReadFirstValue(fileBytes, type, count, valueField, bigTiff, bigEndian); break;
                    case TagTileWidth: result.TileWidth = ReadFirstValue(fileBytes, type, count, valueField, bigTiff, bigEndian); break;
                    case TagTileLength: result.TileLength = ReadFirstValue(fileBytes, type, count, valueField, bigTiff, bigEndian); break;
                    case TagStripOffsets: result.StripOffsets = ReadValues(fileBytes, type, count, valueField, bigTiff, bigEndian); break;
                    case TagStripByteCounts: result.StripByteCounts = ReadValues(fileBytes, type, count, valueField, bigTiff, bigEndian); break;
                    case TagTileOffsets: result.TileOffsets = ReadValues(fileBytes, type, count, valueField, bigTiff, bigEndian); break;
                    case TagTileByteCounts: result.TileByteCounts = ReadValues(fileBytes, type, count, valueField, bigTiff, bigEndian); break;
                }
            }

            if (result.RowsPerStrip <= 0)
                result.RowsPerStrip = result.Height; // TIFF默认：单条带包含整幅图

            info = result;
            return true;
        }

        /// <summary>按条带(strip)组织复制像素数据到行优先输出数组。</summary>
        private static bool TryCopyStrips(TiffInfo info, byte[] fileBytes, float[] pixels, out string error)
        {
            error = null;
            if (info.StripOffsets == null || info.StripOffsets.Length == 0)
            {
                error = "TIFF缺少StripOffsets";
                return false;
            }

            int width = (int)info.Width;
            int height = (int)info.Height;
            long rowsPerStrip = info.RowsPerStrip;
            int row = 0;
            for (int strip = 0; strip < info.StripOffsets.Length && row < height; strip++)
            {
                int rows = (int)Math.Min(rowsPerStrip, height - row);
                long byteCount = (long)width * rows * 4;
                long offset = (long)info.StripOffsets[strip];
                if (offset < 0 || offset + byteCount > fileBytes.Length)
                {
                    error = "条带数据超出文件范围(strip=" + strip + ")";
                    return false;
                }
                CopyFloats(fileBytes, offset, pixels, (long)row * width, width * rows, info.IsBigEndian);
                row += rows;
            }

            if (row < height)
            {
                error = "条带数据不足，缺少 " + (height - row) + " 行";
                return false;
            }
            return true;
        }

        /// <summary>按分块(tile)组织复制像素数据；tile在边缘会有填充区域，仅复制图像有效范围。</summary>
        private static bool TryCopyTiles(TiffInfo info, byte[] fileBytes, float[] pixels, out string error)
        {
            error = null;
            if (info.TileWidth <= 0 || info.TileLength <= 0)
            {
                error = "TIFF缺少TileWidth/TileLength";
                return false;
            }

            int width = (int)info.Width;
            int height = (int)info.Height;
            int tileW = (int)info.TileWidth;
            int tileH = (int)info.TileLength;
            int tilesAcross = (width + tileW - 1) / tileW;
            int tilesDown = (height + tileH - 1) / tileH;
            if (info.TileOffsets.Length < tilesAcross * tilesDown)
            {
                error = "Tile数量不足: 需要" + (tilesAcross * tilesDown) + "，实际" + info.TileOffsets.Length;
                return false;
            }

            for (int ty = 0; ty < tilesDown; ty++)
            {
                for (int tx = 0; tx < tilesAcross; tx++)
                {
                    long tileOffset = (long)info.TileOffsets[ty * tilesAcross + tx];
                    long tileBytes = (long)tileW * tileH * 4;
                    if (tileOffset < 0 || tileOffset + tileBytes > fileBytes.Length)
                    {
                        error = "Tile数据超出文件范围(tile=" + (ty * tilesAcross + tx) + ")";
                        return false;
                    }

                    int copyW = Math.Min(tileW, width - tx * tileW);
                    int copyH = Math.Min(tileH, height - ty * tileH);
                    for (int r = 0; r < copyH; r++)
                    {
                        long src = tileOffset + (long)r * tileW * 4;
                        long dst = (long)(ty * tileH + r) * width + tx * tileW;
                        CopyFloats(fileBytes, src, pixels, dst, copyW, info.IsBigEndian);
                    }
                }
            }
            return true;
        }

        /// <summary>从文件字节复制float序列，按需做大端字节交换。</summary>
        private static void CopyFloats(
            byte[] source, long sourceOffset, float[] target, long targetIndex, int count, bool bigEndian)
        {
            if (!bigEndian)
            {
                Buffer.BlockCopy(source, (int)sourceOffset, target, (int)(targetIndex * 4), count * 4);
                return;
            }

            var swap = new byte[4];
            for (int i = 0; i < count; i++)
            {
                long p = sourceOffset + (long)i * 4;
                swap[0] = source[p + 3];
                swap[1] = source[p + 2];
                swap[2] = source[p + 1];
                swap[3] = source[p];
                target[targetIndex + i] = BitConverter.ToSingle(swap, 0);
            }
        }

        /// <summary>读取标签首个数值（SHORT/LONG/LONG8等整数类型）。</summary>
        private static long ReadFirstValue(
            byte[] bytes, int type, long count, long valueField, bool bigTiff, bool bigEndian)
        {
            ulong[] values = ReadValues(bytes, type, Math.Min(count, 1), valueField, bigTiff, bigEndian);
            return values != null && values.Length > 0 ? (long)values[0] : 0;
        }

        /// <summary>
        /// 读取标签的整数值数组。值总字节数不超过值域宽度（classic 4字节 / BigTIFF 8字节）时
        /// 内联存放，否则值域是数据区偏移。
        /// </summary>
        private static ulong[] ReadValues(
            byte[] bytes, int type, long count, long valueField, bool bigTiff, bool bigEndian)
        {
            int size = TypeSize(type);
            if (size == 0 || count <= 0 || count > 1024 * 1024)
                return null;

            int inlineCapacity = bigTiff ? 8 : 4;
            long dataOffset = count * size <= inlineCapacity
                ? valueField
                : (long)ReadUInt(bytes, valueField, inlineCapacity, bigEndian);
            if (dataOffset < 0 || dataOffset + count * size > bytes.Length)
                return null;

            var values = new ulong[count];
            for (long i = 0; i < count; i++)
                values[i] = ReadUInt(bytes, dataOffset + i * size, size, bigEndian);
            return values;
        }

        /// <summary>TIFF数据类型的字节宽度（仅整数类型；其余返回0表示不支持）。</summary>
        private static int TypeSize(int type)
        {
            switch (type)
            {
                case 1: return 1;  // BYTE
                case 3: return 2;  // SHORT
                case 4: return 4;  // LONG
                case 16: return 8; // LONG8 (BigTIFF)
                case 17: return 8; // SLONG8 (BigTIFF)
                default: return 0;
            }
        }

        /// <summary>按指定字节序读取无符号整数。</summary>
        private static ulong ReadUInt(byte[] bytes, long offset, int size, bool bigEndian)
        {
            if (offset < 0 || offset + size > bytes.Length)
                throw new InvalidDataException("TIFF读取越界");

            ulong value = 0;
            if (bigEndian)
            {
                for (int i = 0; i < size; i++)
                    value = (value << 8) | bytes[offset + i];
            }
            else
            {
                for (int i = size - 1; i >= 0; i--)
                    value = (value << 8) | bytes[offset + i];
            }
            return value;
        }
    }
}
