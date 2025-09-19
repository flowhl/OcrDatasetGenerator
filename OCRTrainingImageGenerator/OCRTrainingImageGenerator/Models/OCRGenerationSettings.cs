using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Xml.Serialization;

namespace OCRTrainingImageGenerator.Models
{
    [Serializable]
    public class OCRGenerationSettings
    {
        // Text Content
        public string StringsFilePath { get; set; } = "";

        // Fonts
        public string FontFolderPath { get; set; } = "";
        public RangeOrFixed FontSize { get; set; } = new RangeOrFixed { Fixed = 24 };
        public TextAlignment HorizontalAlignment { get; set; } = TextAlignment.Left;
        public VerticalAlignment VerticalAlignment { get; set; } = VerticalAlignment.Center;
        public RangeOrFixed CharacterSpacing { get; set; } = new RangeOrFixed { Fixed = 0 };
        public RangeOrFixed LineSpacing { get; set; } = new RangeOrFixed { Fixed = 1.0 };

        // Colors
        public List<ColorSetting> ForegroundColors { get; set; } = new List<ColorSetting>();
        public BackgroundSetting Background { get; set; } = new BackgroundSetting();

        // Image Dimensions & Margins
        public RangeOrFixed InitialHeight { get; set; } = new RangeOrFixed { Fixed = 64 };
        public RangeOrFixed RescaledHeight { get; set; } = new RangeOrFixed { Fixed = 32 };
        public MarginSettings Margins { get; set; } = new MarginSettings();

        // Distortions
        public RangeOrFixed BlurRadius { get; set; } = new RangeOrFixed { Fixed = 0 };
        public RangeOrFixed WarpStrength { get; set; } = new RangeOrFixed { Fixed = 0 };
        public RangeOrFixed SkewAngle { get; set; } = new RangeOrFixed { Fixed = 0 };
        public RangeOrFixed RotationAngle { get; set; } = new RangeOrFixed { Fixed = 0 };

        // Noise & Artifacts
        public RangeOrFixed GaussianNoise { get; set; } = new RangeOrFixed { Fixed = 0 };
        public RangeOrFixed SaltPepperNoise { get; set; } = new RangeOrFixed { Fixed = 0 };
        public bool EnableJpgArtifacts { get; set; } = false;

        // Output Settings
        public RangeOrFixed JpgQuality { get; set; } = new RangeOrFixed { Fixed = 95 };

        // Effects
        public DropShadowSettings DropShadow { get; set; } = new DropShadowSettings();
    }

    [Serializable]
    public class RangeOrFixed
    {
        [XmlAttribute]
        public bool UseRange { get; set; } = false;

        public double Fixed { get; set; } = 0;
        public double Min { get; set; } = 0;
        public double Max { get; set; } = 1;

        public double GetRandomValue(Random random = null)
        {
            if (!UseRange) return Fixed;

            random = random ?? new Random();
            return random.NextDouble() * (Max - Min) + Min;
        }
    }

    [Serializable]
    public class ColorSetting
    {
        public byte R { get; set; } = 0;
        public byte G { get; set; } = 0;
        public byte B { get; set; } = 0;
        public byte A { get; set; } = 255;

        [XmlIgnore]
        public System.Windows.Media.Color Color
        {
            get => System.Windows.Media.Color.FromArgb(A, R, G, B);
            set { A = value.A; R = value.R; G = value.G; B = value.B; }
        }
    }

    [Serializable]
    public class BackgroundSetting
    {
        public BackgroundType Type { get; set; } = BackgroundType.SolidColor;
        public ColorSetting SolidColor { get; set; } = new ColorSetting { R = 255, G = 255, B = 255 };
        public ColorSetting GradientStart { get; set; } = new ColorSetting { R = 255, G = 255, B = 255 };
        public ColorSetting GradientEnd { get; set; } = new ColorSetting { R = 240, G = 240, B = 240 };
        public double GradientAngle { get; set; } = 0;
    }

    [Serializable]
    public class MarginSettings
    {
        public RangeOrFixed Top { get; set; } = new RangeOrFixed { Fixed = 5 };
        public RangeOrFixed Left { get; set; } = new RangeOrFixed { Fixed = 5 };
        public RangeOrFixed Right { get; set; } = new RangeOrFixed { Fixed = 5 };
        public RangeOrFixed Bottom { get; set; } = new RangeOrFixed { Fixed = 5 };
    }

    [Serializable]
    public class DropShadowSettings
    {
        public bool Enabled { get; set; } = false;
        public RangeOrFixed OffsetX { get; set; } = new RangeOrFixed { Fixed = 2 };
        public RangeOrFixed OffsetY { get; set; } = new RangeOrFixed { Fixed = 2 };
        public RangeOrFixed BlurRadius { get; set; } = new RangeOrFixed { Fixed = 1 };
        public RangeOrFixed Opacity { get; set; } = new RangeOrFixed { Fixed = 0.5 };
        public ColorSetting ShadowColor { get; set; } = new ColorSetting { R = 0, G = 0, B = 0 };
    }

    public enum BackgroundType
    {
        SolidColor,
        LinearGradient
    }

    public enum TextAlignment
    {
        Left,
        Center,
        Right
    }

    public enum VerticalAlignment
    {
        Top,
        Center,
        Bottom
    }
}