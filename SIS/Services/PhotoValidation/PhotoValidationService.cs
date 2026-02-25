using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SIS.Models.StudentApplication;

namespace SIS.Services.PhotoValidation
{
    public class PhotoValidationService : IPhotoValidationService
    {
        public async Task<PhotoValidationResult> ValidatePassportPhotoAsync(IFormFile photo)
        {
            var result = new PhotoValidationResult
            {
                IsValid = true,
                Errors = new List<string>(),
                Warnings = new List<string>()
            };

            if (photo == null || photo.Length == 0)
            {
                result.IsValid = false;
                result.Errors.Add("No photo file provided.");
                return result;
            }

            // Validate file size (2MB max)
            if (photo.Length > 2 * 1024 * 1024)
            {
                result.IsValid = false;
                result.Errors.Add("Photo file size must be less than 2MB.");
                return result;
            }

            // Validate file format
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png" };
            var fileExtension = Path.GetExtension(photo.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(fileExtension))
            {
                result.IsValid = false;
                result.Errors.Add("Photo must be in JPG, JPEG, or PNG format.");
                return result;
            }

            try
            {
                using var stream = photo.OpenReadStream();
                using var image = await Image.LoadAsync<Rgba32>(stream);

                // Check minimum dimensions (flexible)
                if (image.Width < 200 || image.Height < 250)
                {
                    result.IsValid = false;
                    result.Errors.Add("Photo dimensions are too small. Minimum size is 200x250 pixels.");
                    return result;
                }

                // Recommend better dimensions
                if (image.Width < 300 || image.Height < 400)
                {
                    result.Warnings.Add("For best results, use a photo with dimensions of at least 300x400 pixels.");
                }

                // Check aspect ratio (should be roughly portrait)
                var aspectRatio = (double)image.Width / image.Height;
                if (aspectRatio > 1.0) // Width greater than height
                {
                    result.Warnings.Add("Photo should be in portrait orientation (height greater than width).");
                }

                // Basic background analysis
                var backgroundScore = AnalyzeBackground(image);
                if (backgroundScore < 0.6) // Less than 60% likely to be light/white
                {
                    result.Warnings.Add("For best results, use a photo with a light or white background.");
                }

                // Basic face detection (simplified)
                var hasFace = HasPotentialFace(image);
                if (!hasFace)
                {
                    result.Warnings.Add("Unable to detect a clear face in the photo. Please ensure your face is clearly visible.");
                }

                result.Message = result.Errors.Any() ? "Photo validation failed." :
                               result.Warnings.Any() ? "Photo uploaded with recommendations." :
                               "Photo uploaded successfully.";

                return result;
            }
            catch (Exception ex)
            {
                result.IsValid = false;
                result.Errors.Add("Unable to process the image file. Please ensure it's a valid photo.");
                return result;
            }
        }

        private double AnalyzeBackground(Image<Rgba32> image)
        {
            var sampleSize = 50; // Sample pixels around the edges
            var lightPixelCount = 0;
            var totalSamples = 0;

            // Sample top and bottom edges
            for (int x = 0; x < image.Width; x += Math.Max(1, image.Width / sampleSize))
            {
                // Top edge
                var topPixel = image[x, 0];
                if (IsLightPixel(topPixel)) lightPixelCount++;
                totalSamples++;

                // Bottom edge
                var bottomPixel = image[x, image.Height - 1];
                if (IsLightPixel(bottomPixel)) lightPixelCount++;
                totalSamples++;
            }

            // Sample left and right edges
            for (int y = 0; y < image.Height; y += Math.Max(1, image.Height / sampleSize))
            {
                // Left edge
                var leftPixel = image[0, y];
                if (IsLightPixel(leftPixel)) lightPixelCount++;
                totalSamples++;

                // Right edge
                var rightPixel = image[image.Width - 1, y];
                if (IsLightPixel(rightPixel)) lightPixelCount++;
                totalSamples++;
            }

            return totalSamples > 0 ? (double)lightPixelCount / totalSamples : 0;
        }

        private bool IsLightPixel(Rgba32 pixel)
        {
            // Consider a pixel "light" if it's close to white
            var brightness = (pixel.R + pixel.G + pixel.B) / 3.0;
            return brightness > 200; // Threshold for "light" background
        }

        private bool HasPotentialFace(Image<Rgba32> image)
        {
            // Very basic face detection - look for skin-tone colors in center area
            var centerX = image.Width / 2;
            var centerY = image.Height / 3; // Upper third where face usually is
            var searchRadius = Math.Min(image.Width, image.Height) / 6;

            var skinTonePixels = 0;
            var totalChecked = 0;

            for (int x = Math.Max(0, centerX - searchRadius); x < Math.Min(image.Width, centerX + searchRadius); x += 5)
            {
                for (int y = Math.Max(0, centerY - searchRadius); y < Math.Min(image.Height, centerY + searchRadius); y += 5)
                {
                    var pixel = image[x, y];
                    if (IsSkinTone(pixel)) skinTonePixels++;
                    totalChecked++;
                }
            }

            // If at least 10% of center area has skin-tone colors, consider face present
            return totalChecked > 0 && (double)skinTonePixels / totalChecked > 0.1;
        }

        private bool IsSkinTone(Rgba32 pixel)
        {
            // Basic skin tone detection
            return pixel.R > 80 && pixel.R < 255 &&
                   pixel.G > 50 && pixel.G < 220 &&
                   pixel.B > 30 && pixel.B < 180 &&
                   pixel.R > pixel.G && pixel.G > pixel.B;
        }
    }
}