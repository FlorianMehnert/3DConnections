using System;
using UnityEngine;
using Color = UnityEngine.Color;

public static class Colorpalette
{
    public static Color[] GeneratePaletteFromBaseColor(Color baseColor = default, int prebuiltChannels = 0, bool generateColors = false, bool alternativeColors = false)
{
    var palette = new Color[8];
    if (!generateColors)
    {
        switch (prebuiltChannels)
        {
            case 0:
                return new[]
                {
                    new Color(0.2f, 0.6f, 1f),
                    new(0.4f, 0.8f, 0.4f),
                    new(0.8f, 0.4f, 0.8f),
                    new(0.1f, 0.9f, 0.9f),
                    new(0.5f, 0.5f, 1f),
                    new(0.5f, 1f, 0.5f),
                    new(1f, 0f, 0.5f),
                    new(1f, 0.6f, 0f)
                };
            case 1:
                return new[]
                {
                    HexToColor("#FAD900"),
                    HexToColor("#00E6FA"),
                    HexToColor("#FA0090"),
                    HexToColor("#c7ecee"),
                    HexToColor("#379CA5"),
                    HexToColor("#7A723D"),
                    HexToColor("#7A3D60"),
                    HexToColor("#87934A")
                };
            case 2:
                return new[]
                {
                    HexToColor("#22a6b3"),
                    HexToColor("#be2edd"),
                    HexToColor("#4834d4"),
                    HexToColor("#f0932b"),
                    HexToColor("#7ed6df"),
                    HexToColor("#e056fd"),
                    HexToColor("#686de0"),
                    HexToColor("#CAF95D"),
                };
            case 3:
                return new[]
                {
                    HexToColor("#6D214F"),
                    HexToColor("#182C61"),
                    HexToColor("#FC427B"),
                    HexToColor("#BDC581"),
                    HexToColor("#B33771"),
                    HexToColor("#3B3B98"),
                    HexToColor("#FD7272"),
                    HexToColor("#FDC972"),
                };
        }
    }

    if (baseColor == default)
    {
        baseColor = new Color(0.2f, 0.6f, 1f);
    }

    Color.RGBToHSV(baseColor, out var h, out var s, out _);

    // Ensure good color variation
    s = Mathf.Max(0.5f, s);
    
    // Generate variations
    palette[0] = baseColor; 
    palette[1] = HSVToRGB((h + 0.3f) % 1f, s, 1);
    palette[2] = HSVToRGB((h + 0.6f) % 1f, s, 1);
    palette[3] = HSVToRGB((h + 0.1f) % 1f, s, 1);
    palette[4] = HSVToRGB(h % 1f, s * 0.7f, 0.7f);
    palette[5] = HSVToRGB((h + 0.3f) % 1f, s * 0.7f, 0.7f);
    palette[6] = HSVToRGB((h + 0.6f) % 1f, s * 0.7f, 0.7f);
    palette[7] = HSVToRGB((h + 0.25f) % 1f, s * 0.5f, 0.2f);

    return palette;
}


    private static Color HexToColor(string hex)
    {
        hex = hex.Replace("#", "");

        if (hex.Length == 3)
        {
            // Short hex format
            hex = string.Concat(
                hex[0], hex[0],
                hex[1], hex[1],
                hex[2], hex[2]
            );
        }

        if (hex.Length != 6)
            throw new ArgumentException("Invalid hex color format");

        var r = byte.Parse(hex.Substring(0, 2), System.Globalization.NumberStyles.HexNumber);
        var g = byte.Parse(hex.Substring(2, 2), System.Globalization.NumberStyles.HexNumber);
        var b = byte.Parse(hex.Substring(4, 2), System.Globalization.NumberStyles.HexNumber);

        return new Color(r / 255f, g / 255f, b / 255f);
    }


    private static Color HSVToRGB(float h, float s, float v)
    {
        float r, g, b;

        var i = Mathf.FloorToInt(h * 6);
        var f = h * 6 - i;
        var p = v * (1 - s);
        var q = v * (1 - f * s);
        var t = v * (1 - (1 - f) * s);

        switch (i % 6)
        {
            case 0:
                r = v;
                g = t;
                b = p;
                break;
            case 1:
                r = q;
                g = v;
                b = p;
                break;
            case 2:
                r = p;
                g = v;
                b = t;
                break;
            case 3:
                r = p;
                g = q;
                b = v;
                break;
            case 4:
                r = t;
                g = p;
                b = v;
                break;
            case 5:
                r = v;
                g = p;
                b = q;
                break;
            default: r = g = b = v; break;
        }

        return new Color(r, g, b);
    }
}