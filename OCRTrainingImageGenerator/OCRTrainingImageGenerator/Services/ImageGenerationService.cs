using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using OCRTrainingImageGenerator.Models;

namespace OCRTrainingImageGenerator.Services
{
    public class ImageGenerationService
    {
        private readonly Random _random = new Random();
        private readonly object _randomLock = new object();
        private readonly Dictionary<string, PrivateFontCollection> _fontCollections = new Dictionary<string, PrivateFontCollection>();
        private readonly object _fontCollectionLock = new object();

        /// <summary>
        /// Generates a single OCR training image
        /// </summary>
        public Mat GenerateImage(OCRGenerationSettings settings, string text, string fontPath)
        {
            Random localRandom;
            lock (_randomLock)
            {
                localRandom = new Random(_random.Next());
            }

            var fontSize = (float)settings.FontSize.GetRandomValue(localRandom);
            var characterSpacing = (float)settings.CharacterSpacing.GetRandomValue(localRandom);
            var marginLeft = (int)settings.Margins.Left.GetRandomValue(localRandom);
            var marginRight = (int)settings.Margins.Right.GetRandomValue(localRandom);
            var marginTop = (int)settings.Margins.Top.GetRandomValue(localRandom);
            var marginBottom = (int)settings.Margins.Bottom.GetRandomValue(localRandom);

            System.Drawing.Size textSize;
            Font font = null;

            try
            {
                font = LoadFont(fontPath, fontSize);
                textSize = MeasureTextWithSpacing(text, font, characterSpacing);
            }
            catch
            {
                font = new Font(FontFamily.GenericSansSerif, fontSize, FontStyle.Regular);
                textSize = MeasureTextWithSpacing(text, font, characterSpacing);
            }

            var totalWidth = textSize.Width + marginLeft + marginRight;
            var totalHeight = Math.Max((int)settings.InitialHeight.GetRandomValue(localRandom),
                                     textSize.Height + marginTop + marginBottom);

            using (var bitmap = new Bitmap(totalWidth, totalHeight))
            using (var graphics = Graphics.FromImage(bitmap))
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.TextRenderingHint = TextRenderingHint.AntiAlias;
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;

                DrawBackground(graphics, bitmap.Size, settings, localRandom);

                DrawTextWithSpacing(graphics, text, font, characterSpacing, settings, localRandom,
                    new Rectangle(marginLeft, marginTop, textSize.Width, totalHeight - marginTop - marginBottom),
                    textSize);

                var mat = bitmap.ToMat();
                mat = ApplyEffects(mat, settings, localRandom);

                return mat;
            }
        }

        /// <summary>
        /// Generates a single OCR training image and applies JPG compression for preview
        /// </summary>
        public Mat GenerateImageForPreview(OCRGenerationSettings settings, string text, string fontPath)
        {
            // Generate the base image
            using (var baseMat = GenerateImage(settings, text, fontPath))
            {
                // Apply JPG compression if enabled
                if (settings.EnableJpgArtifacts)
                {
                    Random localRandom;
                    lock (_randomLock)
                    {
                        localRandom = new Random(_random.Next());
                    }

                    var quality = (int)settings.JpgQuality.GetRandomValue(localRandom);

                    // Convert to JPG and back to simulate compression artifacts
                    var compressionParams = new int[]
                    {
                        (int)ImwriteFlags.JpegQuality, quality
                    };

                    // Encode to JPG bytes
                    Cv2.ImEncode(".jpg", baseMat, out byte[] jpgData, compressionParams);

                    // Decode back to Mat
                    return Cv2.ImDecode(jpgData, ImreadModes.Color);
                }
                else
                {
                    return baseMat.Clone();
                }
            }
        }

