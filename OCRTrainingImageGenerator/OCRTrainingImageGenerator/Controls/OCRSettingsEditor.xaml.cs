using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Xml.Serialization;
using OCRTrainingImageGenerator.Models;
using OCRTrainingImageGenerator.Services;
using Microsoft.Win32;
using System.Windows.Forms;
using MessageBox = System.Windows.MessageBox;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Windows.Threading;
using Color = System.Windows.Media.Color;
using VerticalAlignment = OCRTrainingImageGenerator.Models.VerticalAlignment;
using TextAlignment = OCRTrainingImageGenerator.Models.TextAlignment;

namespace OCRTrainingImageGenerator.Controls
{
    public partial class OCRSettingsEditor : System.Windows.Controls.UserControl
    {
        public OCRGenerationSettings Settings { get; private set; }
        public ObservableCollection<ColorDisplay> ForegroundColors { get; private set; }
        public ObservableCollection<BackgroundDisplay> Backgrounds { get; private set; }

        private string _currentFilePath;
        private ImageGenerationService _imageService;
        private DispatcherTimer _autoPreviewTimer;
        private bool _isAutoPreviewEnabled = false;
        private List<string> _previewStrings;
        private List<string> _previewFonts;
        private Random _previewRandom = new Random();

        public OCRSettingsEditor()
        {
            InitializeComponent();
            Settings = new OCRGenerationSettings();
            ForegroundColors = new ObservableCollection<ColorDisplay>();
            Backgrounds = new ObservableCollection<BackgroundDisplay>();
            _imageService = new ImageGenerationService();

            InitializeUI();
            LoadSettingsToUI();
            InitializePreview();
        }

        private void InitializeUI()
        {
            // Initialize ComboBoxes
            HorizontalAlignmentBox.ItemsSource = Enum.GetValues(typeof(TextAlignment));
            VerticalAlignmentBox.ItemsSource = Enum.GetValues(typeof(VerticalAlignment));

            // Set default values
            HorizontalAlignmentBox.SelectedItem = TextAlignment.Left;
            VerticalAlignmentBox.SelectedItem = OCRTrainingImageGenerator.Models.VerticalAlignment.Center;

            // Initialize lists
            ForegroundColorsList.ItemsSource = ForegroundColors;
            BackgroundsList.ItemsSource = Backgrounds;
        }

        private void InitializePreview()
        {
            // Setup auto preview timer
            _autoPreviewTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(1500) // 1.5 second delay after last change
            };
            _autoPreviewTimer.Tick += (s, e) =>
            {
                _autoPreviewTimer.Stop();
                if (_isAutoPreviewEnabled)
                {
                    GeneratePreview_Click(null, null);
                }
            };

            // Subscribe to UI change events for auto preview
            SubscribeToUIChanges();

            // Load default preview data
            LoadPreviewData();
        }

        private void LoadPreviewData()
        {
            // Load default strings for preview
            _previewStrings = new List<string>
            {
                "Hello World",
                "The quick brown fox",
                "jumps over the lazy dog",
                "1234567890",
                "ABCDEFGHIJKLMNOP",
                "Sample Text Preview",
                "Training Data Example",
                "OCR Generation Test"
            };

            // Try to load strings from settings if available
            if (!string.IsNullOrEmpty(Settings?.StringsFilePath) && File.Exists(Settings.StringsFilePath))
            {
                try
                {
                    var fileStrings = File.ReadAllLines(Settings.StringsFilePath)
                        .Where(line => !string.IsNullOrWhiteSpace(line))
                        .Take(20) // Limit for preview
                        .ToList();

                    if (fileStrings.Any())
                    {
                        _previewStrings = fileStrings;
                    }
                }
                catch
                {
                    // Keep default strings if file loading fails
                }
            }

            // Load enabled fonts for preview
            _previewFonts = new List<string>();
            try
            {
                var enabledFonts = FontSelectorControl.GetEnabledFonts();
                if (enabledFonts.Any())
                {
                    _previewFonts = enabledFonts.Select(f => f.FilePath).Take(10).ToList();
                }

                // Fallback to system fonts if no enabled custom fonts
                if (!_previewFonts.Any())
                {
                    var systemFonts = System.Windows.Media.Fonts.SystemFontFamilies
                        .Take(5)
                        .Select(ff => ff.Source)
                        .ToList();
                    _previewFonts = systemFonts;
                }
            }
            catch
            {
                // Ultimate fallback
                _previewFonts = new List<string> { "Arial", "Times New Roman", "Courier New" };
            }
        }

