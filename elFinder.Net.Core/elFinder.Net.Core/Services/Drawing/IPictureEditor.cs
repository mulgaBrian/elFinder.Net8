using ImageMagick;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;

namespace elFinder.Net.Core.Services.Drawing
{
  public interface IPictureEditor
  {
    Color BackgroundColor { get; set; }

    bool CanProcessFile(string fileExtension);
    Task<ImageWithMimeType> CropAsync(MagickImage input, uint x, uint y, uint width, uint height, int? quality = null);
    Task<ImageWithMimeType> GenerateThumbnailAsync(Stream input, int size, bool keepAspectRatio);
    Task<Size> ImageSize(Stream input);
    Task<Size> ImageSize(string fullPath);
    Task<ImageWithMimeType> RotateAsync(MagickImage image, MagickFormat format, int angle, Color? background = null, int? quality = null);
    Task<ImageWithMimeType> RotateAsync(Stream input, int angle, string backgroundHex = null, int? quality = null);
    Task<ImageWithMimeType> ScaleAsync(MagickImage input, int width, int height, int? quality = null);
  }
}