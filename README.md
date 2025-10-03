# OCR Training Image Generator

A WPF application for generating synthetic OCR training datasets with customizable text effects, distortions, and noise patterns. Built with C# and OpenCV, similar to Python's `trdg` but with a visual editor and batch processing.

## Features

- **Visual Settings Editor**: Real-time preview, save/load configurations, font selection with previews
- **Text Customization**: Custom fonts (TTF/OTF), adjustable spacing, alignment, drop shadows, multiple colors
- **Image Effects**: Blur, perspective warp, skew, rotation, noise, JPG compression artifacts
- **Batch Generator**: Multi-threaded generation, queue multiple settings files, progress tracking
- **String Generator**: Create training text with OCR confusion sequences (I1l, O0o, 5Ss, etc.)
- **Range/Fixed Values**: Most parameters support fixed values or random ranges

## Installation

### Prerequisites
- .NET 8.0 SDK or later
- Windows OS (WPF application)

### Building from Source
```bash
git clone https://github.com/yourusername/OcrDatasetGenerator.git
cd OcrDatasetGenerator/OCRTrainingImageGenerator
dotnet build
dotnet run
```

Dependencies managed via NuGet: OpenCvSharp4, Denxorz.ZoomControl, Newtonsoft.Json

## Quick Start

1. **Settings Editor**: Load fonts and strings → configure appearance → save as XML
2. **Batch Generator**: Add settings files → set output folder → start generation
3. **String Generator** (optional): Generate custom training strings with OCR challenges

### Output Structure

```
output_folder/
├── 0.jpg, 1.jpg, 2.jpg, ...
└── labels.txt (format: "filename.jpg text")
```

## Configuration

Settings are saved as XML files with parameters for:
- Text sources (strings file, font folder)
- Appearance (size, spacing, alignment, colors, backgrounds)
- Effects (blur, warp, skew, rotation, noise, compression)
- All numeric values support fixed or random range modes

Example XML:
```xml
<OCRGenerationSettings>
  <StringsFilePath>C:\data\strings.txt</StringsFilePath>
  <FontFolderPath>C:\fonts\</FontFolderPath>
  <FontSize UseRange="true">
    <Min>12</Min>
    <Max>48</Max>
  </FontSize>
</OCRGenerationSettings>
```

## Troubleshooting

- **Preview not working**: Check fonts folder and strings file paths
- **Slow generation**: Reduce thread count or image dimensions
- **Memory issues**: Lower thread count or process smaller batches

## License

Apache License 2.0 - see LICENSE file

## Acknowledgments

Inspired by [belval/trdg](https://github.com/Belval/TextRecognitionDataGenerator), built with OpenCvSharp4 and WPF
