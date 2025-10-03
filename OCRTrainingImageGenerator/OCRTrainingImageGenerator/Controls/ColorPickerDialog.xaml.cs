using System.Windows;
using System.Windows.Media;
using ColorPicker.Models;
using Color = System.Windows.Media.Color;

namespace OCRTrainingImageGenerator.Controls
{
    public partial class ColorPickerDialog : Window
    {
        public Color SelectedColor { get; private set; }

        public ColorPickerDialog(Color initialColor)
        {
            InitializeComponent();

            SelectedColor = initialColor;

            colorPicker.SelectedColor = initialColor;
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            SelectedColor = colorPicker.SelectedColor;

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