        private void SubscribeToUIChanges()
        {
            // Subscribe to text box changes
            foreach (var textBox in FindVisualChildren<System.Windows.Controls.TextBox>(this))
            {
                textBox.TextChanged += OnUIChanged;
            }

            // Subscribe to checkbox changes
            foreach (var checkBox in FindVisualChildren<System.Windows.Controls.CheckBox>(this))
            {
                checkBox.Checked += OnUIChanged;
                checkBox.Unchecked += OnUIChanged;
            }

            // Subscribe to combobox changes
            foreach (var comboBox in FindVisualChildren<System.Windows.Controls.ComboBox>(this))
            {
                comboBox.SelectionChanged += OnUIChanged;
            }
        }

        private void OnUIChanged(object sender, EventArgs e)
        {
            if (_isAutoPreviewEnabled && _autoPreviewTimer != null)
            {
                _autoPreviewTimer.Stop();
                _autoPreviewTimer.Start();
            }
        }

        private async void GeneratePreview_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                PreviewStatusLabel.Text = "Generating preview...";
                GeneratePreviewButton.IsEnabled = false;

                // Update preview data in case settings changed
                LoadPreviewData();

                // Save current UI to settings for preview
                SaveUIToSettings();

                // Validate basic requirements
                if (!_previewStrings.Any())
                {
                    PreviewStatusLabel.Text = "No preview strings available";
                    return;
                }

                if (!_previewFonts.Any())
                {
                    PreviewStatusLabel.Text = "No preview fonts available";
                    return;
                }

                await Task.Run(() =>
                {
                    try
                    {
                        // Get random string and font for preview
                        var randomString = _previewStrings[_previewRandom.Next(_previewStrings.Count)];
                        var randomFont = _previewFonts[_previewRandom.Next(_previewFonts.Count)];

                        // Generate preview image
                        using (var mat = _imageService.GenerateImage(Settings, randomString, randomFont))
                        {
                            // Convert to WPF BitmapSource
                            var bitmap = mat.ToBitmap();

                            Dispatcher.Invoke(() =>
                            {
                                try
                                {
                                    var bitmapSource = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                                        bitmap.GetHbitmap(),
                                        IntPtr.Zero,
                                        Int32Rect.Empty,
                                        BitmapSizeOptions.FromEmptyOptions());

                                    PreviewImage.Source = bitmapSource;
                                    PreviewStatusLabel.Text = $"Preview: \"{randomString}\"";
                                }
                                finally
                                {
                                    bitmap?.Dispose();
                                }
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        Dispatcher.Invoke(() =>
                        {
                            PreviewStatusLabel.Text = $"Preview failed: {ex.Message}";
                            PreviewImage.Source = null;
                        });
                    }
                });
            }
            catch (Exception ex)
            {
                PreviewStatusLabel.Text = $"Preview error: {ex.Message}";
                PreviewImage.Source = null;
            }
            finally
            {
                GeneratePreviewButton.IsEnabled = true;
            }
        }

        private void ToggleAutoPreview_Click(object sender, RoutedEventArgs e)
        {
            _isAutoPreviewEnabled = !_isAutoPreviewEnabled;
            AutoPreviewCheck.IsChecked = _isAutoPreviewEnabled;

            AutoPreviewButton.Content = _isAutoPreviewEnabled ? "Disable Auto" : "Enable Auto";

            if (_isAutoPreviewEnabled)
            {
                // Generate initial preview
                GeneratePreview_Click(null, null);
            }
        }

        // Helper method to find child controls
        private static IEnumerable<T> FindVisualChildren<T>(DependencyObject depObj) where T : DependencyObject
        {
            if (depObj != null)
            {
                for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
                {
                    DependencyObject child = VisualTreeHelper.GetChild(depObj, i);
                    if (child != null && child is T)
                    {
                        yield return (T)child;
                    }

                    foreach (T childOfChild in FindVisualChildren<T>(child))
                    {
                        yield return childOfChild;
                    }
                }
            }
        }

