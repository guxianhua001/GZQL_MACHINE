using HalconDotNet;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace VisionTools.Tools.GrabImage
{
    /// <summary>文件读图失败类型；调用层使用该稳定枚举映射多语言提示，不依赖异常文本。</summary>
    public enum VisionImageReadErrorCode
    {
        None,
        FileNotFound,
        UnsupportedFileType,
        DirectoryNotFound,
        NativeReadFailed
    }

    /// <summary>HALCON 图像读取结果；成功时调用方取得并负责释放 Image。</summary>
    public sealed class VisionImageReadResult
    {
        public bool IsSuccess { get; private set; }
        public VisionImage Image { get; private set; }
        public VisionImageReadErrorCode ErrorCode { get; private set; }

        private VisionImageReadResult() { }

        internal static VisionImageReadResult Success(VisionImage image) =>
            new VisionImageReadResult { IsSuccess = true, Image = image };

        internal static VisionImageReadResult Failure(VisionImageReadErrorCode errorCode) =>
            new VisionImageReadResult { ErrorCode = errorCode };
    }

    /// <summary>
    /// 视觉工具通用图像载体。独占 HImage 的生命周期；需要跨工具保存时调用 Clone，
    /// 防止上游释放图像后下游仍访问已失效的原生资源。
    /// </summary>
    public sealed class VisionImage : IDisposable
    {
        private HImage _image;

        internal VisionImage(string sourceFilePath, HImage image)
        {
            SourceFilePath = sourceFilePath;
            _image = image ?? throw new ArgumentNullException(nameof(image));
            _image.GetImageSize(out int width, out int height);
            Width = width;
            Height = height;
            ChannelCount = _image.CountChannels().I;
            PixelType = _image.GetImageType().ToString().Trim().Trim('"');
        }

        public string SourceFilePath { get; }
        public int Width { get; }
        public int Height { get; }
        public int ChannelCount { get; }
        public string PixelType { get; }
        public bool IsHeightMap => ChannelCount == 1 &&
                                   string.Equals(PixelType, "real", StringComparison.OrdinalIgnoreCase);

        /// <summary>获取仅供即时处理的原生图像；调用方不得释放该对象。</summary>
        public HImage Image => _image ?? throw new ObjectDisposedException(nameof(VisionImage));

        /// <summary>复制图像并转交给新的 VisionImage 实例，供后续工具链安全持有。</summary>
        public VisionImage Clone() => new VisionImage(SourceFilePath, Image.CopyImage());

        public void Dispose()
        {
            if (_image == null) return;
            try { _image.Dispose(); } catch { }
            _image = null;
        }
    }

    /// <summary>GrabImage 首阶段文件和目录读取契约；后续拖拽式工具流程可直接复用。</summary>
    public interface IGrabImageReader
    {
        VisionImageReadResult ReadFile(string filePath);
        IReadOnlyList<string> GetImageFiles(string directoryPath, bool includeSubdirectories = false);
    }

    /// <summary>
    /// 迁移自 Plugin.GrabImage 的文件读图能力。仅负责文件/目录与 HALCON HImage，
    /// 不包含旧 VM 的 ModuleBase、相机采集、窗口显示或流程调度。
    /// </summary>
    public sealed class GrabImageReader : IGrabImageReader
    {
        private static readonly HashSet<string> SupportedExtensions = new HashSet<string>(
            // 与原 Plugin.GrabImage 的文件选择范围一致；实际可读性仍由 HALCON 解码器决定。
            new[]
            {
                ".bmp", ".pcx", ".png", ".jpg", ".jpeg", ".gif", ".tif", ".tiff", ".ico",
                ".dxf", ".cgm", ".cdr", ".wmf", ".eps", ".emf"
            },
            StringComparer.OrdinalIgnoreCase);

        /// <summary>用 HALCON 原生 ReadImage 加载单个文件，并将 HImage 所有权交给结果对象。</summary>
        public VisionImageReadResult ReadFile(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                return VisionImageReadResult.Failure(VisionImageReadErrorCode.FileNotFound);

            if (!SupportedExtensions.Contains(Path.GetExtension(filePath)))
                return VisionImageReadResult.Failure(VisionImageReadErrorCode.UnsupportedFileType);

            HImage image = null;
            try
            {
                image = new HImage();
                image.ReadImage(filePath);
                var result = VisionImageReadResult.Success(new VisionImage(filePath, image));
                image = null; // VisionImage 接管原生资源，finally 不再释放
                return result;
            }
            catch
            {
                return VisionImageReadResult.Failure(VisionImageReadErrorCode.NativeReadFailed);
            }
            finally
            {
                try { image?.Dispose(); } catch { }
            }
        }

        /// <summary>按完整路径稳定排序返回目录图像文件，供未来流程中的循环读图节点使用。</summary>
        public IReadOnlyList<string> GetImageFiles(string directoryPath, bool includeSubdirectories = false)
        {
            if (string.IsNullOrWhiteSpace(directoryPath) || !Directory.Exists(directoryPath))
                return Array.Empty<string>();

            var searchOption = includeSubdirectories ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            return Directory.EnumerateFiles(directoryPath, "*.*", searchOption)
                .Where(filePath => SupportedExtensions.Contains(Path.GetExtension(filePath)))
                .OrderBy(filePath => filePath, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }
}
