using System;
using UnityEngine;
using Color = UnityEngine.Color;

namespace _3DConnections.Runtime.Utils
{
    public static class Colorpalette
    {
        public static Color[] GeneratePaletteFromBaseColor(Color baseColor, bool usePrebuilt = false)
        {
            var palette = new Color[6];

            if (usePrebuilt)
            {
                return new[]
                {
                    HexToColor("#FAD900"), // go
                    HexToColor("#00E6FA"), // co
                    HexToColor("#FA0090"), // so
                    HexToColor("#379CA5"), // pc
                    HexToColor("#7A723D"), // coc
                    HexToColor("#7A3D60")  // ref
                };
            }
    
            // Manual HSV conversion
            var min = Mathf.Min(baseColor.r, Mathf.Min(baseColor.g, baseColor.b));
            var max = Mathf.Max(baseColor.r, Mathf.Max(baseColor.g, baseColor.b));
            var delta = max - min;
    
            float h = 0;
            var s = (max == 0) ? 0 : delta / max;

            if (delta != 0)
            {
                if (Mathf.Approximately(baseColor.r, max))
                    h = (baseColor.g - baseColor.b) / delta;
                else if (Mathf.Approximately(baseColor.g, max))
                    h = 2 + (baseColor.b - baseColor.r) / delta;
                else
                    h = 4 + (baseColor.r - baseColor.g) / delta;
        
                h /= 6;
                if (h < 0) h += 1;
            }
    
            // Generate variations
            palette[0] = baseColor;  // Original color
            palette[1] = HSVToRGB(h + 0.3f, s, max);  // Shifted hue
            palette[2] = HSVToRGB(h + 0.6f, s, max);  // More saturated
            palette[3] = HSVToRGB(h, s * 0.5f, max * 0.7f);  // Brighter
            palette[4] = HSVToRGB(h + 0.3f, s * 0.5f, max * 0.7f);  // Complementary
            palette[5] = HSVToRGB(h + 0.6f, s * 0.5f, max * 0.7f);  // Muted
    
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
    
            byte r = byte.Parse(hex.Substring(0, 2), System.Globalization.NumberStyles.HexNumber);
            byte g = byte.Parse(hex.Substring(2, 2), System.Globalization.NumberStyles.HexNumber);
            byte b = byte.Parse(hex.Substring(4, 2), System.Globalization.NumberStyles.HexNumber);
    
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
                case 0: r = v; g = t; b = p; break;
                case 1: r = q; g = v; b = p; break;
                case 2: r = p; g = v; b = t; break;
                case 3: r = p; g = q; b = v; break;
                case 4: r = t; g = p; b = v; break;
                case 5: r = v; g = p; b = q; break;
                default: r = g = b = v; break;
            }
    
            return new Color(r, g, b);
        }
    }
}