        private void LoadSettingsToUI()
        {
            // Text Content
            StringsFilePathBox.Text = Settings.StringsFilePath;

            // Fonts
            FontFolderPathBox.Text = Settings.FontFolderPath;

            // Set font folder and load enabled fonts
            if (!string.IsNullOrEmpty(Settings.FontFolderPath))
            {
                FontSelectorControl.SetFontFolder(Settings.FontFolderPath);
                FontSelectorControl.LoadEnabledFonts(Settings.EnabledFonts);
            }

            LoadRangeOrFixedToUI(Settings.FontSize, FontSizeRangeCheck, FontSizeFixedBox, FontSizeMinBox, FontSizeMaxBox);
            LoadRangeOrFixedToUI(Settings.CharacterSpacing, CharSpacingRangeCheck, CharSpacingFixedBox, CharSpacingMinBox, CharSpacingMaxBox);
            LoadRangeOrFixedToUI(Settings.LineSpacing, LineSpacingRangeCheck, LineSpacingFixedBox, LineSpacingMinBox, LineSpacingMaxBox);

            HorizontalAlignmentBox.SelectedItem = Settings.HorizontalAlignment;
            VerticalAlignmentBox.SelectedItem = Settings.VerticalAlignment;

            // Colors
            ForegroundColors.Clear();
            foreach (var color in Settings.ForegroundColors)
            {
                ForegroundColors.Add(new ColorDisplay(color));
            }

            // Backgrounds
            Backgrounds.Clear();
            foreach (var background in Settings.Backgrounds)
            {
                Backgrounds.Add(new BackgroundDisplay(background));
            }

            // Add default background if none exist
            if (Backgrounds.Count == 0)
            {
                var defaultBackground = new BackgroundSetting
                {
                    Name = "Default White",
                    Type = BackgroundType.SolidColor,
                    SolidColor = new ColorSetting { R = 255, G = 255, B = 255, A = 255 }
                };
                Backgrounds.Add(new BackgroundDisplay(defaultBackground));
            }

            // Dimensions & Margins
            LoadRangeOrFixedToUI(Settings.InitialHeight, InitialHeightRangeCheck, InitialHeightFixedBox, InitialHeightMinBox, InitialHeightMaxBox);
            LoadRangeOrFixedToUI(Settings.RescaledHeight, RescaledHeightRangeCheck, RescaledHeightFixedBox, RescaledHeightMinBox, RescaledHeightMaxBox);

            LoadRangeOrFixedToUI(Settings.Margins.Top, MarginTopRangeCheck, MarginTopFixedBox, MarginTopMinBox, MarginTopMaxBox);
            LoadRangeOrFixedToUI(Settings.Margins.Left, MarginLeftRangeCheck, MarginLeftFixedBox, MarginLeftMinBox, MarginLeftMaxBox);
            LoadRangeOrFixedToUI(Settings.Margins.Right, MarginRightRangeCheck, MarginRightFixedBox, MarginRightMinBox, MarginRightMaxBox);
            LoadRangeOrFixedToUI(Settings.Margins.Bottom, MarginBottomRangeCheck, MarginBottomFixedBox, MarginBottomMinBox, MarginBottomMaxBox);

            // Distortions
            LoadRangeOrFixedToUI(Settings.BlurRadius, BlurRangeCheck, BlurFixedBox, BlurMinBox, BlurMaxBox);
            LoadRangeOrFixedToUI(Settings.WarpStrength, WarpRangeCheck, WarpFixedBox, WarpMinBox, WarpMaxBox);
            LoadRangeOrFixedToUI(Settings.SkewAngle, SkewRangeCheck, SkewFixedBox, SkewMinBox, SkewMaxBox);
            LoadRangeOrFixedToUI(Settings.RotationAngle, RotationRangeCheck, RotationFixedBox, RotationMinBox, RotationMaxBox);

            // Noise & Effects
            LoadRangeOrFixedToUI(Settings.GaussianNoise, GaussianNoiseRangeCheck, GaussianNoiseFixedBox, GaussianNoiseMinBox, GaussianNoiseMaxBox);
            LoadRangeOrFixedToUI(Settings.SaltPepperNoise, SaltPepperNoiseRangeCheck, SaltPepperNoiseFixedBox, SaltPepperNoiseMinBox, SaltPepperNoiseMaxBox);
            EnableJpgArtifactsCheck.IsChecked = Settings.EnableJpgArtifacts;

            // Drop Shadow
            DropShadowEnabledCheck.IsChecked = Settings.DropShadow.Enabled;
            UpdateDropShadowUI();
            UpdateColorRectangle(ShadowColorRect, Settings.DropShadow.ShadowColor);
            LoadRangeOrFixedToUI(Settings.DropShadow.OffsetX, ShadowOffsetXRangeCheck, ShadowOffsetXFixedBox, ShadowOffsetXMinBox, ShadowOffsetXMaxBox);
            LoadRangeOrFixedToUI(Settings.DropShadow.OffsetY, ShadowOffsetYRangeCheck, ShadowOffsetYFixedBox, ShadowOffsetYMinBox, ShadowOffsetYMaxBox);
            LoadRangeOrFixedToUI(Settings.DropShadow.BlurRadius, ShadowBlurRangeCheck, ShadowBlurFixedBox, ShadowBlurMinBox, ShadowBlurMaxBox);
            LoadRangeOrFixedToUI(Settings.DropShadow.Opacity, ShadowOpacityRangeCheck, ShadowOpacityFixedBox, ShadowOpacityMinBox, ShadowOpacityMaxBox);

            // Output
            LoadRangeOrFixedToUI(Settings.JpgQuality, JpgQualityRangeCheck, JpgQualityFixedBox, JpgQualityMinBox, JpgQualityMaxBox);
        }

