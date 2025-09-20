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
        public List<BackgroundSetting> Backgrounds { get; set; } = new List<BackgroundSetting>();

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

        // Legacy property for backward compatibility
        [XmlIgnore]
        public BackgroundSetting Background
        {
            get => Backgrounds.Count > 0 ? Backgrounds[0] : new BackgroundSetting();
            set
            {
                if (Backgrounds.Count == 0)
                    Backgrounds.Add(value);
                else
                    Backgrounds[0] = value;
            }
        }
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
        public string Name { get; set; } = "Background";
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

    // New model for batch generation
    public class BatchGenerationJob : INotifyPropertyChanged
    {
        private string _settingsFilePath = "";
        private int _imageCount = 1000;
        private string _subfolderName = "";
        private JobStatus _status = JobStatus.Pending;
        private string _statusMessage = "";
        private int _progress = 0;

        public string SettingsFilePath
        {
            get => _settingsFilePath;
            set { _settingsFilePath = value; OnPropertyChanged(); UpdateSubfolderName(); }
        }

        public int ImageCount
        {
            get => _imageCount;
            set { _imageCount = value; OnPropertyChanged(); }
        }

        public string SubfolderName
        {
            get => _subfolderName;
            set { _subfolderName = value; OnPropertyChanged(); }
        }

        public JobStatus Status
        {
            get => _status;
            set { _status = value; OnPropertyChanged(); }
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set { _statusMessage = value; OnPropertyChanged(); }
        }

        public int Progress
        {
            get => _progress;
            set { _progress = value; OnPropertyChanged(); }
        }

        private void UpdateSubfolderName()
        {
            if (!string.IsNullOrEmpty(SettingsFilePath))
            {
                SubfolderName = System.IO.Path.GetFileNameWithoutExtension(SettingsFilePath);
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public enum JobStatus
    {
        Pending,
        Running,
        Completed,
        Failed,
        Cancelled
    }

    // Model for saving/loading batch configuration
    [Serializable]
    public class BatchConfiguration
    {
        public string OutputFolder { get; set; } = "";
        public int MaxThreads { get; set; } = 4;
        public List<BatchJobInfo> Jobs { get; set; } = new List<BatchJobInfo>();
        public DateTime LastRunDate { get; set; } = DateTime.Now;
    }

    [Serializable]
    public class BatchJobInfo
    {
        public string SettingsFilePath { get; set; } = "";
        public int ImageCount { get; set; } = 1000;
        public string SubfolderName { get; set; } = "";
        public JobStatus LastStatus { get; set; } = JobStatus.Pending;
        public string LastStatusMessage { get; set; } = "";
    }
}