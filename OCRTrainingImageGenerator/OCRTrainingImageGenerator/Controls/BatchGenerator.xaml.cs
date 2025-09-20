using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Forms;
using System.Xml.Serialization;
using OCRTrainingImageGenerator.Models;
using OCRTrainingImageGenerator.Services;
using Button = System.Windows.Controls.Button;
using MessageBox = System.Windows.MessageBox;
using UserControl = System.Windows.Controls.UserControl;

namespace OCRTrainingImageGenerator.Controls
{
    public partial class BatchGenerator : UserControl
    {
        public ObservableCollection<BatchGenerationJob> Jobs { get; private set; }
        private ImageGenerationService _imageService;
        private CancellationTokenSource _cancellationTokenSource;
        private bool _isGenerating = false;
        private const string ConfigFileName = "last_batch_config.xml";

        public static readonly IValueConverter StatusToProgressVisibilityConverter = new StatusToProgressVisibilityValueConverter();

        public BatchGenerator()
        {
            InitializeComponent();
            Jobs = new ObservableCollection<BatchGenerationJob>();
            JobsItemsControl.ItemsSource = Jobs;
            _imageService = new ImageGenerationService();

            // Set default thread count to CPU cores
            MaxThreadsTextBox.Text = Environment.ProcessorCount.ToString();

            // Load last configuration
            LoadLastConfiguration();
        }

        private void LoadLastConfiguration()
        {
            try
            {
                if (!File.Exists(ConfigFileName))
                    return;

                var serializer = new XmlSerializer(typeof(BatchConfiguration));
                BatchConfiguration config;

                using (var reader = new FileStream(ConfigFileName, FileMode.Open))
                {
                    config = (BatchConfiguration)serializer.Deserialize(reader);
                }

                // Restore UI settings
                OutputFolderTextBox.Text = config.OutputFolder;
                MaxThreadsTextBox.Text = config.MaxThreads.ToString();

                // Restore jobs
                foreach (var jobInfo in config.Jobs)
                {
                    // Only add if the settings file still exists
                    if (File.Exists(jobInfo.SettingsFilePath))
                    {
                        var job = new BatchGenerationJob
                        {
                            SettingsFilePath = jobInfo.SettingsFilePath,
                            ImageCount = jobInfo.ImageCount,
                            SubfolderName = jobInfo.SubfolderName,
                            Status = JobStatus.Pending, // Reset status for new run
                            StatusMessage = jobInfo.LastStatus == JobStatus.Completed
                                ? $"Previously completed ({config.LastRunDate:yyyy-MM-dd HH:mm})"
                                : "Ready"
                        };
                        Jobs.Add(job);
                    }
                }

                if (Jobs.Any())
                {
                    OverallStatusTextBlock.Text = $"Loaded last configuration from {config.LastRunDate:yyyy-MM-dd HH:mm}";
                    OverallProgressTextBlock.Text = $"{Jobs.Count} job(s) restored";
                }
            }
            catch (Exception ex)
            {
                // Silently fail - not critical if we can't load the last config
                System.Diagnostics.Debug.WriteLine($"Failed to load last configuration: {ex.Message}");
            }
        }

        private void SaveCurrentConfiguration()
        {
            try
            {
                var config = new BatchConfiguration
                {
                    OutputFolder = OutputFolderTextBox.Text,
                    MaxThreads = int.TryParse(MaxThreadsTextBox.Text, out int threads) ? threads : Environment.ProcessorCount - 2,
                    LastRunDate = DateTime.Now,
                    Jobs = Jobs.Select(j => new BatchJobInfo
                    {
                        SettingsFilePath = j.SettingsFilePath,
                        ImageCount = j.ImageCount,
                        SubfolderName = j.SubfolderName,
                        LastStatus = j.Status,
                        LastStatusMessage = j.StatusMessage
                    }).ToList()
                };

                var serializer = new XmlSerializer(typeof(BatchConfiguration));
                using (var writer = new FileStream(ConfigFileName, FileMode.Create))
                {
                    serializer.Serialize(writer, config);
                }
            }
            catch (Exception ex)
            {
                // Silently fail - not critical if we can't save the config
                System.Diagnostics.Debug.WriteLine($"Failed to save configuration: {ex.Message}");
            }
        }

        private void BrowseOutputFolder_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new FolderBrowserDialog();

            if (dialog.ShowDialog() == DialogResult.OK)
            {
                OutputFolderTextBox.Text = dialog.SelectedPath;
            }
        }

