using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Media;
using System.Windows.Shapes;
using OCRTrainingImageGenerator.Models;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using MessageBox = System.Windows.MessageBox;
using Orientation = System.Windows.Controls.Orientation;
using Rectangle = System.Windows.Shapes.Rectangle;

namespace OCRTrainingImageGenerator.Controls
{
    public partial class BackgroundEditDialog : Window
    {
        public BackgroundSetting BackgroundSetting { get; private set; }

        private System.Windows.Controls.TextBox NameTextBox;
        private System.Windows.Controls.ComboBox TypeComboBox;
        private StackPanel SolidColorPanel;
        private StackPanel GradientPanel;
        private Rectangle SolidColorRect;
        private Rectangle GradientStartRect;
        private Rectangle GradientEndRect;
        private System.Windows.Controls.TextBox AngleTextBox;

        public BackgroundEditDialog(BackgroundSetting background)
        {
            BackgroundSetting = background;
            InitializeComponent();
            LoadBackgroundToUI();
        }

        private void InitializeComponent()
        {
            Title = "Edit Background";
            Width = 450;
            Height = 400;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ResizeMode = ResizeMode.NoResize;

            var mainGrid = new Grid();
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Title
            var titleLabel = new System.Windows.Controls.Label
            {
                Content = "Background Settings",
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                Margin = new Thickness(10)
            };
            Grid.SetRow(titleLabel, 0);
            mainGrid.Children.Add(titleLabel);

            // Settings Panel
            var settingsPanel = new StackPanel { Margin = new Thickness(20) };
            Grid.SetRow(settingsPanel, 1);

            // Name
            var namePanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 5, 0, 5) };
            namePanel.Children.Add(new System.Windows.Controls.Label { Content = "Name:", Width = 80 });
            NameTextBox = new System.Windows.Controls.TextBox { Width = 250, Height = 25 };
            namePanel.Children.Add(NameTextBox);
            settingsPanel.Children.Add(namePanel);