        /// <summary>
        /// Generates multiple OCR training images with streaming to disk
        /// </summary>
        public void GenerateImages(OCRGenerationSettings settings, string outputPath, int imageCount,
            int maxThreads, Action<string> progressCallback = null)
        {
            if (!Directory.Exists(outputPath))
                Directory.CreateDirectory(outputPath);

            progressCallback?.Invoke("Loading strings and fonts...");
            var strings = LoadStrings(settings.StringsFilePath);
            var fonts = LoadFonts(settings.FontFolderPath, settings.EnabledFonts);

            if (!strings.Any())
                throw new InvalidOperationException("No strings loaded from file");

            if (!fonts.Any())
                throw new InvalidOperationException("No fonts found in folder");

            var completed = 0;
            var failed = 0;
            var startTime = DateTime.Now;

            // Thread-safe collections for strings and fonts
            var stringQueue = new ConcurrentQueue<string>(strings);
            var fontQueue = new ConcurrentQueue<string>(fonts);

            // For labels.txt
            var labelsPath = Path.Combine(outputPath, "labels.txt");
            var labelWriteLock = new object();

            // Clear or create labels file
            File.WriteAllText(labelsPath, string.Empty);

            using (var semaphore = new SemaphoreSlim(maxThreads, maxThreads))
            {
                var tasks = new List<Task>();

                for (int i = 0; i < imageCount; i++)
                {
                    var imageIndex = i;

                    var task = Task.Run(async () =>
                    {
                        await semaphore.WaitAsync();

                        try
                        {
                            var text = GetRandomString(stringQueue, strings);
                            var fontPath = GetRandomFont(fontQueue, fonts);

                            // Generate and immediately save to disk
                            using (var mat = GenerateImage(settings, text, fontPath))
                            {
                                var quality = (int)settings.JpgQuality.GetRandomValue();
                                var imageName = $"{imageIndex}.jpg";
                                var imagePath = Path.Combine(outputPath, imageName);

                                SaveImage(mat, imagePath, quality);

                                // Append to labels file
                                lock (labelWriteLock)
                                {
                                    File.AppendAllText(labelsPath, $"{imageName} {text}\n");
                                }
                            }

                            var currentCompleted = Interlocked.Increment(ref completed);

                            if (currentCompleted % 100 == 0 || currentCompleted == imageCount)
                            {
                                var elapsed = DateTime.Now - startTime;
                                var rate = currentCompleted / elapsed.TotalSeconds;
                                var eta = TimeSpan.FromSeconds((imageCount - currentCompleted) / Math.Max(rate, 0.1));

                                progressCallback?.Invoke($"Generated {currentCompleted}/{imageCount} images " +
                                    $"({rate:F1}/sec, ETA: {eta:hh\\:mm\\:ss}, Failed: {failed})");
                            }
                        }
                        catch (Exception ex)
                        {
                            Interlocked.Increment(ref failed);

                            // Still write error entry to labels to maintain numbering
                            lock (labelWriteLock)
                            {
                                File.AppendAllText(labelsPath, $"{imageIndex}.jpg [ERROR: {ex.Message}]\n");
                            }

                            progressCallback?.Invoke($"Failed to generate image {imageIndex}: {ex.Message}");
                        }
                        finally
                        {
                            semaphore.Release();
                        }
                    });

                    tasks.Add(task);
                }

                Task.WaitAll(tasks.ToArray());
            }

            var totalTime = DateTime.Now - startTime;
            progressCallback?.Invoke($"Generation complete! {completed}/{imageCount} images generated " +
                $"in {totalTime:hh\\:mm\\:ss} ({failed} failed)");
        }

        #region Private Helper Methods

        private List<string> LoadStrings(string filePath)
        {
            if (!File.Exists(filePath))
                return new List<string>();

            return File.ReadAllLines(filePath)
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .ToList();
        }

        private List<string> LoadFonts(string folderPath, List<FontSetting> enabledFonts = null)
        {
            if (enabledFonts != null && enabledFonts.Any())
            {
                return enabledFonts.Where(f => f.IsEnabled && File.Exists(f.FilePath))
                                  .Select(f => f.FilePath)
                                  .ToList();
            }

            if (!Directory.Exists(folderPath))
                return new List<string>();

            var extensions = new[] { ".ttf", ".otf", ".woff", ".woff2" };
            return Directory.GetFiles(folderPath, "*.*", SearchOption.AllDirectories)
                .Where(file => extensions.Contains(Path.GetExtension(file).ToLowerInvariant()))
                .ToList();
        }

        private string GetRandomString(ConcurrentQueue<string> queue, List<string> allStrings)
        {
            if (queue.TryDequeue(out string str))
                return str;

            lock (_randomLock)
            {
                return allStrings[_random.Next(allStrings.Count)];
            }
        }

        private string GetRandomFont(ConcurrentQueue<string> queue, List<string> allFonts)
        {
            if (queue.TryDequeue(out string font))
                return font;

            lock (_randomLock)
            {
                return allFonts[_random.Next(allFonts.Count)];
            }
        }