        private void SaveUIToSettings()
        {
            // Text Content
            Settings.StringsFilePath = StringsFilePathBox.Text;

            // Fonts
            Settings.FontFolderPath = FontFolderPathBox.Text;
            Settings.EnabledFonts = FontSelectorControl.GetEnabledFonts();

            SaveRangeOrFixedFromUI(Settings.FontSize, FontSizeRangeCheck, FontSizeFixedBox, FontSizeMinBox, FontSizeMaxBox);
            SaveRangeOrFixedFromUI(Settings.CharacterSpacing, CharSpacingRangeCheck, CharSpacingFixedBox, CharSpacingMinBox, CharSpacingMaxBox);
            SaveRangeOrFixedFromUI(Settings.LineSpacing, LineSpacingRangeCheck, LineSpacingFixedBox, LineSpacingMinBox, LineSpacingMaxBox);

            Settings.HorizontalAlignment = (TextAlignment)HorizontalAlignmentBox.SelectedItem;
            Settings.VerticalAlignment = (VerticalAlignment)VerticalAlignmentBox.SelectedItem;

            // Colors
            Settings.ForegroundColors.Clear();
            foreach (var colorDisplay in ForegroundColors)
            {
                Settings.ForegroundColors.Add(colorDisplay.ColorSetting);
            }

            // Backgrounds
            Settings.Backgrounds.Clear();
            foreach (var backgroundDisplay in Backgrounds)
            {
                Settings.Backgrounds.Add(backgroundDisplay.BackgroundSetting);
            }

            // Dimensions & Margins
            SaveRangeOrFixedFromUI(Settings.InitialHeight, InitialHeightRangeCheck, InitialHeightFixedBox, InitialHeightMinBox, InitialHeightMaxBox);
            SaveRangeOrFixedFromUI(Settings.RescaledHeight, RescaledHeightRangeCheck, RescaledHeightFixedBox, RescaledHeightMinBox, RescaledHeightMaxBox);

            SaveRangeOrFixedFromUI(Settings.Margins.Top, MarginTopRangeCheck, MarginTopFixedBox, MarginTopMinBox, MarginTopMaxBox);
            SaveRangeOrFixedFromUI(Settings.Margins.Left, MarginLeftRangeCheck, MarginLeftFixedBox, MarginLeftMinBox, MarginLeftMaxBox);
            SaveRangeOrFixedFromUI(Settings.Margins.Right, MarginRightRangeCheck, MarginRightFixedBox, MarginRightMinBox, MarginRightMaxBox);
            SaveRangeOrFixedFromUI(Settings.Margins.Bottom, MarginBottomRangeCheck, MarginBottomFixedBox, MarginBottomMinBox, MarginBottomMaxBox);

            // Distortions
            SaveRangeOrFixedFromUI(Settings.BlurRadius, BlurRangeCheck, BlurFixedBox, BlurMinBox, BlurMaxBox);
            SaveRangeOrFixedFromUI(Settings.WarpStrength, WarpRangeCheck, WarpFixedBox, WarpMinBox, WarpMaxBox);
            SaveRangeOrFixedFromUI(Settings.SkewAngle, SkewRangeCheck, SkewFixedBox, SkewMinBox, SkewMaxBox);
            SaveRangeOrFixedFromUI(Settings.RotationAngle, RotationRangeCheck, RotationFixedBox, RotationMinBox, RotationMaxBox);

            // Noise & Effects
            SaveRangeOrFixedFromUI(Settings.GaussianNoise, GaussianNoiseRangeCheck, GaussianNoiseFixedBox, GaussianNoiseMinBox, GaussianNoiseMaxBox);
            SaveRangeOrFixedFromUI(Settings.SaltPepperNoise, SaltPepperNoiseRangeCheck, SaltPepperNoiseFixedBox, SaltPepperNoiseMinBox, SaltPepperNoiseMaxBox);
            Settings.EnableJpgArtifacts = EnableJpgArtifactsCheck.IsChecked == true;

            // Drop Shadow
            Settings.DropShadow.Enabled = DropShadowEnabledCheck.IsChecked == true;
            SaveRangeOrFixedFromUI(Settings.DropShadow.OffsetX, ShadowOffsetXRangeCheck, ShadowOffsetXFixedBox, ShadowOffsetXMinBox, ShadowOffsetXMaxBox);
            SaveRangeOrFixedFromUI(Settings.DropShadow.OffsetY, ShadowOffsetYRangeCheck, ShadowOffsetYFixedBox, ShadowOffsetYMinBox, ShadowOffsetYMaxBox);
            SaveRangeOrFixedFromUI(Settings.DropShadow.BlurRadius, ShadowBlurRangeCheck, ShadowBlurFixedBox, ShadowBlurMinBox, ShadowBlurMaxBox);
            SaveRangeOrFixedFromUI(Settings.DropShadow.Opacity, ShadowOpacityRangeCheck, ShadowOpacityFixedBox, ShadowOpacityMinBox, ShadowOpacityMaxBox);

            // Output
            SaveRangeOrFixedFromUI(Settings.JpgQuality, JpgQualityRangeCheck, JpgQualityFixedBox, JpgQualityMinBox, JpgQualityMaxBox);
        }

