using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Xml.Serialization;
using Microsoft.Win32;
using OCRTrainingImageGenerator.Models;
using MessageBox = System.Windows.MessageBox;
using OpenFileDialog = System.Windows.Forms.OpenFileDialog;
using SaveFileDialog = System.Windows.Forms.SaveFileDialog;
using UserControl = System.Windows.Controls.UserControl;

namespace OCRTrainingImageGenerator.Controls
{
    public partial class StringGenerator : UserControl
    {
        private StringGenerationSettings _settings;
        private string _currentFilePath;
        private Random _random = new Random();

        // OCR confusion pairs - filtered to only include valid characters
        private Dictionary<char, char[]> _confusionPairs;

        public StringGenerator()
        {
            InitializeComponent();
            _settings = new StringGenerationSettings();
            InitializeConfusionPairs();
            LoadSettingsToUI();
        }

        private void InitializeConfusionPairs()
        {
            // Build confusion pairs based on current character sets
            _confusionPairs = new Dictionary<char, char[]>();

            var allChars = BuildCharacterPool().ToHashSet();

            // Define all potential confusion pairs
            var potentialPairs = new Dictionary<char, char[]>
            {
                { '0', new[] { 'O', 'o' } },
                { 'O', new[] { '0', 'o' } },
                { 'o', new[] { '0', 'O' } },
                { '1', new[] { 'I', 'l', '|' } },
                { 'I', new[] { '1', 'l', '|' } },
                { 'l', new[] { '1', 'I', '|' } },
                { '5', new[] { 'S', 's' } },
                { 'S', new[] { '5', 's' } },
                { 's', new[] { '5', 'S' } },
                { '8', new[] { 'B' } },
                { 'B', new[] { '8', 'b' } },
                { 'b', new[] { '6', 'B' } },
                { '2', new[] { 'Z', 'z' } },
                { 'Z', new[] { '2', 'z' } },
                { 'z', new[] { '2', 'Z' } },
                { '6', new[] { 'b' } },
            };

            // Only include pairs where both the source and target characters are in the character set
            foreach (var kvp in potentialPairs)
            {
                if (allChars.Contains(kvp.Key))
                {
                    var validTargets = kvp.Value.Where(c => allChars.Contains(c)).ToArray();
                    if (validTargets.Length > 0)
                    {
                        _confusionPairs[kvp.Key] = validTargets;
                    }
                }
            }
        }

        private void LoadSettingsToUI()
        {
            // Character Sets
            IncludeUppercaseCheck.IsChecked = _settings.CharacterSets.IncludeUppercase;
            IncludeLowercaseCheck.IsChecked = _settings.CharacterSets.IncludeLowercase;
            IncludeNumbersCheck.IsChecked = _settings.CharacterSets.IncludeNumbers;
            SpecialCharsTextBox.Text = _settings.CharacterSets.SpecialCharacters;

            // Generation Mode
            if (_settings.Mode == GenerationMode.RuleBased)
                RuleBasedRadio.IsChecked = true;
            else
                PurelyRandomRadio.IsChecked = true;

            // Start Rules
            StartUppercaseCheck.IsChecked = _settings.StartRules.AllowUppercase;
            StartLowercaseCheck.IsChecked = _settings.StartRules.AllowLowercase;
            StartNumbersCheck.IsChecked = _settings.StartRules.AllowNumbers;
            StartSpecialCheck.IsChecked = _settings.StartRules.AllowSpecialChars;

            // End Rules
            EndUppercaseCheck.IsChecked = _settings.EndRules.AllowUppercase;
            EndLowercaseCheck.IsChecked = _settings.EndRules.AllowLowercase;
            EndNumbersCheck.IsChecked = _settings.EndRules.AllowNumbers;
            EndSpecialCheck.IsChecked = _settings.EndRules.AllowSpecialChars;

            // Length
            if (_settings.Length.UseRange)
            {
                RangeLengthRadio.IsChecked = true;
                MinLengthTextBox.Text = _settings.Length.MinLength.ToString();
                MaxLengthTextBox.Text = _settings.Length.MaxLength.ToString();
            }
            else
            {
                FixedLengthRadio.IsChecked = true;
                FixedLengthTextBox.Text = _settings.Length.FixedLength.ToString();
            }

            // Quantity
            QuantityTextBox.Text = _settings.Quantity.ToString();
            UniqueOnlyCheck.IsChecked = _settings.UniqueOnly;

            // OCR Challenges
            OcrChallengesEnabledCheck.IsChecked = _settings.OcrChallenges.Enabled;
            ConfusionPercentageTextBox.Text = _settings.OcrChallenges.ConfusionPercentage.ToString();

            // Output
            OutputPathTextBox.Text = _settings.OutputPath;
            if (_settings.AppendMode)
                AppendRadio.IsChecked = true;
            else
                OverwriteRadio.IsChecked = true;

            UpdateUIState();
        }