        private Font LoadFont(string fontPath, float fontSize)
        {
            try
            {
                // Cache font collections to keep them alive
                PrivateFontCollection fontCollection;
                lock (_fontCollectionLock)
                {
                    if (!_fontCollections.TryGetValue(fontPath, out fontCollection))
                    {
                        fontCollection = new PrivateFontCollection();
                        fontCollection.AddFontFile(fontPath);
                        _fontCollections[fontPath] = fontCollection;
                    }
                }

                return new Font(fontCollection.Families[0], fontSize, FontStyle.Regular);
            }
            catch
            {
                return new Font(FontFamily.GenericSansSerif, fontSize, FontStyle.Regular);
            }
        }

        private System.Drawing.Size MeasureTextWithSpacing(string text, Font font, float characterSpacing)
        {
            using (var tempBitmap = new Bitmap(1, 1))
            using (var tempGraphics = Graphics.FromImage(tempBitmap))
            {
                tempGraphics.TextRenderingHint = TextRenderingHint.AntiAlias;

                if (Math.Abs(characterSpacing) < 0.01f)
                {
                    var format = new StringFormat(StringFormat.GenericDefault)
                    {
                        FormatFlags = StringFormatFlags.MeasureTrailingSpaces
                    };

                    var measured = tempGraphics.MeasureString(text, font, int.MaxValue, format);
                    return new System.Drawing.Size(
                        (int)Math.Ceiling(measured.Width) + 4,
                        (int)Math.Ceiling(measured.Height) + 2
                    );
                }
                else
                {
                    var totalWidth = 0f;
                    var maxHeight = font.GetHeight(tempGraphics);

                    var naturalSpaceWidth = tempGraphics.MeasureString("i i", font).Width -
                                           tempGraphics.MeasureString("ii", font).Width;
                    naturalSpaceWidth = Math.Max(naturalSpaceWidth, font.Size * 0.3f);

                    for (int i = 0; i < text.Length; i++)
                    {
                        var charStr = text[i].ToString();

                        if (text[i] == ' ')
                        {
                            totalWidth += naturalSpaceWidth + (characterSpacing * 0.1f);
                        }
                        else
                        {
                            var charSize = tempGraphics.MeasureString(charStr, font);
                            totalWidth += charSize.Width;

                            if (i < text.Length - 1)
                            {
                                totalWidth += characterSpacing;
                            }
                        }
                    }

                    totalWidth = Math.Max(1, totalWidth);

                    return new System.Drawing.Size(
                        (int)Math.Ceiling(totalWidth) + 4,
                        (int)Math.Ceiling(maxHeight) + 2
                    );
                }
            }
        }

        private void DrawBackground(Graphics graphics, System.Drawing.Size size, OCRGenerationSettings settings, Random random)
        {
            BackgroundSetting background;
            if (settings.Backgrounds.Count > 0)
            {
                background = settings.Backgrounds[random.Next(settings.Backgrounds.Count)];
            }
            else
            {
                background = new BackgroundSetting();
            }

            switch (background.Type)
            {
                case BackgroundType.SolidColor:
                    using (var brush = new SolidBrush(ColorFromSetting(background.SolidColor)))
                    {
                        graphics.FillRectangle(brush, 0, 0, size.Width, size.Height);
                    }
                    break;

                case BackgroundType.LinearGradient:
                    var angleRad = background.GradientAngle * Math.PI / 180.0;
                    var centerX = size.Width / 2.0f;
                    var centerY = size.Height / 2.0f;
                    var length = Math.Max(size.Width, size.Height);

                    var startX = centerX - (float)(Math.Cos(angleRad) * length / 2);
                    var startY = centerY - (float)(Math.Sin(angleRad) * length / 2);
                    var endX = centerX + (float)(Math.Cos(angleRad) * length / 2);
                    var endY = centerY + (float)(Math.Sin(angleRad) * length / 2);

                    using (var brush = new LinearGradientBrush(
                        new PointF(startX, startY),
                        new PointF(endX, endY),
                        ColorFromSetting(background.GradientStart),
                        ColorFromSetting(background.GradientEnd)))
                    {
                        graphics.FillRectangle(brush, 0, 0, size.Width, size.Height);
                    }
                    break;
            }
        }

