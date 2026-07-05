using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace BiliLiveNotifier.Core;

/// <summary>
/// 图片处理工具 — 将 16:9 封面扩展为 2:1 Hero 图（两侧模糊渐变填充）
/// 解决 Windows Toast HeroImage 对 16:9 图片上下裁切的问题
/// </summary>
public static class ImageProcessor
{
    /// <summary>
    /// 将图片扩展至 2:1 比例，原图居中，两侧用拉伸模糊背景 + 渐变透明过渡填充
    /// </summary>
    public static void ExpandToHeroRatio(
        string sourcePath,
        string outputPath,
        int edgePixels = 43,
        float blurRadius = 8f)
    {
        // 用全限定名避免和 System.Drawing.Image 冲突
        using var image = SixLabors.ImageSharp.Image.Load(sourcePath);
        int origW = image.Width;
        int origH = image.Height;

        // 目标宽度 = 高度 × 2（2:1 比例）
        int targetW = origH * 2;

        // 原图已满足 2:1 → 直接保存
        if (origW >= targetW)
        {
            image.Save(outputPath);
            LLog.Debug($"[ImageProcessor] 原图({origW}x{origH})已满足2:1，直接保存");
            return;
        }

        edgePixels = Math.Min(edgePixels, origW / 2);

        // ─── 1. 拉伸背景（高斯模糊）───
        using var background = image.Clone(ctx =>
        {
            ctx.Resize(targetW, origH);
            if (blurRadius > 0)
                ctx.GaussianBlur(blurRadius);
        });

        // ─── 2. 渐变透明遮罩 + 合并 Alpha ───
        // 用 byte 数组代替 Image<L8>，避免 API 版本差异
        byte[,] maskAlpha = new byte[origW, origH];

        for (int y = 0; y < origH; y++)
        {
            for (int x = 0; x < edgePixels; x++)
            {
                double t = (double)x / edgePixels;
                maskAlpha[x, y] = (byte)(255 * (1 - Math.Cos(t * Math.PI / 2)));
            }
            for (int x = edgePixels; x < origW - edgePixels; x++)
                maskAlpha[x, y] = 255;
            for (int x = origW - edgePixels; x < origW; x++)
            {
                double t = (double)(x - (origW - edgePixels)) / edgePixels;
                maskAlpha[x, y] = (byte)(255 - 255 * (1 - Math.Cos(t * Math.PI / 2)));
            }
        }

        // ─── 3. 将原图转为 Rgba32，逐像素合并 Alpha ───
        using var overlay = image.CloneAs<Rgba32>();
        for (int y = 0; y < origH; y++)
        {
            for (int x = 0; x < origW; x++)
            {
                Rgba32 pixel = overlay[x, y];
                pixel.A = Math.Min(pixel.A, maskAlpha[x, y]);
                overlay[x, y] = pixel;
            }
        }

        // ─── 4. 居中粘贴到背景上（用全限定名避免和 System.Drawing.Point 冲突）───
        int offsetX = (targetW - origW) / 2;
        background.Mutate(ctx => ctx.DrawImage(overlay, new SixLabors.ImageSharp.Point(offsetX, 0), 1f));

        // ─── 5. 保存 ───
        background.Save(outputPath);
        LLog.Debug($"[ImageProcessor] 处理完成: {origW}x{origH} → {targetW}x{origH}, " +
                   $"edgePixels={edgePixels}, blurRadius={blurRadius}");
    }
}
