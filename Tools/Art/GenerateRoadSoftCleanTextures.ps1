param(
    [string]$OutDir = "Assets\Arts\Map\Tiles\Textures\Generated"
)

if (-not (Test-Path -LiteralPath $OutDir)) {
    New-Item -ItemType Directory -Path $OutDir | Out-Null
}

Add-Type -AssemblyName System.Drawing

$source = @"
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

public static class RoadSoftCleanTextureGenerator
{
    struct Mark
    {
        public double X;
        public double Y;
        public double RadiusX;
        public double RadiusY;
        public double Angle;
        public double Strength;
    }

    const int Size = 1024;

    public static void Generate(string outputDirectory)
    {
        Directory.CreateDirectory(outputDirectory);
        GenerateAlbedo(Path.Combine(outputDirectory, "Road_SoftClean_Albedo_1024.png"));
        GeneratePatchMask(Path.Combine(outputDirectory, "Road_SoftClean_PatchMask_1024.png"));
        GenerateMudPatchAtlas(Path.Combine(outputDirectory, "Road_CartoonMudPatch_Atlas_1024.png"));
    }

    static void GenerateAlbedo(string path)
    {
        byte[] pixels = new byte[Size * Size * 4];

        for (int y = 0; y < Size; y++)
        {
            double v = (y + 0.5) / Size;
            for (int x = 0; x < Size; x++)
            {
                double u = (x + 0.5) / Size;
                double n =
                    0.004 * Math.Sin(Tau() * (3.0 * u + 1.0 * v) + 0.43) +
                    0.003 * Math.Cos(Tau() * (-1.0 * u + 4.0 * v) + 2.19);

                double value = Clamp01(0.995 + n);
                int index = (y * Size + x) * 4;
                pixels[index + 0] = ToByte(value * 0.965);
                pixels[index + 1] = ToByte(value * 0.990);
                pixels[index + 2] = ToByte(value * 1.000);
                pixels[index + 3] = 255;
            }
        }

        SavePng(path, pixels);
    }

    static void GeneratePatchMask(string path)
    {
        var pebbles = CreateMarks(22, 0.0025, 0.0060, 0.012, 0.034, 0.86, 1.16, 11879);
        var footprints = CreateCartoonFootprints();
        byte[] pixels = new byte[Size * Size * 4];

        for (int y = 0; y < Size; y++)
        {
            double v = (y + 0.5) / Size;
            for (int x = 0; x < Size; x++)
            {
                double u = (x + 0.5) / Size;
                double darken = 0.0;

                for (int i = 0; i < pebbles.Length; i++)
                {
                    darken += Blob(u, v, pebbles[i]) * pebbles[i].Strength;
                }

                for (int i = 0; i < footprints.Length; i++)
                {
                    darken += Blob(u, v, footprints[i]) * footprints[i].Strength;
                }

                double softSpeckle =
                    0.002 * Math.Sin(Tau() * (11.0 * u + 7.0 * v) + 0.8) +
                    0.002 * Math.Cos(Tau() * (-9.0 * u + 10.0 * v) + 2.4);

                double value = Clamp01(0.994 - darken + softSpeckle);
                int index = (y * Size + x) * 4;
                byte c = ToByte(value);
                pixels[index + 0] = c;
                pixels[index + 1] = c;
                pixels[index + 2] = c;
                pixels[index + 3] = 255;
            }
        }

        SavePng(path, pixels);
    }

    static void GenerateMudPatchAtlas(string path)
    {
        const int atlasGrid = 4;
        int cellSize = Size / atlasGrid;
        byte[] pixels = new byte[Size * Size * 4];

        for (int y = 0; y < Size; y++)
        {
            for (int x = 0; x < Size; x++)
            {
                int index = (y * Size + x) * 4;
                pixels[index + 0] = 255;
                pixels[index + 1] = 255;
                pixels[index + 2] = 255;
                pixels[index + 3] = 255;
            }
        }

        for (int atlasY = 0; atlasY < atlasGrid; atlasY++)
        {
            for (int atlasX = 0; atlasX < atlasGrid; atlasX++)
            {
                int variant = atlasY * atlasGrid + atlasX;
                Mark[] marks = CreateRoadCartoonMarks(variant);

                for (int y = 0; y < cellSize; y++)
                {
                    double v = (y + 0.5) / cellSize;
                    for (int x = 0; x < cellSize; x++)
                    {
                        double u = (x + 0.5) / cellSize;
                        double darken = 0.0;

                        for (int i = 0; i < marks.Length; i++)
                        {
                            darken += BlobLocal(u, v, marks[i]) * marks[i].Strength;
                        }

                        darken = Clamp01(darken);
                        int px = atlasX * cellSize + x;
                        int py = atlasY * cellSize + y;
                        int index = (py * Size + px) * 4;
                        double edgeFade = SmoothStep(0.0, 0.10, Math.Min(Math.Min(u, 1.0 - u), Math.Min(v, 1.0 - v)));
                        double mark = darken * edgeFade;

                        pixels[index + 0] = ToByte(Lerp(1.0, 0.16, mark));
                        pixels[index + 1] = ToByte(Lerp(1.0, 0.31, mark));
                        pixels[index + 2] = ToByte(Lerp(1.0, 0.43, mark));
                        pixels[index + 3] = 255;
                    }
                }
            }
        }

        SavePng(path, pixels);
    }