        #region Range/Fixed Helper Methods

        private void LoadRangeOrFixedToUI(RangeOrFixed rangeOrFixed, System.Windows.Controls.CheckBox rangeCheck,
            System.Windows.Controls.TextBox fixedBox, System.Windows.Controls.TextBox minBox, System.Windows.Controls.TextBox maxBox)
        {
            rangeCheck.IsChecked = rangeOrFixed.UseRange;
            fixedBox.Text = rangeOrFixed.Fixed.ToString(CultureInfo.InvariantCulture);
            minBox.Text = rangeOrFixed.Min.ToString(CultureInfo.InvariantCulture);
            maxBox.Text = rangeOrFixed.Max.ToString(CultureInfo.InvariantCulture);

            UpdateRangeOrFixedUI(rangeCheck, fixedBox, minBox, maxBox);
        }

        private void SaveRangeOrFixedFromUI(RangeOrFixed rangeOrFixed, System.Windows.Controls.CheckBox rangeCheck,
            System.Windows.Controls.TextBox fixedBox, System.Windows.Controls.TextBox minBox, System.Windows.Controls.TextBox maxBox)
        {
            rangeOrFixed.UseRange = rangeCheck.IsChecked == true;

            if (double.TryParse(fixedBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double fixedVal))
                rangeOrFixed.Fixed = fixedVal;

            if (double.TryParse(minBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double minVal))
                rangeOrFixed.Min = minVal;

            if (double.TryParse(maxBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double maxVal))
                rangeOrFixed.Max = maxVal;
        }

