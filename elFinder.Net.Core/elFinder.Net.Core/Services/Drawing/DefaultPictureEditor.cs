using ImageMagick;
using System.Drawing;
using static elFinder.Net.Core.Models.FileInfo.BaseInfoResponse;
using static System.Net.Mime.MediaTypeNames;
namespace elFinder.Net.Core.Services.Drawing
{
  /// <summary>
  /// Represents default pictures editor
  /// </summary>
  public class DefaultPictureEditor(Color backgroundColor) : IPictureEditor
  {
    public DefaultPictureEditor()
        : this(Color.Transparent)
    {
    }

    public virtual Color BackgroundColor { get; set; } = backgroundColor;

    public virtual bool CanProcessFile(string fileExtension)
    {
      string ext = fileExtension.ToLower();

      return ext == ".png"
          || ext == ".jpg"
          || ext == ".jpeg"
          || ext == ".gif"
          || ext == ".tiff"
          || ext == ".bmp"
          || ext == ".pbm"
          || ext == ".tga"
          || ext == ".webp";
    }

    public virtual Task<ImageWithMimeType> CropAsync(MagickImage input, uint x, uint y, uint width, uint height, int? quality = null)
    {
      if (quality == 0) { quality = null; }

      input.Crop(width, height, Gravity.Center);

      MagickFormat format = input.Format; // .Metadata.DecodedImageFormat;

      if (quality == null) return SaveImageAsync(input, format);

      return ChangeQualityAsync(input, format, quality);
    }

    public virtual Task<ImageWithMimeType> ScaleAsync(MagickImage input, int width, int height, int? quality = null)
    {
      if (quality == 0) { quality = null; }
      try
      {
        MagickImage image = new MagickImage(input);
        MagickFormat format = image.Format; //.Metadata.DecodedImageFormat;// (IImageFormat)image.GetConfiguration().ImageFormats;
        return ScaleAsync(input, format, width, height, quality);
      }
      catch
      {
        throw;
      }
    }

    public virtual Task<ImageWithMimeType> ScaleAsync(MagickImage image, MagickFormat format, int width, int height, int? quality = null)
    {
      if (quality == 0) { quality = null; }

      image.Resize((uint)width, (uint)height);

      if (quality == null) return SaveImageAsync(image, format);

      return ChangeQualityAsync(image, format, quality);
    }

    public virtual async Task<ImageWithMimeType> GenerateThumbnailAsync(Stream input, int size, bool keepAspectRatio)
    {
      using var inputImage = new MagickImage(input);
      var profile = inputImage.GetExifProfile();
      int targetWidth;
      int targetHeight;

      if (keepAspectRatio)
      {
        double width = inputImage.Width;
        double height = inputImage.Height;
        double percentWidth = width != 0 ? size / width : 0;
        double percentHeight = height != 0 ? size / height : 0;
        double percent = percentHeight < percentWidth ? percentHeight : percentWidth;

        targetWidth = 48; //(int)(width * percent);
        targetHeight = 48; //(int)(height * percent);
        if (height > width) targetWidth = 0;
      }
      else
      {
        targetWidth = size;
        targetHeight = size;
      }

      return await ScaleAsync(inputImage,targetWidth,targetHeight);
    }

    public virtual async Task<Size> ImageSize(Stream input)
    {
      using var image = new MagickImage(input);
      //return image.Size;
      Size size = new((int)image.Width, (int)image.Height);
      return size;
    }

    public virtual async Task<Size> ImageSize(string fullPath)
    {
      using var image = new MagickImage(fullPath);
      return new Size((int)image.Width, (int)image.Height);
    }

    public virtual Task<ImageWithMimeType> RotateAsync(MagickImage image, MagickFormat format, int angle, Color? background = null, int? quality = null)
    {
      if (quality == 0) { quality = null; }

      image.Rotate(angle);
      //.BackgroundColor(background ?? BackgroundColor));

      if (quality == null) return SaveImageAsync(image, format);

      return ChangeQualityAsync(image, format, quality);
    }

    protected virtual async Task<ImageWithMimeType> SaveImageAsync(MagickImage image, MagickFormat format)
    {
      var memoryStream = new MemoryStream();
      string mimeType;
      await image.WriteAsync(memoryStream, format);
      mimeType = $"image/{format.ToString().ToLower()}";
      memoryStream.Position = 0;

      return new ImageWithMimeType(mimeType, memoryStream);
    }

    public virtual Task<ImageWithMimeType> RotateAsync(Stream input, int angle, string backgroundHex = null, int? quality = null)
    {
      Color bgColor = BackgroundColor;
      if (quality == 0) { quality = null; }

      if (!string.IsNullOrEmpty(backgroundHex))
      {
        bgColor = ColorTranslator.FromHtml($"{backgroundHex[1..]}FF");
      }

      using var image = new MagickImage(input);

      MagickFormat format = image.Format; // .Metadata.DecodedImageFormat;
      return RotateAsync(image, format, angle, bgColor, quality);
    }

    protected virtual async Task<ImageWithMimeType> ChangeQualityAsync(MagickImage image, MagickFormat format, int? quality = null)
    {
      if (quality == 0) { quality = null; }
      var memoryStream = new MemoryStream();
      //IImageEncoder imageEncoder;
      //string mimeType = MagickFormat.Jpg.ToString();
      //imageEncoder = new JpegEncoder
      //{
      //  Quality = quality,
      //};

      image.Format = MagickFormat.Jpg;
      image.Write(memoryStream);
      memoryStream.Position = 0;

      return new ImageWithMimeType("image/jpg", memoryStream);
    }
  }
}
