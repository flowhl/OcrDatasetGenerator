using System;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Media.Imaging;
using OCRTrainingImageGenerator.Models;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using UserControl = System.Windows.Controls.UserControl;
using DrawingFontFamily = System.Drawing.FontFamily;
using DrawingFontStyle = System.Drawing.FontStyle;
using OpenCvSharp.WpfExtensions;

namespace OCRTrainingImageGenerator.Controls
{
    public partial class FontSelector : UserControl
    {
        public ObservableCollection<FontSelectionItem> Fonts { get; private set; }

        public FontSelector()
        {
            InitializeComponent();
            Fonts = new ObservableCollection<FontSelectionItem>();
            FontsItemsControl.ItemsSource = Fonts;
        }

        public void SetFontFolder(string folderPath)
        {
            FontFolderPathBox.Text = folderPath;
            LoadFonts(folderPath);
        }

        public void LoadEnabledFonts(System.Collections.Generic.List<FontSetting> enabledFonts)
        {
            foreach (var fontItem in Fonts)
            {
                var enabledFont = enabledFonts.FirstOrDefault(f => f.FilePath == fontItem.FilePath);
                fontItem.IsEnabled = enabledFont?.IsEnabled ?? false;
            }
            UpdateStatus();
        }

        public System.Collections.Generic.List<FontSetting> GetEnabledFonts()
        {
            return Fonts.Where(f => f.IsEnabled)
                       .Select(f => new FontSetting
                       {
                           FilePath = f.FilePath,
                           FileName = f.FileName,
                           IsEnabled = f.IsEnabled
                       })
                       .ToList();
        }

        private void BrowseFontFolder_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new FolderBrowserDialog();

            if (dialog.ShowDialog() == DialogResult.OK)
            {
                SetFontFolder(dialog.SelectedPath);
            }
        }

        private void LoadFonts(string folderPath)
        {
            Fonts.Clear();

            if (string.IsNullOrEmpty(folderPath) || !Directory.Exists(folderPath))
            {
                StatusLabel.Text = "Invalid folder path";
                return;
            }

            try
            {
                var extensions = new[] { ".ttf", ".otf" };
                var fontFiles = Directory.GetFiles(folderPath, "*.*", SearchOption.AllDirectories)
                    .Where(file => extensions.Contains(Path.GetExtension(file).ToLowerInvariant()))
                    .ToList();

                if (!fontFiles.Any())
                {
                    StatusLabel.Text = "No font files found";
                    return;
                }

                foreach (var fontFile in fontFiles.OrderBy(f => Path.GetFileName(f)))
                {
                    var fontItem = new FontSelectionItem
                    {
                        FilePath = fontFile,
                        FileName = Path.GetFileName(fontFile),
                        IsEnabled = true // Default to enabled
                    };

                    // Try to get the actual font family name and generate preview
                    try
                    {
                        fontItem.FontFamilyName = GetFontFamilyName(fontFile);
                        fontItem.PreviewImage = GeneratePreviewImage(fontFile);
                    }
                    catch (Exception ex)
                    {
                        // Fallback to filename without extension
                        fontItem.FontFamilyName = Path.GetFileNameWithoutExtension(fontFile);
                        fontItem.PreviewImage = GenerateErrorPreview($"Error: {ex.Message}");
                    }

                    Fonts.Add(fontItem);
                }

                UpdateStatus();
            }
            catch (Exception ex)
            {
                StatusLabel.Text = $"Error loading fonts: {ex.Message}";
            }
        }

        private string GetFontFamilyName(string fontPath)
        {
            try
            {
                using (var fontCollection = new PrivateFontCollection())
                {
                    fontCollection.AddFontFile(fontPath);
                    if (fontCollection.Families.Length > 0)
                    {
                        return fontCollection.Families[0].Name;
                    }
                }
            }
            catch
            {
                // Ignore errors and use fallback
            }

            return Path.GetFileNameWithoutExtension(fontPath);
        }

        private BitmapSource GeneratePreviewImage(string fontPath)
        {
            const string previewText = "The quick brown fox";
            const float fontSize = 16f;
            const int imageWidth = 300;
            const int imageHeight = 30;

            try
            {
                // Load font
                Font font;
                try
                {
                    var fontCollection = new PrivateFontCollection();
                    fontCollection.AddFontFile(fontPath);
                    font = new Font(fontCollection.Families[0], fontSize, DrawingFontStyle.Regular);
                }
                catch
                {
                    // Fallback to system font
                    font = new Font(DrawingFontFamily.GenericSansSerif, fontSize, DrawingFontStyle.Regular);
                }

                using (font)
                using (var bitmap = new Bitmap(imageWidth, imageHeight))
                using (var graphics = Graphics.FromImage(bitmap))
                {
                    // Configure graphics quality
                    graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    graphics.TextRenderingHint = TextRenderingHint.AntiAlias;
                    graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;

                    // White background
                    graphics.Clear(Color.White);

                    // Measure text for centering
                    var textSize = graphics.MeasureString(previewText, font);
                    var x = Math.Max(0, (imageWidth - textSize.Width) / 2);
                    var y = Math.Max(0, (imageHeight - textSize.Height) / 2);

                    // Draw text in black
                    using (var brush = new SolidBrush(Color.Black))
                    {
                        graphics.DrawString(previewText, font, brush, x, y);
                    }

                    // Convert to OpenCV Mat and then to WPF BitmapSource
                    using (var mat = bitmap.ToMat())
                    {
                        return mat.ToBitmapSource();
                    }
                }
            }
            catch (Exception ex)
            {
                return GenerateErrorPreview($"Preview failed: {ex.Message}");
            }
        }

        private BitmapSource GenerateErrorPreview(string errorMessage)
        {
            const int imageWidth = 300;
            const int imageHeight = 30;

            try
            {
                using (var bitmap = new Bitmap(imageWidth, imageHeight))
                using (var graphics = Graphics.FromImage(bitmap))
                using (var font = new Font(DrawingFontFamily.GenericSansSerif, 10f, DrawingFontStyle.Regular))
                {
                    graphics.Clear(Color.LightGray);

                    using (var brush = new SolidBrush(Color.Red))
                    {
                        graphics.DrawString(errorMessage, font, brush, 5, 5);
                    }

                    using (var mat = bitmap.ToMat())
                    {
                        return mat.ToBitmapSource();
                    }
                }
            }
            catch
            {
                // Create a minimal fallback image
                using (var mat = new Mat(imageHeight, imageWidth, MatType.CV_8UC3, new Scalar(200, 200, 200)))
                {
                    return mat.ToBitmapSource();
                }
            }
        }

        private void SelectAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var font in Fonts)
            {
                font.IsEnabled = true;
            }
            UpdateStatus();
        }

        private void DeselectAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var font in Fonts)
            {
                font.IsEnabled = false;
            }
            UpdateStatus();
        }

        private void UpdateStatus()
        {
            var enabledCount = Fonts.Count(f => f.IsEnabled);
            var totalCount = Fonts.Count;

            if (totalCount == 0)
            {
                StatusLabel.Text = "No fonts loaded";
            }
            else
            {
                StatusLabel.Text = $"{enabledCount} of {totalCount} fonts enabled";
            }
        }
    }
}