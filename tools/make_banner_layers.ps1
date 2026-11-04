# Splits the blank banner painting into the two layers the game draws:
#   pole.png:  the sandstone pole and crossbar, continued behind the cloth so its moving hem never shows a gap.
#   cloth.png: the cloth's folds as grey around 0.5, for the shader to tint (BannerCloth.shader).
# The painting's cloth is a vivid turquoise only so it can be told apart from the stone and the grey background.
#
#   powershell -NoProfile -ExecutionPolicy Bypass -File tools/make_banner_layers.ps1 <painting> <output folder>
param([string]$Blank, [string]$Dir)

Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @"
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

public static class BannerLayers
{
    static float Smooth(float a, float b, float x)
    {
        var t = Math.Max(0f, Math.Min(1f, (x - a) / (b - a)));
        return t * t * (3f - 2f * t);
    }

    static void Save(byte[] px, int w, int h, string path)
    {
        using (var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb))
        {
            var d = bmp.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            for (var y = 0; y < h; y++)
                Marshal.Copy(px, y * w * 4, d.Scan0 + y * d.Stride, w * 4);
            bmp.UnlockBits(d);
            bmp.Save(path, ImageFormat.Png);
        }
    }

    public static void Run(string blankPath, string dir)
    {
        using (var blank = new Bitmap(blankPath))
        {
            int w = blank.Width, h = blank.Height;
            var src = new byte[w * h * 4];
            var data = blank.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            for (var y = 0; y < h; y++)
                Marshal.Copy(data.Scan0 + y * data.Stride, src, y * w * 4, w * 4);
            blank.UnlockBits(data);

            // The cloth's average brightness, so its folds become a multiplier around 1.
            double sum = 0; long count = 0;
            for (var i = 0; i < w * h; i++)
            {
                int b = src[i * 4], g = src[i * 4 + 1], r = src[i * 4 + 2];
                if (Math.Min(g, b) - r > 60) { sum += 0.3 * r + 0.59 * g + 0.11 * b; count++; }
            }
            var mean = (float)(sum / count);

            var pole = new byte[w * h * 4];
            var cloth = new byte[w * h * 4];
            for (var i = 0; i < w * h * 4; i += 4)
            {
                float b = src[i], g = src[i + 1], r = src[i + 2];
                var clothMask = Smooth(12f, 45f, Math.Min(g, b) - r);
                var stoneMask = Smooth(10f, 30f, r - b) * (1f - clothMask);
                var lum = 0.3f * r + 0.59f * g + 0.11f * b;

                pole[i] = src[i]; pole[i + 1] = src[i + 1]; pole[i + 2] = src[i + 2];
                pole[i + 3] = (byte)(stoneMask * 255f);

                // Edge pixels are part background: never brighter than the cloth, or the edge gets a pale line.
                var fold = (float)Math.Pow(lum / mean, 1.5);
                if (clothMask < 0.98f)
                    fold = Math.Min(fold, 0.9f);
                var grey = (byte)Math.Min(255f, fold * 127.5f);
                cloth[i] = grey; cloth[i + 1] = grey; cloth[i + 2] = grey;
                cloth[i + 3] = (byte)(clothMask * 255f);
            }

            // Continue the pole behind the cloth, from the stretch below the hem.
            for (var y = 262; y < 1125; y++)
            for (var x = 340; x < 430; x++)
            {
                var i = (y * w + x) * 4;
                var from = ((1125 + (y % 120)) * w + x) * 4;
                pole[i] = pole[from]; pole[i + 1] = pole[from + 1]; pole[i + 2] = pole[from + 2];
                pole[i + 3] = pole[from + 3];
            }

            Save(pole, w, h, dir + "\\pole.png");
            Save(cloth, w, h, dir + "\\cloth.png");
        }
    }
}
"@

[BannerLayers]::Run((Resolve-Path $Blank).Path, (Resolve-Path $Dir).Path)
