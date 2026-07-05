using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace BiliLiveNotifier.Core;

/// <summary>
/// 图片本地缓存服务 (标准 .NET IO 实现)
/// 通过 SHA1 哈希映射远程 URL 到本地文件，提供 file:/// URI
/// </summary>
public static class ToastImageCache
{
    private static readonly HttpClient _httpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(10)
    };

    // 缓存目录改为当前 exe 所在目录下的 ./cache/
    private static readonly string _cacheDirectory = Path.Combine(
        AppContext.BaseDirectory,
        "cache"
    );

    // 需要清理的图片扩展名白名单
    private static readonly string[] _imageExtensions = [".png", ".jpg", ".jpeg", ".webp", ".gif", ".bmp", ".tmp"];

    static ToastImageCache()
    {
        if (!Directory.Exists(_cacheDirectory))
        {
            Directory.CreateDirectory(_cacheDirectory);
            LLog.Info($"[ToastImageCache] 初始化: 缓存目录已创建于 {_cacheDirectory}");
        }
    }

    /// <summary>
    /// 获取图片的本地缓存 URI
    /// 若本地存在对应 SHA1 文件则直接返回，否则下载后返回
    /// </summary>
    public static async Task<Uri> GetLocalUriAsync(string remoteUrl)
    {
        if (string.IsNullOrWhiteSpace(remoteUrl))
            return null;

        try
        {
            string fileName = ComputeSha1(remoteUrl) + GetExtension(remoteUrl);
            string localPath = Path.Combine(_cacheDirectory, fileName);

            // SHA1 命名天然保证内容一致性，文件存在即视为有效缓存
            if (File.Exists(localPath))
            {
                LLog.Debug($"[ToastImageCache] 命中缓存: {fileName}");
                return new Uri(localPath);
            }

            LLog.Debug($"[ToastImageCache] 开始下载: {remoteUrl}");
            byte[] imageBytes = await _httpClient.GetByteArrayAsync(remoteUrl);

            // 原子写入：先写临时文件再重命名，避免并发读取到不完整文件
            string tempPath = localPath + ".tmp";
            await File.WriteAllBytesAsync(tempPath, imageBytes);
            File.Move(tempPath, localPath, overwrite: true);

            LLog.Debug($"[ToastImageCache] 写入完成: {fileName} ({imageBytes.Length} 字节)");
            return new Uri(localPath);
        }
        catch (Exception ex)
        {
            LLog.Error($"[ToastImageCache] 处理失败: URL={remoteUrl}, 原因={ex.Message}");
            // 降级返回原始 URL，避免阻断调用方流程
            return new Uri(remoteUrl);
        }
    }

    /// <summary>
    /// 获取图片的 2:1 Hero 版本地缓存 URI
    /// 在原始缓存基础上做两侧模糊渐变扩展，以 hero_ 前缀独立缓存
    /// </summary>
    public static async Task<Uri?> GetHeroUriAsync(string remoteUrl)
    {
        // 1. 先取原图缓存
        var rawUri = await GetLocalUriAsync(remoteUrl);
        if (rawUri == null || !rawUri.IsFile)
            return null;

        string rawPath = rawUri.LocalPath;
        string dir = Path.GetDirectoryName(rawPath)!;
        string sha1 = Path.GetFileNameWithoutExtension(rawPath);
        string ext = Path.GetExtension(rawPath);
        string heroFileName = $"hero_{sha1}{ext}";
        string heroPath = Path.Combine(dir, heroFileName);

        // 2. 已缓存 → 直接返回
        if (File.Exists(heroPath))
        {
            LLog.Debug($"[ToastImageCache] 命中 Hero 缓存: {heroFileName}");
            return new Uri(heroPath);
        }

        // 3. 处理并原子写入（temp 文件带扩展名，供 ImageSharp 识别编码器）
        string tempPath = Path.Combine(dir, $"hero_{sha1}_tmp{ext}");
        try
        {
            ImageProcessor.ExpandToHeroRatio(rawPath, tempPath);
            File.Move(tempPath, heroPath, overwrite: true);
            LLog.Debug($"[ToastImageCache] Hero 处理完成: {heroFileName}");
            return new Uri(heroPath);
        }
        catch (Exception ex)
        {
            LLog.Error($"[ToastImageCache] Hero 处理失败: {ex.Message}");
            // 降级返回原图 URI
            return rawUri;
        }
    }

    /// <summary>
    /// 清理缓存目录下的所有图片文件
    /// 仅删除 _cacheDirectory 根目录下的匹配文件，不遍历子文件夹
    /// </summary>
    public static void ClearCache()
    {
        try
        {
            if (!Directory.Exists(_cacheDirectory))
            {
                LLog.Warn("[ToastImageCache] 清理跳过: 缓存目录不存在");
                return;
            }

            int deletedCount = 0;
            foreach (var ext in _imageExtensions)
            {
                // SearchOption.TopDirectoryOnly 确保不递归子目录
                var files = Directory.GetFiles(_cacheDirectory, $"*{ext}", SearchOption.TopDirectoryOnly);
                foreach (var file in files)
                {
                    File.Delete(file);
                    deletedCount++;
                }
            }

            LLog.Info($"[ToastImageCache] 清理完成: 共删除 {deletedCount} 个文件");
        }
        catch (Exception ex)
        {
            LLog.Error($"[ToastImageCache] 清理失败: 原因={ex.Message}");
        }
    }

    private static string ComputeSha1(string input)
    {
        using var sha1 = SHA1.Create();
        byte[] hash = sha1.ComputeHash(Encoding.UTF8.GetBytes(input));
        return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
    }

    private static string GetExtension(string url)
    {
        try
        {
            string path = new Uri(url).AbsolutePath;
            string ext = Path.GetExtension(path).ToLowerInvariant();
            return string.IsNullOrEmpty(ext) ? ".jpg" : ext;
        }
        catch
        {
            return ".jpg";
        }
    }
}