        private void DrawTextWithSpacing(Graphics graphics, string text, Font font, float characterSpacing,
            OCRGenerationSettings settings, Random random, Rectangle bounds, System.Drawing.Size textSize)
        {
            var colorSetting = settings.ForegroundColors.Any()
                ? settings.ForegroundColors[random.Next(settings.ForegroundColors.Count)]
                : new ColorSetting { R = 0, G = 0, B = 0, A = 255 };

            using (var brush = new SolidBrush(ColorFromSetting(colorSetting)))
            {
                float startX = bounds.X;
                float y = bounds.Y;

                switch (settings.HorizontalAlignment)
                {
                    case TextAlignment.Left:
                        startX = bounds.X;
                        break;
                    case TextAlignment.Center:
                        startX = bounds.X + (bounds.Width - textSize.Width) / 2.0f;
                        break;
                    case TextAlignment.Right:
                        startX = bounds.X + bounds.Width - textSize.Width;
                        break;
                }

                switch (settings.VerticalAlignment)
                {
                    case VerticalAlignment.Top:
                        y = bounds.Y;
                        break;
                    case VerticalAlignment.Center:
                        var fontHeight = font.GetHeight(graphics);
                        var ascent = fontHeight * font.FontFamily.GetCellAscent(font.Style) / font.FontFamily.GetEmHeight(font.Style);
                        var descent = fontHeight * font.FontFamily.GetCellDescent(font.Style) / font.FontFamily.GetEmHeight(font.Style);
                        var textHeightActual = ascent + descent;

                        y = bounds.Y + (bounds.Height - textHeightActual) / 2.0f + ascent - fontHeight;
                        break;
                    case VerticalAlignment.Bottom:
                        y = bounds.Y + bounds.Height - textSize.Height;
                        break;
                }

                if (settings.DropShadow.Enabled)
                {
                    var shadowOffsetX = (float)settings.DropShadow.OffsetX.GetRandomValue(random);
                    var shadowOffsetY = (float)settings.DropShadow.OffsetY.GetRandomValue(random);
                    var shadowOpacity = (float)settings.DropShadow.Opacity.GetRandomValue(random);

                    var shadowColor = ColorFromSetting(settings.DropShadow.ShadowColor);
                    shadowColor = Color.FromArgb((int)(255 * shadowOpacity), shadowColor.R, shadowColor.G, shadowColor.B);

                    using (var shadowBrush = new SolidBrush(shadowColor))
                    {
                        DrawTextWithSpacingAtPosition(graphics, text, font, characterSpacing, shadowBrush,
                            startX + shadowOffsetX, y + shadowOffsetY);
                    }
                }

                DrawTextWithSpacingAtPosition(graphics, text, font, characterSpacing, brush, startX, y);
            }
        }

        private void DrawTextWithSpacingAtPosition(Graphics graphics, string text, Font font, float characterSpacing,
            Brush brush, float x, float y)
        {
            if (Math.Abs(characterSpacing) < 0.01f)
            {
                graphics.DrawString(text, font, brush, x, y);
            }
            else
            {
                var currentX = x;

                var naturalSpaceWidth = graphics.MeasureString("i i", font).Width -
                                       graphics.MeasureString("ii", font).Width;
                naturalSpaceWidth = Math.Max(naturalSpaceWidth, font.Size * 0.3f);

                for (int i = 0; i < text.Length; i++)
                {
                    var character = text[i].ToString();

                    if (text[i] == ' ')
                    {
                        currentX += naturalSpaceWidth + (characterSpacing * 0.1f);
                    }
                    else
                    {
                        graphics.DrawString(character, font, brush, currentX, y);

                        var charSize = graphics.MeasureString(character, font);
                        currentX += charSize.Width;

                        if (i < text.Length - 1)
                        {
                            currentX += characterSpacing;
                        }
                    }
                }
            }
        }

        private Mat ApplyEffects(Mat inputMat, OCRGenerationSettings settings, Random random)
        {
            var mat = inputMat.Clone();

            try
            {
                var blurRadius = settings.BlurRadius.GetRandomValue(random);
                if (blurRadius > 0)
                {
                    var kernelSize = (int)(blurRadius * 2) | 1;
                    if (kernelSize >= 3)
                    {
                        Cv2.GaussianBlur(mat, mat, new OpenCvSharp.Size(kernelSize, kernelSize), blurRadius);
                    }
                }

                var warpStrength = settings.WarpStrength.GetRandomValue(random);
                if (warpStrength > 0)
                {
                    mat = ApplyPerspectiveTransform(mat, warpStrength, random);
                }

                var skewAngle = settings.SkewAngle.GetRandomValue(random);
                if (Math.Abs(skewAngle) > 0.1)
                {
                    mat = ApplySkew(mat, skewAngle);
                }

                var rotationAngle = settings.RotationAngle.GetRandomValue(random);
                if (Math.Abs(rotationAngle) > 0.1)
                {
                    mat = ApplyRotation(mat, rotationAngle);
                }

                var gaussianNoise = settings.GaussianNoise.GetRandomValue(random);
                if (gaussianNoise > 0)
                {
                    mat = ApplyGaussianNoise(mat, gaussianNoise, random);
                }

                var saltPepperNoise = settings.SaltPepperNoise.GetRandomValue(random);
                if (saltPepperNoise > 0)
                {
                    mat = ApplySaltPepperNoise(mat, saltPepperNoise, random);
                }

                var rescaledHeight = (int)settings.RescaledHeight.GetRandomValue(random);
                if (rescaledHeight != mat.Height && rescaledHeight > 0)
                {
                    var rescaledWidth = (int)(mat.Width * (rescaledHeight / (double)mat.Height));
                    Cv2.Resize(mat, mat, new OpenCvSharp.Size(rescaledWidth, rescaledHeight), 0, 0, InterpolationFlags.Linear);
                }

                return mat;
            }
            catch
            {
                mat?.Dispose();
                return inputMat.Clone();
            }
        }