        private void UpdateRangeOrFixedUI(System.Windows.Controls.CheckBox rangeCheck, System.Windows.Controls.TextBox fixedBox,
            System.Windows.Controls.TextBox minBox, System.Windows.Controls.TextBox maxBox)
        {
            bool useRange = rangeCheck.IsChecked == true;
            fixedBox.IsEnabled = !useRange;
            minBox.IsEnabled = useRange;
            maxBox.IsEnabled = useRange;
        }

        #endregion

        #region UI Event Handlers

        // Range/Fixed toggle events
        private void FontSizeRange_Changed(object sender, RoutedEventArgs e)
        {
            UpdateRangeOrFixedUI(FontSizeRangeCheck, FontSizeFixedBox, FontSizeMinBox, FontSizeMaxBox);
        }

        private void CharSpacingRange_Changed(object sender, RoutedEventArgs e)
        {
            UpdateRangeOrFixedUI(CharSpacingRangeCheck, CharSpacingFixedBox, CharSpacingMinBox, CharSpacingMaxBox);
        }

        private void LineSpacingRange_Changed(object sender, RoutedEventArgs e)
        {
            UpdateRangeOrFixedUI(LineSpacingRangeCheck, LineSpacingFixedBox, LineSpacingMinBox, LineSpacingMaxBox);
        }

        private void InitialHeightRange_Changed(object sender, RoutedEventArgs e)
        {
            UpdateRangeOrFixedUI(InitialHeightRangeCheck, InitialHeightFixedBox, InitialHeightMinBox, InitialHeightMaxBox);
        }

        private void RescaledHeightRange_Changed(object sender, RoutedEventArgs e)
        {
            UpdateRangeOrFixedUI(RescaledHeightRangeCheck, RescaledHeightFixedBox, RescaledHeightMinBox, RescaledHeightMaxBox);
        }

        private void MarginTopRange_Changed(object sender, RoutedEventArgs e)
        {
            UpdateRangeOrFixedUI(MarginTopRangeCheck, MarginTopFixedBox, MarginTopMinBox, MarginTopMaxBox);
        }

        private void MarginLeftRange_Changed(object sender, RoutedEventArgs e)
        {
            UpdateRangeOrFixedUI(MarginLeftRangeCheck, MarginLeftFixedBox, MarginLeftMinBox, MarginLeftMaxBox);
        }

        private void MarginRightRange_Changed(object sender, RoutedEventArgs e)
        {
            UpdateRangeOrFixedUI(MarginRightRangeCheck, MarginRightFixedBox, MarginRightMinBox, MarginRightMaxBox);
        }

        private void MarginBottomRange_Changed(object sender, RoutedEventArgs e)
        {
            UpdateRangeOrFixedUI(MarginBottomRangeCheck, MarginBottomFixedBox, MarginBottomMinBox, MarginBottomMaxBox);
        }

        private void BlurRange_Changed(object sender, RoutedEventArgs e)
        {
            UpdateRangeOrFixedUI(BlurRangeCheck, BlurFixedBox, BlurMinBox, BlurMaxBox);
        }

        private void WarpRange_Changed(object sender, RoutedEventArgs e)
        {
            UpdateRangeOrFixedUI(WarpRangeCheck, WarpFixedBox, WarpMinBox, WarpMaxBox);
        }

        private void SkewRange_Changed(object sender, RoutedEventArgs e)
        {
            UpdateRangeOrFixedUI(SkewRangeCheck, SkewFixedBox, SkewMinBox, SkewMaxBox);
        }

        private void RotationRange_Changed(object sender, RoutedEventArgs e)
        {
            UpdateRangeOrFixedUI(RotationRangeCheck, RotationFixedBox, RotationMinBox, RotationMaxBox);
        }

        private void GaussianNoiseRange_Changed(object sender, RoutedEventArgs e)
        {
            UpdateRangeOrFixedUI(GaussianNoiseRangeCheck, GaussianNoiseFixedBox, GaussianNoiseMinBox, GaussianNoiseMaxBox);
        }

