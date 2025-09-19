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

        /// <summary>
        /// Generates a single OCR training image
        /// </summary>
        /// <param name="settings">Generation settings</param>
        /// <param name="text">Text to render</param>
        /// <param name="fontPath">Path to the font file</param>
        /// <returns>Generated image as OpenCV Mat</returns>
        public Mat GenerateImage(OCRGenerationSettings settings, string text, string fontPath)
        {
            Random localRandom;
            lock (_randomLock)
            {
                localRandom = new Random(_random.Next());
            }

            // UPDATED: Create initial bitmap with proper text measurement
            var fontSize = (float)settings.FontSize.GetRandomValue(localRandom);
            var marginLeft = (int)settings.Margins.Left.GetRandomValue(localRandom);
            var marginRight = (int)settings.Margins.Right.GetRandomValue(localRandom);
            var marginTop = (int)settings.Margins.Top.GetRandomValue(localRandom);
            var marginBottom = (int)settings.Margins.Bottom.GetRandomValue(localRandom);

            // UPDATED: Measure text properly before creating bitmap
            System.Drawing.Size textSize;
            Font font = null;

            try
            {
                font = LoadFont(fontPath, fontSize);
                textSize = MeasureText(text, font);
            }
            catch
            {
                font = new Font(FontFamily.GenericSansSerif, fontSize, FontStyle.Regular);
                textSize = MeasureText(text, font);
            }

            // UPDATED: Calculate total dimensions based on actual text size
            var totalWidth = textSize.Width + marginLeft + marginRight;
            var totalHeight = Math.Max((int)settings.InitialHeight.GetRandomValue(localRandom),
                                     textSize.Height + marginTop + marginBottom);

            using (var bitmap = new Bitmap(totalWidth, totalHeight))
            using (var graphics = Graphics.FromImage(bitmap))
            {
                // Configure graphics quality
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.TextRenderingHint = TextRenderingHint.AntiAlias;
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;

                // UPDATED: Draw background using multiple background support
                DrawBackground(graphics, bitmap.Size, settings, localRandom);

                // UPDATED: Draw text with proper vertical centering
                DrawText(graphics, text, font, settings, localRandom,
                    new Rectangle(marginLeft, marginTop, textSize.Width, totalHeight - marginTop - marginBottom),
                    textSize);

                // Convert to OpenCV Mat
                var mat = bitmap.ToMat();

                // Apply effects
                mat = ApplyEffects(mat, settings, localRandom);

                return mat;
            }
        }

        /// <summary>
        /// Generates multiple OCR training images with multithreading
        /// </summary>
        /// <param name="settings">Generation settings</param>
        /// <param name="outputPath">Path where to save images and labels</param>
        /// <param name="imageCount">Number of images to generate</param>
        /// <param name="maxThreads">Maximum number of threads to use</param>
        /// <param name="progressCallback">Callback for progress updates</param>
        public void GenerateImages(OCRGenerationSettings settings, string outputPath, int imageCount,
            int maxThreads, Action<string> progressCallback = null)
        {
            // Validate paths
            if (!Directory.Exists(outputPath))
                Directory.CreateDirectory(outputPath);

            // Load strings and fonts
            progressCallback?.Invoke("Loading strings and fonts...");
            var strings = LoadStrings(settings.StringsFilePath);
            var fonts = LoadFonts(settings.FontFolderPath);

            if (!strings.Any())
                throw new InvalidOperationException("No strings loaded from file");

            if (!fonts.Any())
                throw new InvalidOperationException("No fonts found in folder");

            // Create labels directory
            var labelsPath = Path.Combine(outputPath, "labels");
            if (!Directory.Exists(labelsPath))
                Directory.CreateDirectory(labelsPath);

            // Progress tracking
            var completed = 0;
            var failed = 0;
            var startTime = DateTime.Now;

            // Thread-safe collections
            var stringQueue = new ConcurrentQueue<string>(strings);
            var fontQueue = new ConcurrentQueue<string>(fonts);

            // Semaphore for thread limiting
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
                            // Get random string and font
                            var text = GetRandomString(stringQueue, strings);
                            var fontPath = GetRandomFont(fontQueue, fonts);

                            // Generate image
                            using (var mat = GenerateImage(settings, text, fontPath))
                            {
                                // Save image
                                var imageName = $"image_{imageIndex:D6}.jpg";
                                var imagePath = Path.Combine(outputPath, imageName);

                                // Apply JPG quality settings
                                var quality = (int)settings.JpgQuality.GetRandomValue();
                                SaveImage(mat, imagePath, quality);

                                // Save label
                                var labelPath = Path.Combine(labelsPath, $"image_{imageIndex:D6}.txt");
                                File.WriteAllText(labelPath, text);
                            }

                            // Update progress
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
                            progressCallback?.Invoke($"Failed to generate image {imageIndex}: {ex.Message}");
                        }
                        finally
                        {
                            semaphore.Release();
                        }
                    });

                    tasks.Add(task);
                }

                // Wait for all tasks to complete
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

        private List<string> LoadFonts(string folderPath)
        {
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

        // UPDATED: New method to load fonts properly
        private Font LoadFont(string fontPath, float fontSize)
        {
            try
            {
                var fontCollection = new PrivateFontCollection();
                fontCollection.AddFontFile(fontPath);
                return new Font(fontCollection.Families[0], fontSize, FontStyle.Regular);
            }
            catch
            {
                // Fallback to system font
                return new Font(FontFamily.GenericSansSerif, fontSize, FontStyle.Regular);
            }
        }

        // UPDATED: New method to measure text accurately
        private System.Drawing.Size MeasureText(string text, Font font)
        {
            using (var tempBitmap = new Bitmap(1, 1))
            using (var tempGraphics = Graphics.FromImage(tempBitmap))
            {
                tempGraphics.TextRenderingHint = TextRenderingHint.AntiAlias;

                // Use MeasureString with StringFormat for more accurate measurement
                var format = new StringFormat(StringFormat.GenericDefault)
                {
                    FormatFlags = StringFormatFlags.MeasureTrailingSpaces
                };

                var measured = tempGraphics.MeasureString(text, font, int.MaxValue, format);

                // Add some padding to ensure text isn't clipped
                return new System.Drawing.Size(
                    (int)Math.Ceiling(measured.Width) + 4,
                    (int)Math.Ceiling(measured.Height) + 2
                );
            }
        }

        // UPDATED: Updated to support multiple backgrounds
        private void DrawBackground(Graphics graphics, System.Drawing.Size size, OCRGenerationSettings settings, Random random)
        {
            // Use random background if multiple are available
            BackgroundSetting background;
            if (settings.Backgrounds.Count > 0)
            {
                background = settings.Backgrounds[random.Next(settings.Backgrounds.Count)];
            }
            else
            {
                // Fallback to default white background
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
                    // Calculate gradient endpoints based on angle
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

        // UPDATED: Improved text drawing with proper vertical centering
        private void DrawText(Graphics graphics, string text, Font font, OCRGenerationSettings settings,
            Random random, Rectangle bounds, System.Drawing.Size textSize)
        {
            // Get random foreground color
            var colorSetting = settings.ForegroundColors.Any()
                ? settings.ForegroundColors[random.Next(settings.ForegroundColors.Count)]
                : new ColorSetting { R = 0, G = 0, B = 0, A = 255 };

            using (var brush = new SolidBrush(ColorFromSetting(colorSetting)))
            {
                // UPDATED: Calculate precise positioning for proper alignment
                float x = bounds.X;
                float y = bounds.Y;

                // Horizontal alignment
                switch (settings.HorizontalAlignment)
                {
                    case TextAlignment.Left:
                        x = bounds.X;
                        break;
                    case TextAlignment.Center:
                        x = bounds.X + (bounds.Width - textSize.Width) / 2.0f;
                        break;
                    case TextAlignment.Right:
                        x = bounds.X + bounds.Width - textSize.Width;
                        break;
                }

                // UPDATED: Improved vertical alignment with font metrics
                switch (settings.VerticalAlignment)
                {
                    case VerticalAlignment.Top:
                        y = bounds.Y;
                        break;
                    case VerticalAlignment.Center:
                        // Use font metrics for proper centering
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

                // Draw drop shadow if enabled
                if (settings.DropShadow.Enabled)
                {
                    var shadowOffsetX = (float)settings.DropShadow.OffsetX.GetRandomValue(random);
                    var shadowOffsetY = (float)settings.DropShadow.OffsetY.GetRandomValue(random);
                    var shadowOpacity = (float)settings.DropShadow.Opacity.GetRandomValue(random);

                    var shadowColor = ColorFromSetting(settings.DropShadow.ShadowColor);
                    shadowColor = Color.FromArgb((int)(255 * shadowOpacity), shadowColor.R, shadowColor.G, shadowColor.B);

                    using (var shadowBrush = new SolidBrush(shadowColor))
                    {
                        graphics.DrawString(text, font, shadowBrush, x + shadowOffsetX, y + shadowOffsetY);
                    }
                }

                // Draw main text at calculated position
                graphics.DrawString(text, font, brush, x, y);
            }
        }

        private Mat ApplyEffects(Mat inputMat, OCRGenerationSettings settings, Random random)
        {
            var mat = inputMat.Clone();

            try
            {
                // Apply blur
                var blurRadius = settings.BlurRadius.GetRandomValue(random);
                if (blurRadius > 0)
                {
                    var kernelSize = (int)(blurRadius * 2) | 1; // Make odd
                    if (kernelSize >= 3)
                    {
                        Cv2.GaussianBlur(mat, mat, new OpenCvSharp.Size(kernelSize, kernelSize), blurRadius);
                    }
                }

                // Apply warp/perspective transform
                var warpStrength = settings.WarpStrength.GetRandomValue(random);
                if (warpStrength > 0)
                {
                    mat = ApplyPerspectiveTransform(mat, warpStrength, random);
                }

                // Apply skew
                var skewAngle = settings.SkewAngle.GetRandomValue(random);
                if (Math.Abs(skewAngle) > 0.1)
                {
                    mat = ApplySkew(mat, skewAngle);
                }

                // Apply rotation
                var rotationAngle = settings.RotationAngle.GetRandomValue(random);
                if (Math.Abs(rotationAngle) > 0.1)
                {
                    mat = ApplyRotation(mat, rotationAngle);
                }

                // Apply Gaussian noise
                var gaussianNoise = settings.GaussianNoise.GetRandomValue(random);
                if (gaussianNoise > 0)
                {
                    mat = ApplyGaussianNoise(mat, gaussianNoise, random);
                }

                // Apply salt and pepper noise
                var saltPepperNoise = settings.SaltPepperNoise.GetRandomValue(random);
                if (saltPepperNoise > 0)
                {
                    mat = ApplySaltPepperNoise(mat, saltPepperNoise, random);
                }

                // Apply rescaling
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