        private Mat ApplyPerspectiveTransform(Mat input, double strength, Random random)
        {
            var height = input.Height;
            var width = input.Width;

            var maxOffset = strength * Math.Min(width, height) * 0.1;

            var srcPoints = new Point2f[]
            {
                new Point2f(0, 0),
                new Point2f(width, 0),
                new Point2f(width, height),
                new Point2f(0, height)
            };

            var dstPoints = new Point2f[]
            {
                new Point2f((float)(random.NextDouble() * maxOffset), (float)(random.NextDouble() * maxOffset)),
                new Point2f(width - (float)(random.NextDouble() * maxOffset), (float)(random.NextDouble() * maxOffset)),
                new Point2f(width - (float)(random.NextDouble() * maxOffset), height - (float)(random.NextDouble() * maxOffset)),
                new Point2f((float)(random.NextDouble() * maxOffset), height - (float)(random.NextDouble() * maxOffset))
            };

            var transform = Cv2.GetPerspectiveTransform(srcPoints, dstPoints);
            var result = new Mat();
            Cv2.WarpPerspective(input, result, transform, input.Size());

            transform.Dispose();
            return result;
        }

        private Mat ApplySkew(Mat input, double angle)
        {
            var radians = angle * Math.PI / 180.0;
            var skewMatrix = new Mat(2, 3, MatType.CV_64F);

            skewMatrix.Set<double>(0, 0, 1);
            skewMatrix.Set<double>(0, 1, Math.Tan(radians));
            skewMatrix.Set<double>(0, 2, 0);
            skewMatrix.Set<double>(1, 0, 0);
            skewMatrix.Set<double>(1, 1, 1);
            skewMatrix.Set<double>(1, 2, 0);

            var result = new Mat();
            Cv2.WarpAffine(input, result, skewMatrix, input.Size());

            skewMatrix.Dispose();
            return result;
        }

        private Mat ApplyRotation(Mat input, double angle)
        {
            var center = new Point2f(input.Width / 2.0f, input.Height / 2.0f);
            var rotationMatrix = Cv2.GetRotationMatrix2D(center, angle, 1.0);

            var result = new Mat();
            Cv2.WarpAffine(input, result, rotationMatrix, input.Size());

            rotationMatrix.Dispose();
            return result;
        }

        private Mat ApplyGaussianNoise(Mat input, double intensity, Random random)
        {
            var noise = new Mat(input.Size(), input.Type());
            var mean = new Scalar(0);
            var stddev = new Scalar(intensity * 255);

            Cv2.Randn(noise, mean, stddev);

            var result = new Mat();
            Cv2.Add(input, noise, result);

            noise.Dispose();
            return result;
        }

        private Mat ApplySaltPepperNoise(Mat input, double intensity, Random random)
        {
            var result = input.Clone();
            var totalPixels = input.Width * input.Height * input.Channels();
            var noisePixels = (int)(totalPixels * intensity);

            for (int i = 0; i < noisePixels; i++)
            {
                var x = random.Next(input.Width);
                var y = random.Next(input.Height);
                var value = random.NextDouble() > 0.5 ? 255 : 0;

                result.Set(y, x, value);
            }

            return result;
        }

        private void SaveImage(Mat mat, string filePath, int quality)
        {
            var compressionParams = new int[]
            {
                (int)ImwriteFlags.JpegQuality, quality
            };

            Cv2.ImWrite(filePath, mat, compressionParams);
        }

        private Color ColorFromSetting(ColorSetting colorSetting)
        {
            return Color.FromArgb(colorSetting.A, colorSetting.R, colorSetting.G, colorSetting.B);
        }

        #endregion
    }
}