        private void SaltPepperNoiseRange_Changed(object sender, RoutedEventArgs e)
        {
            UpdateRangeOrFixedUI(SaltPepperNoiseRangeCheck, SaltPepperNoiseFixedBox, SaltPepperNoiseMinBox, SaltPepperNoiseMaxBox);
        }

        private void JpgQualityRange_Changed(object sender, RoutedEventArgs e)
        {
            UpdateRangeOrFixedUI(JpgQualityRangeCheck, JpgQualityFixedBox, JpgQualityMinBox, JpgQualityMaxBox);
        }

        // Drop Shadow events
        private void DropShadowEnabled_Changed(object sender, RoutedEventArgs e)
        {
            UpdateDropShadowUI();
        }

        private void UpdateDropShadowUI()
        {
            DropShadowPanel.IsEnabled = DropShadowEnabledCheck.IsChecked == true;
        }

        private void ShadowOffsetXRange_Changed(object sender, RoutedEventArgs e)
        {
            UpdateRangeOrFixedUI(ShadowOffsetXRangeCheck, ShadowOffsetXFixedBox, ShadowOffsetXMinBox, ShadowOffsetXMaxBox);
        }

        private void ShadowOffsetYRange_Changed(object sender, RoutedEventArgs e)
        {
            UpdateRangeOrFixedUI(ShadowOffsetYRangeCheck, ShadowOffsetYFixedBox, ShadowOffsetYMinBox, ShadowOffsetYMaxBox);
        }

        private void ShadowBlurRange_Changed(object sender, RoutedEventArgs e)
        {
            UpdateRangeOrFixedUI(ShadowBlurRangeCheck, ShadowBlurFixedBox, ShadowBlurMinBox, ShadowBlurMaxBox);
        }

        private void ShadowOpacityRange_Changed(object sender, RoutedEventArgs e)
        {
            UpdateRangeOrFixedUI(ShadowOpacityRangeCheck, ShadowOpacityFixedBox, ShadowOpacityMinBox, ShadowOpacityMaxBox);
        }

        #endregion

        #region File Operations