        private void AddJob_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "XML files (*.xml)|*.xml|All files (*.*)|*.*",
                FilterIndex = 1,
                Multiselect = true
            };

            if (dialog.ShowDialog() == true)
            {
                foreach (var fileName in dialog.FileNames)
                {
                    // Check if this settings file is already added
                    if (Jobs.Any(j => j.SettingsFilePath.Equals(fileName, StringComparison.OrdinalIgnoreCase)))
                    {
                        MessageBox.Show($"Settings file '{Path.GetFileName(fileName)}' is already added.",
                            "Duplicate File", MessageBoxButton.OK, MessageBoxImage.Information);
                        continue;
                    }

                    var job = new BatchGenerationJob
                    {
                        SettingsFilePath = fileName,
                        ImageCount = 1000, // Default count
                        Status = JobStatus.Pending,
                        StatusMessage = "Ready"
                    };

                    Jobs.Add(job);
                }
            }
        }

        private void RemoveJob_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is BatchGenerationJob job)
            {
                if (job.Status == JobStatus.Running)
                {
                    MessageBox.Show("Cannot remove a job that is currently running.",
                        "Cannot Remove", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                Jobs.Remove(job);
            }
        }

        private void ClearJobs_Click(object sender, RoutedEventArgs e)
        {
            if (_isGenerating)
            {
                MessageBox.Show("Cannot clear jobs while generation is running.",
                    "Cannot Clear", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (Jobs.Count > 0 && MessageBox.Show("Remove all jobs?", "Confirm",
                MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                Jobs.Clear();
                OverallStatusTextBlock.Text = "Ready to generate";
                OverallProgressTextBlock.Text = "";
            }
        }

        private void SaveConfig_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "XML files (*.xml)|*.xml|All files (*.*)|*.*",
                FilterIndex = 1,
                DefaultExt = "xml",
                FileName = "batch_config.xml"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var config = new BatchConfiguration
                    {
                        OutputFolder = OutputFolderTextBox.Text,
                        MaxThreads = int.TryParse(MaxThreadsTextBox.Text, out int threads) ? threads : Environment.ProcessorCount - 2,
                        LastRunDate = DateTime.Now,
                        Jobs = Jobs.Select(j => new BatchJobInfo
                        {
                            SettingsFilePath = j.SettingsFilePath,
                            ImageCount = j.ImageCount,
                            SubfolderName = j.SubfolderName,
                            LastStatus = j.Status,
                            LastStatusMessage = j.StatusMessage
                        }).ToList()
                    };

                    var serializer = new XmlSerializer(typeof(BatchConfiguration));
                    using (var writer = new FileStream(dialog.FileName, FileMode.Create))
                    {
                        serializer.Serialize(writer, config);
                    }

                    MessageBox.Show("Configuration saved successfully!", "Success",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error saving configuration: {ex.Message}", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void LoadConfig_Click(object sender, RoutedEventArgs e)
        {
            if (_isGenerating)
            {
                MessageBox.Show("Cannot load configuration while generation is running.",
                    "Cannot Load", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "XML files (*.xml)|*.xml|All files (*.*)|*.*",
                FilterIndex = 1
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    LoadConfigurationFromFile(dialog.FileName);
                    MessageBox.Show("Configuration loaded successfully!", "Success",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error loading configuration: {ex.Message}", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void LoadConfigurationFromFile(string filePath)
        {
            var serializer = new XmlSerializer(typeof(BatchConfiguration));
            BatchConfiguration config;

            using (var reader = new FileStream(filePath, FileMode.Open))
            {
                config = (BatchConfiguration)serializer.Deserialize(reader);
            }

            // Clear existing jobs
            Jobs.Clear();

            // Restore UI settings
            OutputFolderTextBox.Text = config.OutputFolder;
            MaxThreadsTextBox.Text = config.MaxThreads.ToString();

            // Restore jobs
            var missingFiles = new System.Collections.Generic.List<string>();
            foreach (var jobInfo in config.Jobs)
            {
                if (File.Exists(jobInfo.SettingsFilePath))
                {
                    var job = new BatchGenerationJob
                    {
                        SettingsFilePath = jobInfo.SettingsFilePath,
                        ImageCount = jobInfo.ImageCount,
                        SubfolderName = jobInfo.SubfolderName,
                        Status = JobStatus.Pending, // Reset status for new run
                        StatusMessage = jobInfo.LastStatus == JobStatus.Completed
                            ? $"Previously completed ({config.LastRunDate:yyyy-MM-dd HH:mm})"
                            : "Ready"
                    };
                    Jobs.Add(job);
                }
                else
                {
                    missingFiles.Add(Path.GetFileName(jobInfo.SettingsFilePath));
                }
            }

            if (missingFiles.Any())
            {
                var missingList = string.Join(", ", missingFiles);
                MessageBox.Show($"The following settings files could not be found and were skipped: {missingList}",
                    "Missing Files", MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            OverallStatusTextBlock.Text = $"Loaded configuration from {config.LastRunDate:yyyy-MM-dd HH:mm}";
            OverallProgressTextBlock.Text = $"{Jobs.Count} job(s) loaded";
        }

        private async void StartGeneration_Click(object sender, RoutedEventArgs e)
        {
            if (_isGenerating)
            {
                // Cancel generation
                _cancellationTokenSource?.Cancel();
                return;
            }

            // Validate inputs
            if (string.IsNullOrWhiteSpace(OutputFolderTextBox.Text))
            {
                MessageBox.Show("Please select an output folder.", "Validation Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!Jobs.Any())
            {
                MessageBox.Show("Please add at least one settings file.", "Validation Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!int.TryParse(MaxThreadsTextBox.Text, out int maxThreads) || maxThreads < 1)
            {
                MessageBox.Show("Please enter a valid number of threads (1 or greater).", "Validation Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                MaxThreadsTextBox.Focus();
                return;
            }

            // Check if any jobs have image count <= 0
            var invalidJobs = Jobs.Where(j => j.ImageCount <= 0).ToList();
            if (invalidJobs.Any())
            {
                MessageBox.Show("All jobs must have an image count greater than 0.", "Validation Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Check if output folder or subfolders already contain files
            if (!await CheckAndConfirmOverwrite(OutputFolderTextBox.Text))
            {
                return; // User cancelled
            }

            try
            {
                _isGenerating = true;
                _cancellationTokenSource = new CancellationTokenSource();
                GenerateButton.Content = "Cancel Generation";

                await RunBatchGeneration(OutputFolderTextBox.Text, maxThreads, _cancellationTokenSource.Token);

                // Save configuration after successful completion
                SaveCurrentConfiguration();
            }
            catch (OperationCanceledException)
            {
                OverallStatusTextBlock.Text = "Generation cancelled";
                foreach (var job in Jobs.Where(j => j.Status == JobStatus.Running))
                {
                    job.Status = JobStatus.Cancelled;
                    job.StatusMessage = "Cancelled";
                }
            }
            catch (Exception ex)
            {
                OverallStatusTextBlock.Text = $"Generation failed: {ex.Message}";
                MessageBox.Show($"Generation failed: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _isGenerating = false;
                GenerateButton.Content = "Start Generation";
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;
            }
        }

        private async Task<bool> CheckAndConfirmOverwrite(string outputPath)
        {
            var foldersWithFiles = new System.Collections.Generic.List<string>();

            // Check main output folder
            if (Directory.Exists(outputPath))
            {
                var files = Directory.GetFiles(outputPath, "*.*", SearchOption.TopDirectoryOnly);
                if (files.Any())
                {
                    foldersWithFiles.Add("main output folder");
                }
            }

            // Check each job's subfolder
            foreach (var job in Jobs)
            {
                var jobOutputPath = Path.Combine(outputPath, job.SubfolderName);
                if (Directory.Exists(jobOutputPath))
                {
                    var files = Directory.GetFiles(jobOutputPath, "*.*", SearchOption.AllDirectories);
                    if (files.Any())
                    {
                        foldersWithFiles.Add($"'{job.SubfolderName}' subfolder");
                    }
                }
            }

            if (!foldersWithFiles.Any())
            {
                return true; // No files found, proceed
            }

            // Show confirmation dialog
            var folderList = string.Join(", ", foldersWithFiles);
            var message = foldersWithFiles.Count == 1
                ? $"The {folderList} already contains files. Do you want to continue and potentially overwrite existing files?"
                : $"The following folders already contain files: {folderList}.\n\nDo you want to continue and potentially overwrite existing files?";

            var result = MessageBox.Show(
                message,
                "Existing Files Detected",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question,
                MessageBoxResult.No // Default to No for safety
            );

            return result == MessageBoxResult.Yes;
        }

        private async Task RunBatchGeneration(string outputPath, int maxThreads, CancellationToken cancellationToken)
        {
            // Create output directory if it doesn't exist
            if (!Directory.Exists(outputPath))
            {
                Directory.CreateDirectory(outputPath);
            }

            // Reset all job statuses
            foreach (var job in Jobs)
            {
                job.Status = JobStatus.Pending;
                job.Progress = 0;
                job.StatusMessage = "Waiting...";
            }

            var totalJobs = Jobs.Count;
            var completedJobs = 0;
            var startTime = DateTime.Now;

            OverallStatusTextBlock.Text = $"Starting generation of {totalJobs} jobs...";

            // Process jobs sequentially to avoid overwhelming the system
            foreach (var job in Jobs)
            {
                if (cancellationToken.IsCancellationRequested)
                    return;

                try
                {
                    job.Status = JobStatus.Running;
                    job.StatusMessage = "Loading settings...";
                    job.Progress = 0;

                    // Load settings
                    var settings = LoadSettings(job.SettingsFilePath);
                    if (settings == null)
                    {
                        job.Status = JobStatus.Failed;
                        job.StatusMessage = "Failed to load settings";
                        continue;
                    }

                    // Create job-specific output folder
                    var jobOutputPath = Path.Combine(outputPath, job.SubfolderName);
                    if (!Directory.Exists(jobOutputPath))
                    {
                        Directory.CreateDirectory(jobOutputPath);
                    }

                    job.StatusMessage = "Generating images...";

                    // Generate images for this job
                    await Task.Run(() =>
                    {
                        _imageService.GenerateImages(
                            settings: settings,
                            outputPath: jobOutputPath,
                            imageCount: job.ImageCount,
                            maxThreads: maxThreads,
                            progressCallback: status =>
                            {
                                if (cancellationToken.IsCancellationRequested)
                                    return;

                                Dispatcher.Invoke(() =>
                                {
                                    job.StatusMessage = status;

                                    // Try to extract progress from status message
                                    // Status format: "Generated X/Y images (rate/sec, ETA: time, Failed: count)"
                                    if (status.Contains("/"))
                                    {
                                        try
                                        {
                                            var parts = status.Split(' ');
                                            if (parts.Length > 1 && parts[1].Contains("/"))
                                            {
                                                var progressParts = parts[1].Split('/');
                                                if (int.TryParse(progressParts[0], out int current) &&
                                                    int.TryParse(progressParts[1], out int total) && total > 0)
                                                {
                                                    job.Progress = (int)((current / (double)total) * 100);
                                                }
                                            }
                                        }
                                        catch
                                        {
                                            // Ignore parsing errors
                                        }
                                    }
                                });
                            }
                        );
                    }, cancellationToken);

                    if (cancellationToken.IsCancellationRequested)
                        return;

                    job.Status = JobStatus.Completed;
                    job.Progress = 100;
                    job.StatusMessage = $"Completed - {job.ImageCount} images generated";
                    completedJobs++;
                }
                catch (Exception ex)
                {
                    job.Status = JobStatus.Failed;
                    job.StatusMessage = $"Failed: {ex.Message}";
                }

                // Update overall progress
                var elapsed = DateTime.Now - startTime;
                var remainingJobs = totalJobs - completedJobs;
                var avgTimePerJob = completedJobs > 0 ? elapsed.TotalSeconds / completedJobs : 0;
                var eta = remainingJobs > 0 ? TimeSpan.FromSeconds(remainingJobs * avgTimePerJob) : TimeSpan.Zero;

                OverallStatusTextBlock.Text = $"Jobs: {completedJobs}/{totalJobs} completed";
                OverallProgressTextBlock.Text = remainingJobs > 0
                    ? $"ETA: {eta:hh\\:mm\\:ss}"
                    : $"All jobs completed in {elapsed:hh\\:mm\\:ss}";
            }

            var finalElapsed = DateTime.Now - startTime;
            var successfulJobs = Jobs.Count(j => j.Status == JobStatus.Completed);
            var failedJobs = Jobs.Count(j => j.Status == JobStatus.Failed);

            OverallStatusTextBlock.Text = $"Generation complete: {successfulJobs} successful, {failedJobs} failed";
            OverallProgressTextBlock.Text = $"Total time: {finalElapsed:hh\\:mm\\:ss}";

            if (failedJobs == 0)
            {
                MessageBox.Show($"All {successfulJobs} jobs completed successfully!", "Success",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show($"Generation completed with {failedJobs} failed jobs. Check individual job status for details.",
                    "Completed with Errors", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private OCRGenerationSettings LoadSettings(string filePath)
        {
            try
            {
                var serializer = new XmlSerializer(typeof(OCRGenerationSettings));
                using (var reader = new FileStream(filePath, FileMode.Open))
                {
                    return (OCRGenerationSettings)serializer.Deserialize(reader);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading settings from '{Path.GetFileName(filePath)}': {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return null;
            }
        }
    }

    public class StatusToProgressVisibilityValueConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is JobStatus status)
            {
                return status == JobStatus.Running ? Visibility.Visible : Visibility.Collapsed;
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}