    static Mark[] CreateMarks(int count, double minRadius, double maxRadius, double minStrength, double maxStrength, int seed)
    {
        return CreateMarks(count, minRadius, maxRadius, minStrength, maxStrength, 0.55, 1.65, seed);
    }

    static Mark[] CreateMarks(
        int count,
        double minRadius,
        double maxRadius,
        double minStrength,
        double maxStrength,
        double minAspect,
        double maxAspect,
        int seed)
    {
        var random = new Random(seed);
        var marks = new Mark[count];
        for (int i = 0; i < count; i++)
        {
            double radius = Lerp(minRadius, maxRadius, random.NextDouble());
            double aspect = Lerp(minAspect, maxAspect, random.NextDouble());
            marks[i] = new Mark
            {
                X = random.NextDouble(),
                Y = random.NextDouble(),
                RadiusX = radius * aspect,
                RadiusY = radius / aspect,
                Angle = random.NextDouble() * Tau(),
                Strength = Lerp(minStrength, maxStrength, random.NextDouble())
            };
        }

        return marks;
    }

    static Mark[] CreateCartoonFootprints()
    {
        var marks = new List<Mark>();
        AddFootprint(marks, 0.42, 0.42, -0.72, 0.92);
        AddFootprint(marks, 0.56, 0.54, -0.62, 0.82);
        AddFootprint(marks, 0.47, 0.66, -0.78, 0.68);

        return marks.ToArray();
    }

    static Mark[] CreateRoadCartoonMarks(int variant)
    {
        var random = new Random(6203 + variant * 97);
        var marks = new List<Mark>();
        int groupCount = variant % 5 == 0 ? 1 : 2;
        if (variant % 7 == 0)
        {
            groupCount = 0;
        }

        for (int group = 0; group < groupCount; group++)
        {
            double centerX = Lerp(0.36, 0.64, random.NextDouble());
            double centerY = Lerp(0.36, 0.64, random.NextDouble());
            double angle = Lerp(-0.95, -0.48, random.NextDouble());
            double strength = Lerp(0.62, 0.95, random.NextDouble());
            AddRoadMarkCluster(marks, centerX, centerY, angle, strength, random);
        }

        int pebbleCount = 3 + variant % 5;
        for (int i = 0; i < pebbleCount; i++)
        {
            double angle = random.NextDouble() * Tau();
            double distance = Lerp(0.08, 0.26, random.NextDouble());
            marks.Add(new Mark
            {
                X = 0.5 + Math.Cos(angle) * distance,
                Y = 0.5 + Math.Sin(angle) * distance,
                RadiusX = Lerp(0.010, 0.020, random.NextDouble()),
                RadiusY = Lerp(0.007, 0.016, random.NextDouble()),
                Angle = random.NextDouble() * Tau(),
                Strength = Lerp(0.055, 0.105, random.NextDouble())
            });
        }

        return marks.ToArray();
    }

    static void AddRoadMarkCluster(List<Mark> marks, double x, double y, double angle, double strength, Random random)
    {
        double forwardX = Math.Cos(angle);
        double forwardY = Math.Sin(angle);
        double sideX = -forwardY;
        double sideY = forwardX;

        int strokeCount = 2 + random.Next(3);
        for (int i = 0; i < strokeCount; i++)
        {
            double side = (i - (strokeCount - 1) * 0.5) * Lerp(0.022, 0.036, random.NextDouble());
            double forward = Lerp(-0.045, 0.055, random.NextDouble());
            marks.Add(new Mark
            {
                X = x + forwardX * forward + sideX * side,
                Y = y + forwardY * forward + sideY * side,
                RadiusX = Lerp(0.008, 0.014, random.NextDouble()),
                RadiusY = Lerp(0.033, 0.060, random.NextDouble()),
                Angle = angle + Math.PI * 0.5 + Lerp(-0.14, 0.14, random.NextDouble()),
                Strength = Lerp(0.105, 0.185, random.NextDouble()) * strength
            });
        }

        int dotCount = 3 + random.Next(4);
        for (int i = 0; i < dotCount; i++)
        {
            double forward = Lerp(0.035, 0.105, random.NextDouble());
            double side = Lerp(-0.055, 0.055, random.NextDouble());
            marks.Add(new Mark
            {
                X = x + forwardX * forward + sideX * side,
                Y = y + forwardY * forward + sideY * side,
                RadiusX = Lerp(0.007, 0.018, random.NextDouble()),
                RadiusY = Lerp(0.006, 0.015, random.NextDouble()),
                Angle = random.NextDouble() * Tau(),
                Strength = Lerp(0.070, 0.145, random.NextDouble()) * strength
            });
        }
    }