            // Type
            var typePanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 5, 0, 5) };
            typePanel.Children.Add(new System.Windows.Controls.Label { Content = "Type:", Width = 80 });
            TypeComboBox = new System.Windows.Controls.ComboBox { Width = 150, Height = 25 };
            TypeComboBox.ItemsSource = Enum.GetValues(typeof(BackgroundType));
            TypeComboBox.SelectionChanged += TypeComboBox_SelectionChanged;
            typePanel.Children.Add(TypeComboBox);
            settingsPanel.Children.Add(typePanel);

            // Solid Color Panel
            SolidColorPanel = new StackPanel { Margin = new Thickness(0, 15, 0, 0) };
            var solidColorHeader = new System.Windows.Controls.Label { Content = "Solid Color", FontWeight = FontWeights.Bold };
            SolidColorPanel.Children.Add(solidColorHeader);

            var solidColorControls = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 5, 0, 0) };
            solidColorControls.Children.Add(new System.Windows.Controls.Label { Content = "Color:", Width = 80 });
            SolidColorRect = new Rectangle { Width = 40, Height = 25, Stroke = Brushes.Black, StrokeThickness = 1 };
            solidColorControls.Children.Add(SolidColorRect);
            var changeSolidButton = new System.Windows.Controls.Button { Content = "Change", Width = 80, Height = 25, Margin = new Thickness(10, 0, 0, 0) };
            changeSolidButton.Click += ChangeSolidColor_Click;
            solidColorControls.Children.Add(changeSolidButton);
            SolidColorPanel.Children.Add(solidColorControls);
            settingsPanel.Children.Add(SolidColorPanel);

            // Gradient Panel
            GradientPanel = new StackPanel { Margin = new Thickness(0, 15, 0, 0) };
            var gradientHeader = new System.Windows.Controls.Label { Content = "Gradient", FontWeight = FontWeights.Bold };
            GradientPanel.Children.Add(gradientHeader);

            // Start Color
            var startColorControls = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 5, 0, 0) };
            startColorControls.Children.Add(new System.Windows.Controls.Label { Content = "Start Color:", Width = 80 });
            GradientStartRect = new Rectangle { Width = 40, Height = 25, Stroke = Brushes.Black, StrokeThickness = 1 };
            startColorControls.Children.Add(GradientStartRect);
            var changeStartButton = new System.Windows.Controls.Button { Content = "Change", Width = 80, Height = 25, Margin = new Thickness(10, 0, 0, 0) };
            changeStartButton.Click += ChangeGradientStart_Click;
            startColorControls.Children.Add(changeStartButton);
            GradientPanel.Children.Add(startColorControls);

            // End Color
            var endColorControls = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 5, 0, 0) };
            endColorControls.Children.Add(new System.Windows.Controls.Label { Content = "End Color:", Width = 80 });
            GradientEndRect = new Rectangle { Width = 40, Height = 25, Stroke = Brushes.Black, StrokeThickness = 1 };
            endColorControls.Children.Add(GradientEndRect);
            var changeEndButton = new System.Windows.Controls.Button { Content = "Change", Width = 80, Height = 25, Margin = new Thickness(10, 0, 0, 0) };
            changeEndButton.Click += ChangeGradientEnd_Click;
            endColorControls.Children.Add(changeEndButton);
            GradientPanel.Children.Add(endColorControls);

            // Angle
            var angleControls = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 10, 0, 0) };
            angleControls.Children.Add(new System.Windows.Controls.Label { Content = "Angle:", Width = 80 });
            AngleTextBox = new System.Windows.Controls.TextBox { Width = 100, Height = 25 };
            angleControls.Children.Add(AngleTextBox);
            angleControls.Children.Add(new System.Windows.Controls.Label { Content = " degrees", Margin = new Thickness(5, 0, 0, 0) });
            GradientPanel.Children.Add(angleControls);

            settingsPanel.Children.Add(GradientPanel);
            mainGrid.Children.Add(settingsPanel);

            // Buttons
            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
                Margin = new Thickness(20)
            };
            Grid.SetRow(buttonPanel, 2);

            var okButton = new System.Windows.Controls.Button
            {
                Content = "OK",
                Width = 75,
                Height = 30,
                Margin = new Thickness(5),
                IsDefault = true
            };
            okButton.Click += OkButton_Click;
            buttonPanel.Children.Add(okButton);

            var cancelButton = new System.Windows.Controls.Button
            {
                Content = "Cancel",
                Width = 75,
                Height = 30,
                Margin = new Thickness(5),
                IsCancel = true
            };
            cancelButton.Click += CancelButton_Click;
            buttonPanel.Children.Add(cancelButton);

            mainGrid.Children.Add(buttonPanel);
            Content = mainGrid;
        }

        private void LoadBackgroundToUI()
        {
            NameTextBox.Text = BackgroundSetting.Name ?? "Background";
            TypeComboBox.SelectedItem = BackgroundSetting.Type;
            AngleTextBox.Text = BackgroundSetting.GradientAngle.ToString(CultureInfo.InvariantCulture);

            UpdateColorRectangle(SolidColorRect, BackgroundSetting.SolidColor);
            UpdateColorRectangle(GradientStartRect, BackgroundSetting.GradientStart);
            UpdateColorRectangle(GradientEndRect, BackgroundSetting.GradientEnd);

            UpdatePanelVisibility();
        }

        private void SaveUIToBackground()
        {
            BackgroundSetting.Name = NameTextBox.Text;
            BackgroundSetting.Type = (BackgroundType)TypeComboBox.SelectedItem;

            if (double.TryParse(AngleTextBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double angle))
            {
                BackgroundSetting.GradientAngle = angle;
            }
        }

        private void TypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdatePanelVisibility();
        }

        private void UpdatePanelVisibility()
        {
            if (TypeComboBox.SelectedItem == null) return;

            var type = (BackgroundType)TypeComboBox.SelectedItem;
            SolidColorPanel.Visibility = type == BackgroundType.SolidColor ? Visibility.Visible : Visibility.Collapsed;
            GradientPanel.Visibility = type == BackgroundType.LinearGradient ? Visibility.Visible : Visibility.Collapsed;
        }

        private void ChangeSolidColor_Click(object sender, RoutedEventArgs e)
        {
            var color = ChooseColor(BackgroundSetting.SolidColor.Color);
            if (color.HasValue)
            {
                BackgroundSetting.SolidColor.Color = color.Value;
                UpdateColorRectangle(SolidColorRect, BackgroundSetting.SolidColor);
            }
        }

        private void ChangeGradientStart_Click(object sender, RoutedEventArgs e)
        {
            var color = ChooseColor(BackgroundSetting.GradientStart.Color);
            if (color.HasValue)
            {
                BackgroundSetting.GradientStart.Color = color.Value;
                UpdateColorRectangle(GradientStartRect, BackgroundSetting.GradientStart);
            }
        }

        private void ChangeGradientEnd_Click(object sender, RoutedEventArgs e)
        {
            var color = ChooseColor(BackgroundSetting.GradientEnd.Color);
            if (color.HasValue)
            {
                BackgroundSetting.GradientEnd.Color = color.Value;
                UpdateColorRectangle(GradientEndRect, BackgroundSetting.GradientEnd);
            }
        }

        private Color? ChooseColor(Color initialColor)
        {
            var dialog = new ColorDialog
            {
                Color = System.Drawing.Color.FromArgb(initialColor.A, initialColor.R, initialColor.G, initialColor.B),
                FullOpen = true
            };

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                return Color.FromArgb(dialog.Color.A, dialog.Color.R, dialog.Color.G, dialog.Color.B);
            }
            return null;
        }

        private void UpdateColorRectangle(Rectangle rectangle, ColorSetting colorSetting)
        {
            rectangle.Fill = new SolidColorBrush(colorSetting.Color);
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            // Validate name
            if (string.IsNullOrWhiteSpace(NameTextBox.Text))
            {
                MessageBox.Show("Please enter a name for the background.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                NameTextBox.Focus();
                return;
            }

            // Validate angle for gradients
            if (TypeComboBox.SelectedItem != null && (BackgroundType)TypeComboBox.SelectedItem == BackgroundType.LinearGradient)
            {
                if (!double.TryParse(AngleTextBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double angle))
                {
                    MessageBox.Show("Please enter a valid angle (number).", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    AngleTextBox.Focus();
                    return;
                }
            }

            SaveUIToBackground();
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}