        private void SaveUIToSettings()
        {
            // Character Sets
            _settings.CharacterSets.IncludeUppercase = IncludeUppercaseCheck.IsChecked == true;
            _settings.CharacterSets.IncludeLowercase = IncludeLowercaseCheck.IsChecked == true;
            _settings.CharacterSets.IncludeNumbers = IncludeNumbersCheck.IsChecked == true;
            _settings.CharacterSets.SpecialCharacters = SpecialCharsTextBox.Text;

            // Generation Mode
            _settings.Mode = RuleBasedRadio.IsChecked == true ? GenerationMode.RuleBased : GenerationMode.PurelyRandom;

            // Start Rules
            _settings.StartRules.AllowUppercase = StartUppercaseCheck.IsChecked == true;
            _settings.StartRules.AllowLowercase = StartLowercaseCheck.IsChecked == true;
            _settings.StartRules.AllowNumbers = StartNumbersCheck.IsChecked == true;
            _settings.StartRules.AllowSpecialChars = StartSpecialCheck.IsChecked == true;

            // End Rules
            _settings.EndRules.AllowUppercase = EndUppercaseCheck.IsChecked == true;
            _settings.EndRules.AllowLowercase = EndLowercaseCheck.IsChecked == true;
            _settings.EndRules.AllowNumbers = EndNumbersCheck.IsChecked == true;
            _settings.EndRules.AllowSpecialChars = EndSpecialCheck.IsChecked == true;

            // Length
            _settings.Length.UseRange = RangeLengthRadio.IsChecked == true;
            if (int.TryParse(FixedLengthTextBox.Text, out int fixedLen))
                _settings.Length.FixedLength = fixedLen;
            if (int.TryParse(MinLengthTextBox.Text, out int minLen))
                _settings.Length.MinLength = minLen;
            if (int.TryParse(MaxLengthTextBox.Text, out int maxLen))
                _settings.Length.MaxLength = maxLen;

            // Quantity
            if (int.TryParse(QuantityTextBox.Text, out int quantity))
                _settings.Quantity = quantity;
            _settings.UniqueOnly = UniqueOnlyCheck.IsChecked == true;

            // OCR Challenges
            _settings.OcrChallenges.Enabled = OcrChallengesEnabledCheck.IsChecked == true;
            if (int.TryParse(ConfusionPercentageTextBox.Text, out int confusionPct))
                _settings.OcrChallenges.ConfusionPercentage = confusionPct;

            // Output
            _settings.OutputPath = OutputPathTextBox.Text;
            _settings.AppendMode = AppendRadio.IsChecked == true;
        }

        private void UpdateUIState()
        {
            // Enable/disable rules based on mode
            RulesGroupBox.IsEnabled = RuleBasedRadio.IsChecked == true;

            // Enable/disable length textboxes
            FixedLengthTextBox.IsEnabled = FixedLengthRadio.IsChecked == true;
            MinLengthTextBox.IsEnabled = RangeLengthRadio.IsChecked == true;
            MaxLengthTextBox.IsEnabled = RangeLengthRadio.IsChecked == true;

            // Enable/disable OCR challenges panel
            OcrChallengesPanel.IsEnabled = OcrChallengesEnabledCheck.IsChecked == true;
        }

        private void GenerationMode_Changed(object sender, RoutedEventArgs e)
        {
            UpdateUIState();
        }

        private void LengthMode_Changed(object sender, RoutedEventArgs e)
        {
            UpdateUIState();
        }

        private void OcrChallenges_Changed(object sender, RoutedEventArgs e)
        {
            UpdateUIState();
        }

