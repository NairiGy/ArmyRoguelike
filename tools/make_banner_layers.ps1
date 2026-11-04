# Splits the blank banner painting into the two layers the game draws:
#   pole.png:  the sandstone post, crossbar and loops, with the post continued behind the cloth.
#   cloth.png: the cloth's light and shade as grey from 0 to 1, for the shader to colour through a ramp.
# The painting's cloth is a vivid turquoise only so it can be told apart from the stone and the grey background.
# The numbers for where the post runs fit banner_blank_iso__leonardo_gpt-image-2.5-flare.jpg.
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

    // The post's left edge, which leans slightly in the painting.
    static float PostLeft(int y)
    {
        return 472f - (y - 400) * 0.028f;
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

            // The cloth's darkest and brightest (ignoring the extreme 2%), so its shade fills the ramp.
            var histogram = new int[256];
            long count = 0;
            for (var i = 0; i < w * h; i++)
            {
                int b = src[i * 4], g = src[i * 4 + 1], r = src[i * 4 + 2];
                if (Math.Min(g, b) - r > 60)
                {
                    histogram[(int)(0.3 * r + 0.59 * g + 0.11 * b)]++;
                    count++;
                }
            }

            int low = 0, high = 255;
            for (long seen = 0; seen + histogram[low] < count / 50; low++)
                seen += histogram[low];
            for (long seen = 0; seen + histogram[high] < count / 50; high--)
                seen += histogram[high];

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

                var shade = Math.Max(0f, Math.Min(1f, (lum - low) / (high - low)));
                // Edge pixels are part background: keep them from reading as a pale line.
                if (clothMask < 0.98f)
                    shade = Math.Min(shade, 0.45f);
                var grey = (byte)(shade * 255f);
                cloth[i] = grey; cloth[i + 1] = grey; cloth[i + 2] = grey;
                cloth[i + 3] = (byte)(clothMask * 255f);
            }

            // Continue the post behind the cloth, from the stretch below the hem, so the moving cloth shows no hole.
            for (var y = 375; y < 870; y++)
            for (var x = (int)PostLeft(y); x < 500; x++)
            {
                var i = (y * w + x) * 4;
                if (pole[i + 3] == 255)
                    continue;
                var row = 880 + y % 150;
                var from = (row * w + x + (int)(PostLeft(row) - PostLeft(y))) * 4;
                pole[i] = src[from]; pole[i + 1] = src[from + 1]; pole[i + 2] = src[from + 2];
                pole[i + 3] = 255;
            }

            Save(pole, w, h, dir + "\\pole.png");
            Save(cloth, w, h, dir + "\\cloth.png");
        }
    }
}
"@

[BannerLayers]::Run((Resolve-Path $Blank).Path, (Resolve-Path $Dir).Path)