        private void LoadButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "XML files (*.xml)|*.xml|All files (*.*)|*.*",
                FilterIndex = 1
            };

            if (dialog.ShowDialog() == true)
            {
                LoadSettings(dialog.FileName);
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_currentFilePath))
            {
                SaveAsButton_Click(sender, e);
            }
            else
            {
                SaveSettings(_currentFilePath);
            }
        }

        private void SaveAsButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "XML files (*.xml)|*.xml|All files (*.*)|*.*",
                FilterIndex = 1,
                DefaultExt = "xml"
            };

            if (dialog.ShowDialog() == true)
            {
                SaveSettings(dialog.FileName);
            }
        }

        private void LoadSettings(string filePath)
        {
            try
            {
                var serializer = new XmlSerializer(typeof(OCRGenerationSettings));
                using (var reader = new FileStream(filePath, FileMode.Open))
                {
                    Settings = (OCRGenerationSettings)serializer.Deserialize(reader);
                }

                LoadSettingsToUI();
                _currentFilePath = filePath;
                CurrentFileLabel.Text = Path.GetFileName(filePath);

                // Reload preview data after loading new settings
                LoadPreviewData();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading settings: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SaveSettings(string filePath)
        {
            try
            {
                SaveUIToSettings();

                var serializer = new XmlSerializer(typeof(OCRGenerationSettings));
                using (var writer = new FileStream(filePath, FileMode.Create))
                {
                    serializer.Serialize(writer, Settings);
                }

                _currentFilePath = filePath;
                CurrentFileLabel.Text = Path.GetFileName(filePath);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving settings: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region Browse Events

        private void BrowseStringsFile_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*",
                FilterIndex = 1
            };

            if (dialog.ShowDialog() == true)
            {
                StringsFilePathBox.Text = dialog.FileName;
                // Reload preview data when strings file changes
                LoadPreviewData();

                if (_isAutoPreviewEnabled)
                {
                    _autoPreviewTimer.Stop();
                    _autoPreviewTimer.Start();
                }
            }
        }

        private void BrowseFontFolder_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new FolderBrowserDialog();

            if (dialog.ShowDialog() == DialogResult.OK)
            {
                FontFolderPathBox.Text = dialog.SelectedPath;
                FontSelectorControl.SetFontFolder(dialog.SelectedPath);

                // Reload preview data when font folder changes
                LoadPreviewData();

                if (_isAutoPreviewEnabled)
                {
                    _autoPreviewTimer.Stop();
                    _autoPreviewTimer.Start();
                }
            }
        }

        #endregion

        #region Color Events

        private void AddForegroundColor_Click(object sender, RoutedEventArgs e)
        {
            var color = ChooseColor(Colors.Black);
            if (color.HasValue)
            {
                var colorSetting = new ColorSetting { R = color.Value.R, G = color.Value.G, B = color.Value.B, A = color.Value.A };
                ForegroundColors.Add(new ColorDisplay(colorSetting));
            }
        }

        private void RemoveForegroundColor_Click(object sender, RoutedEventArgs e)
        {
            if (ForegroundColorsList.SelectedItem is ColorDisplay selected)
            {
                ForegroundColors.Remove(selected);
            }
        }

        private void ChangeShadowColor_Click(object sender, RoutedEventArgs e)
        {
            var color = ChooseColor(Settings.DropShadow.ShadowColor.Color);
            if (color.HasValue)
            {
                Settings.DropShadow.ShadowColor.Color = color.Value;
                UpdateColorRectangle(ShadowColorRect, Settings.DropShadow.ShadowColor);
            }
        }

        private Color? ChooseColor(Color initialColor)
        {
            var dialog = new ColorDialog
            {
                Color = System.Drawing.Color.FromArgb(initialColor.A, initialColor.R, initialColor.G, initialColor.B),
                FullOpen = true
            };

            if (dialog.ShowDialog() == DialogResult.OK)
            {
                return Color.FromArgb(dialog.Color.A, dialog.Color.R, dialog.Color.G, dialog.Color.B);
            }
            return null;
        }

        private void UpdateColorRectangle(System.Windows.Shapes.Rectangle rectangle, ColorSetting colorSetting)
        {
            rectangle.Fill = new SolidColorBrush(colorSetting.Color);
        }

        #endregion

        #region Background Events

        private void AddBackground_Click(object sender, RoutedEventArgs e)
        {
            var newBackground = new BackgroundSetting
            {
                Name = $"Background {Backgrounds.Count + 1}",
                Type = BackgroundType.SolidColor,
                SolidColor = new ColorSetting { R = 255, G = 255, B = 255, A = 255 }
            };
            Backgrounds.Add(new BackgroundDisplay(newBackground));
        }

        private void RemoveBackground_Click(object sender, RoutedEventArgs e)
        {
            if (BackgroundsList.SelectedItem is BackgroundDisplay selected && Backgrounds.Count > 1)
            {
                Backgrounds.Remove(selected);
            }
        }

        private void EditBackground_Click(object sender, RoutedEventArgs e)
        {
            if (BackgroundsList.SelectedItem is BackgroundDisplay selected)
            {
                var dialog = new BackgroundEditDialog(selected.BackgroundSetting);
                if (dialog.ShowDialog() == true)
                {
                    // Refresh the display
                    var index = Backgrounds.IndexOf(selected);
                    Backgrounds[index] = new BackgroundDisplay(selected.BackgroundSetting);

                    // Trigger auto preview if enabled
                    if (_isAutoPreviewEnabled)
                    {
                        _autoPreviewTimer.Stop();
                        _autoPreviewTimer.Start();
                    }
                }
            }
        }

        #endregion
    }

    public class ColorDisplay
    {
        public ColorSetting ColorSetting { get; }
        public string DisplayText => $"RGBA({ColorSetting.R}, {ColorSetting.G}, {ColorSetting.B}, {ColorSetting.A})";

        public ColorDisplay(ColorSetting colorSetting)
        {
            ColorSetting = colorSetting;
        }

        public override string ToString() => DisplayText;
    }

    public class BackgroundDisplay
    {
        public BackgroundSetting BackgroundSetting { get; }
        public string DisplayText => $"{BackgroundSetting.Name} ({BackgroundSetting.Type})";

        public BackgroundDisplay(BackgroundSetting backgroundSetting)
        {
            BackgroundSetting = backgroundSetting;
        }

        public override string ToString() => DisplayText;
    }
}