        private void LoadButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "XML files (*.xml)|*.xml|All files (*.*)|*.*",
                FilterIndex = 1
            };

            if (dialog.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    var serializer = new XmlSerializer(typeof(StringGenerationSettings));
                    using (var reader = new FileStream(dialog.FileName, FileMode.Open))
                    {
                        _settings = (StringGenerationSettings)serializer.Deserialize(reader);
                    }

                    LoadSettingsToUI();
                    _currentFilePath = dialog.FileName;
                    CurrentFileLabel.Text = Path.GetFileName(dialog.FileName);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error loading settings: {ex.Message}", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
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
            var dialog = new SaveFileDialog
            {
                Filter = "XML files (*.xml)|*.xml|All files (*.*)|*.*",
                FilterIndex = 1,
                DefaultExt = "xml"
            };

            if (dialog.ShowDialog() == DialogResult.OK)
            {
                SaveSettings(dialog.FileName);
            }
        }

        private void SaveSettings(string filePath)
        {
            try
            {
                SaveUIToSettings();

                var serializer = new XmlSerializer(typeof(StringGenerationSettings));
                using (var writer = new FileStream(filePath, FileMode.Create))
                {
                    serializer.Serialize(writer, _settings);
                }

                _currentFilePath = filePath;
                CurrentFileLabel.Text = Path.GetFileName(filePath);
                MessageBox.Show("Settings saved successfully!", "Success",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving settings: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BrowseOutput_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new SaveFileDialog
            {
                Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*",
                FilterIndex = 1,
                DefaultExt = "txt",
                FileName = "strings.txt"
            };

            if (dialog.ShowDialog() == DialogResult.OK)
            {
                OutputPathTextBox.Text = dialog.FileName;
            }
        }

        private void RegeneratePreview_Click(object sender, RoutedEventArgs e)
        {
            SaveUIToSettings();
            InitializeConfusionPairs(); // Rebuild confusion pairs based on current settings

            try
            {
                var previewStrings = GenerateStrings(50); // Generate 50 for preview

                var preview = new StringBuilder();
                foreach (var str in previewStrings.Take(50))
                {
                    preview.AppendLine(str);
                }

                PreviewTextBox.Text = preview.ToString();

                var avgLength = previewStrings.Average(s => s.Length);
                var uniqueCount = previewStrings.Distinct().Count();
                PreviewStatsLabel.Text = $"Preview: {previewStrings.Count} strings, Avg length: {avgLength:F1}, Unique: {uniqueCount}";
            }
            catch (Exception ex)
            {
                PreviewTextBox.Text = $"Error generating preview: {ex.Message}";
                PreviewStatsLabel.Text = "Preview failed";
            }
        }

        private void Generate_Click(object sender, RoutedEventArgs e)
        {
            SaveUIToSettings();
            InitializeConfusionPairs(); // Rebuild confusion pairs based on current settings

            // Validation
            if (!ValidateSettings())
                return;

            try
            {
                StatusLabel.Text = "Generating strings...";
                GenerateButton.IsEnabled = false;

                var strings = GenerateStrings(_settings.Quantity);

                // Write to file
                if (_settings.AppendMode && File.Exists(_settings.OutputPath))
                {
                    File.AppendAllLines(_settings.OutputPath, strings);
                }
                else
                {
                    File.WriteAllLines(_settings.OutputPath, strings);
                }

                var avgLength = strings.Average(s => s.Length);
                var uniqueCount = strings.Distinct().Count();

                StatusLabel.Text = $"Success! Generated {strings.Count} strings";
                MessageBox.Show($"Successfully generated {strings.Count} strings!\n\n" +
                    $"Average length: {avgLength:F1}\n" +
                    $"Unique strings: {uniqueCount}\n" +
                    $"Saved to: {_settings.OutputPath}",
                    "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                StatusLabel.Text = "Generation failed";
                MessageBox.Show($"Error generating strings: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                GenerateButton.IsEnabled = true;
            }
        }

        private bool ValidateSettings()
        {
            // Check if at least one character set is selected
            if (!_settings.CharacterSets.IncludeUppercase &&
                !_settings.CharacterSets.IncludeLowercase &&
                !_settings.CharacterSets.IncludeNumbers &&
                string.IsNullOrEmpty(_settings.CharacterSets.SpecialCharacters))
            {
                MessageBox.Show("Please select at least one character set.", "Validation Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            // Check length settings
            if (_settings.Length.UseRange)
            {
                if (_settings.Length.MinLength < 1 || _settings.Length.MaxLength < 1)
                {
                    MessageBox.Show("Length values must be at least 1.", "Validation Error",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }

                if (_settings.Length.MinLength > _settings.Length.MaxLength)
                {
                    MessageBox.Show("Minimum length cannot be greater than maximum length.", "Validation Error",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }
            }
            else
            {
                if (_settings.Length.FixedLength < 1)
                {
                    MessageBox.Show("Fixed length must be at least 1.", "Validation Error",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }
            }

            // Check quantity
            if (_settings.Quantity < 1)
            {
                MessageBox.Show("Quantity must be at least 1.", "Validation Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            // Check output path
            if (string.IsNullOrWhiteSpace(_settings.OutputPath))
            {
                MessageBox.Show("Please specify an output file path.", "Validation Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            // Check rule-based mode requirements
            if (_settings.Mode == GenerationMode.RuleBased)
            {
                if (!HasValidStartRule())
                {
                    MessageBox.Show("In rule-based mode, at least one 'Can Start With' option must be selected.",
                        "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }

                if (!HasValidEndRule())
                {
                    MessageBox.Show("In rule-based mode, at least one 'Can End With' option must be selected.",
                        "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }
            }

            return true;
        }

        private bool HasValidStartRule()
        {
            return (_settings.StartRules.AllowUppercase && _settings.CharacterSets.IncludeUppercase) ||
                   (_settings.StartRules.AllowLowercase && _settings.CharacterSets.IncludeLowercase) ||
                   (_settings.StartRules.AllowNumbers && _settings.CharacterSets.IncludeNumbers) ||
                   (_settings.StartRules.AllowSpecialChars && !string.IsNullOrEmpty(_settings.CharacterSets.SpecialCharacters));
        }

        private bool HasValidEndRule()
        {
            return (_settings.EndRules.AllowUppercase && _settings.CharacterSets.IncludeUppercase) ||
                   (_settings.EndRules.AllowLowercase && _settings.CharacterSets.IncludeLowercase) ||
                   (_settings.EndRules.AllowNumbers && _settings.CharacterSets.IncludeNumbers) ||
                   (_settings.EndRules.AllowSpecialChars && !string.IsNullOrEmpty(_settings.CharacterSets.SpecialCharacters));
        }

        private List<string> GenerateStrings(int count)
        {
            var strings = new List<string>();
            var uniqueStrings = new HashSet<string>();

            // Build character pools
            var allChars = BuildCharacterPool();
            var startChars = _settings.Mode == GenerationMode.RuleBased ? BuildStartCharacterPool() : allChars;
            var endChars = _settings.Mode == GenerationMode.RuleBased ? BuildEndCharacterPool() : allChars;

            if (allChars.Count == 0)
                throw new InvalidOperationException("No characters available for generation.");

            if (startChars.Count == 0)
                throw new InvalidOperationException("No characters available for start position.");

            if (endChars.Count == 0)
                throw new InvalidOperationException("No characters available for end position.");

            int attempts = 0;
            int maxAttempts = _settings.UniqueOnly ? count * 100 : count; // More attempts if unique required

            while (strings.Count < count && attempts < maxAttempts)
            {
                attempts++;

                var length = _settings.Length.UseRange
                    ? _random.Next(_settings.Length.MinLength, _settings.Length.MaxLength + 1)
                    : _settings.Length.FixedLength;

                var str = GenerateSingleString(length, allChars, startChars, endChars);

                // Apply OCR confusion if enabled
                if (_settings.OcrChallenges.Enabled && _random.Next(100) < _settings.OcrChallenges.ConfusionPercentage)
                {
                    str = ApplyOcrConfusion(str);
                }

                // Check uniqueness if required
                if (_settings.UniqueOnly)
                {
                    if (uniqueStrings.Add(str))
                    {
                        strings.Add(str);
                    }
                }
                else
                {
                    strings.Add(str);
                }
            }

            if (_settings.UniqueOnly && strings.Count < count)
            {
                throw new InvalidOperationException(
                    $"Could only generate {strings.Count} unique strings out of {count} requested. " +
                    "Try increasing length range or character set variety.");
            }

            return strings;
        }

        private string GenerateSingleString(int length, List<char> allChars, List<char> startChars, List<char> endChars)
        {
            if (length == 1)
            {
                // Special case: single character must satisfy both start and end rules
                var validChars = startChars.Intersect(endChars).ToList();
                if (validChars.Count == 0)
                    throw new InvalidOperationException("No characters satisfy both start and end rules for length 1.");

                return validChars[_random.Next(validChars.Count)].ToString();
            }

            var sb = new StringBuilder(length);

            // First character
            sb.Append(startChars[_random.Next(startChars.Count)]);

            // Middle characters
            for (int i = 1; i < length - 1; i++)
            {
                sb.Append(allChars[_random.Next(allChars.Count)]);
            }

            // Last character
            sb.Append(endChars[_random.Next(endChars.Count)]);

            return sb.ToString();
        }

        private string ApplyOcrConfusion(string input)
        {
            var sb = new StringBuilder(input);

            // Randomly replace some characters with their confusion pairs
            for (int i = 0; i < sb.Length; i++)
            {
                if (_confusionPairs.ContainsKey(sb[i]) && _random.Next(100) < 30) // 30% chance per character
                {
                    var confusionOptions = _confusionPairs[sb[i]];
                    sb[i] = confusionOptions[_random.Next(confusionOptions.Length)];
                }
            }

            return sb.ToString();
        }

        private List<char> BuildCharacterPool()
        {
            var pool = new List<char>();

            if (_settings.CharacterSets.IncludeUppercase)
            {
                for (char c = 'A'; c <= 'Z'; c++)
                    pool.Add(c);
            }

            if (_settings.CharacterSets.IncludeLowercase)
            {
                for (char c = 'a'; c <= 'z'; c++)
                    pool.Add(c);
            }

            if (_settings.CharacterSets.IncludeNumbers)
            {
                for (char c = '0'; c <= '9'; c++)
                    pool.Add(c);
            }

            if (!string.IsNullOrEmpty(_settings.CharacterSets.SpecialCharacters))
            {
                pool.AddRange(_settings.CharacterSets.SpecialCharacters.ToCharArray());
            }

            return pool;
        }

        private List<char> BuildStartCharacterPool()
        {
            var pool = new List<char>();

            if (_settings.StartRules.AllowUppercase && _settings.CharacterSets.IncludeUppercase)
            {
                for (char c = 'A'; c <= 'Z'; c++)
                    pool.Add(c);
            }

            if (_settings.StartRules.AllowLowercase && _settings.CharacterSets.IncludeLowercase)
            {
                for (char c = 'a'; c <= 'z'; c++)
                    pool.Add(c);
            }

            if (_settings.StartRules.AllowNumbers && _settings.CharacterSets.IncludeNumbers)
            {
                for (char c = '0'; c <= '9'; c++)
                    pool.Add(c);
            }

            if (_settings.StartRules.AllowSpecialChars && !string.IsNullOrEmpty(_settings.CharacterSets.SpecialCharacters))
            {
                pool.AddRange(_settings.CharacterSets.SpecialCharacters.ToCharArray());
            }

            return pool;
        }

        private List<char> BuildEndCharacterPool()
        {
            var pool = new List<char>();

            if (_settings.EndRules.AllowUppercase && _settings.CharacterSets.IncludeUppercase)
            {
                for (char c = 'A'; c <= 'Z'; c++)
                    pool.Add(c);
            }

            if (_settings.EndRules.AllowLowercase && _settings.CharacterSets.IncludeLowercase)
            {
                for (char c = 'a'; c <= 'z'; c++)
                    pool.Add(c);
            }

            if (_settings.EndRules.AllowNumbers && _settings.CharacterSets.IncludeNumbers)
            {
                for (char c = '0'; c <= '9'; c++)
                    pool.Add(c);
            }

            if (_settings.EndRules.AllowSpecialChars && !string.IsNullOrEmpty(_settings.CharacterSets.SpecialCharacters))
            {
                pool.AddRange(_settings.CharacterSets.SpecialCharacters.ToCharArray());
            }

            return pool;
        }
    }
}