    static void AddFootprint(List<Mark> marks, double x, double y, double angle, double strength)
    {
        double forwardX = Math.Cos(angle);
        double forwardY = Math.Sin(angle);
        double sideX = -forwardY;
        double sideY = forwardX;

        marks.Add(new Mark
        {
            X = x,
            Y = y,
            RadiusX = 0.014,
            RadiusY = 0.043,
            Angle = angle + Math.PI * 0.5,
            Strength = 0.190 * strength
        });

        for (int i = 0; i < 3; i++)
        {
            double side = (i - 1) * 0.014;
            double toeForward = 0.045 + i * 0.003;
            marks.Add(new Mark
            {
                X = x + forwardX * toeForward + sideX * side,
                Y = y + forwardY * toeForward + sideY * side,
                RadiusX = 0.0065 - i * 0.0007,
                RadiusY = 0.0080 - i * 0.0006,
                Angle = angle,
                Strength = 0.150 * strength
            });
        }

        marks.Add(new Mark
        {
            X = x - forwardX * 0.030,
            Y = y - forwardY * 0.030,
            RadiusX = 0.010,
            RadiusY = 0.018,
            Angle = angle + Math.PI * 0.5,
            Strength = 0.085 * strength
        });
    }

    static double Blob(double u, double v, Mark mark)
    {
        double dx = WrapDelta(u - mark.X);
        double dy = WrapDelta(v - mark.Y);
        double ca = Math.Cos(mark.Angle);
        double sa = Math.Sin(mark.Angle);
        double rx = dx * ca - dy * sa;
        double ry = dx * sa + dy * ca;
        double q = (rx * rx) / (mark.RadiusX * mark.RadiusX) + (ry * ry) / (mark.RadiusY * mark.RadiusY);

        if (q >= 1.0)
        {
            return 0.0;
        }

        double t = 1.0 - q;
        return t * t * (3.0 - 2.0 * t);
    }

    static double BlobLocal(double u, double v, Mark mark)
    {
        double dx = u - mark.X;
        double dy = v - mark.Y;
        double ca = Math.Cos(mark.Angle);
        double sa = Math.Sin(mark.Angle);
        double rx = dx * ca - dy * sa;
        double ry = dx * sa + dy * ca;
        double q = (rx * rx) / (mark.RadiusX * mark.RadiusX) + (ry * ry) / (mark.RadiusY * mark.RadiusY);

        if (q >= 1.0)
        {
            return 0.0;
        }

        double t = 1.0 - q;
        return t * t * (3.0 - 2.0 * t);
    }

    static double WrapDelta(double value)
    {
        value = value - Math.Floor(value);
        if (value > 0.5)
        {
            value -= 1.0;
        }

        return value;
    }

    static double Clamp01(double value)
    {
        if (value < 0.0) return 0.0;
        if (value > 1.0) return 1.0;
        return value;
    }

    static double Lerp(double a, double b, double t)
    {
        return a + (b - a) * t;
    }

    static double SmoothStep(double edge0, double edge1, double value)
    {
        double t = Clamp01((value - edge0) / (edge1 - edge0));
        return t * t * (3.0 - 2.0 * t);
    }

    static byte ToByte(double value)
    {
        value = Clamp01(value);
        return (byte)Math.Round(value * 255.0);
    }

    static double Tau()
    {
        return Math.PI * 2.0;
    }

    static void SavePng(string path, byte[] pixels)
    {
        using (var bitmap = new Bitmap(Size, Size, PixelFormat.Format32bppArgb))
        {
            var rect = new Rectangle(0, 0, Size, Size);
            BitmapData data = bitmap.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            try
            {
                Marshal.Copy(pixels, 0, data.Scan0, pixels.Length);
            }
            finally
            {
                bitmap.UnlockBits(data);
            }

            bitmap.Save(path, ImageFormat.Png);
        }
    }
}
"@

Add-Type -ReferencedAssemblies "System.Drawing" -TypeDefinition $source
$resolvedOutDir = (Resolve-Path -LiteralPath $OutDir).Path
[RoadSoftCleanTextureGenerator]::Generate($resolvedOutDir)
Write-Host "Generated road soft clean textures in $resolvedOutDir"
