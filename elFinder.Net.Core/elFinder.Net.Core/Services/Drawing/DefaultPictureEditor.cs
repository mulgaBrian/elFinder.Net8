using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;

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

    public virtual Task<ImageWithMimeType> CropAsync(Image input, int x, int y, int width, int height, int? quality = null)
    {
      if (quality == 0) { quality = null; }

      input.Mutate(im => im.Crop(new Rectangle(x, y, width, height)));

      IImageFormat format = input.Metadata.DecodedImageFormat;

      if (quality == null) return SaveImageAsync(input, format);

      return ChangeQualityAsync(input, format, quality);
      //}
    }

    public virtual Task<ImageWithMimeType> ScaleAsync(Image input, int width, int height, int? quality = null)
    {
      if (quality == 0) { quality = null; }
      try
      {
        //Image image = Image.Load(input);
        IImageFormat format = input.Metadata.DecodedImageFormat;// (IImageFormat)image.GetConfiguration().ImageFormats;
        return ScaleAsync(input, format, width, height, quality);
      }
      catch
      {
        throw;
      }
    }

    public virtual Task<ImageWithMimeType> GenerateThumbnailAsync(Stream input, int size, bool keepAspectRatio)
    {
      using var inputImage = Image.Load(input);
      int targetWidth;
      int targetHeight;

      if (keepAspectRatio)
      {
        double width = inputImage.Width;
        double height = inputImage.Height;
        double percentWidth = width != 0 ? size / width : 0;
        double percentHeight = height != 0 ? size / height : 0;
        double percent = percentHeight < percentWidth ? percentHeight : percentWidth;

        targetWidth = (int)(width * percent);
        targetHeight = (int)(height * percent);
      }
      else
      {
        targetWidth = size;
        targetHeight = size;
      }

      return ScaleAsync(inputImage, targetWidth, targetHeight);
    }

    public virtual Size ImageSize(Stream input)
    {
      using var image = Image.Load(input);
      return image.Size;
    }

    public virtual Size ImageSize(string fullPath)
    {
      using var image = Image.Load(fullPath);
      return new Size(image.Width, image.Height);
    }

    public virtual Task<ImageWithMimeType> RotateAsync(Image image, IImageFormat format, int angle, Color? background = null, int? quality = null)
    {
      if (quality == 0) { quality = null; }

      image.Mutate(im => im
          .Rotate(angle)
          .BackgroundColor(background ?? BackgroundColor));

      if (quality == null) return SaveImageAsync(image, format);

      return ChangeQualityAsync(image, format, quality);
    }

    public virtual Task<ImageWithMimeType> RotateAsync(Stream input, int angle, string backgroundHex = null, int? quality = null)
    {
      Color bgColor = BackgroundColor;
      if (quality == 0) { quality = null; }

      if (!string.IsNullOrEmpty(backgroundHex))
      {
        bgColor = Color.ParseHex($"{backgroundHex[1..]}FF");
      }

      using var image = Image.Load(input);

      IImageFormat format = image.Metadata.DecodedImageFormat;
      return RotateAsync(image, format, angle, bgColor, quality);
    }

    public virtual Task<ImageWithMimeType> ScaleAsync(Image image, IImageFormat format, int width, int height, int? quality = null)
    {
      if (quality == 0) { quality = null; }

      image.Mutate(im => im
          .Resize(width, height));

      if (quality == null) return SaveImageAsync(image, format);

      return ChangeQualityAsync(image, format, quality);
    }

    protected virtual async Task<ImageWithMimeType> SaveImageAsync(Image image, IImageFormat format)
    {
      var memoryStream = new MemoryStream();
      string mimeType;
      await image.SaveAsync(memoryStream, format);
      mimeType = format.DefaultMimeType;
      memoryStream.Position = 0;

      return new ImageWithMimeType(mimeType, memoryStream);
    }

    protected virtual async Task<ImageWithMimeType> ChangeQualityAsync(Image image, IImageFormat format, int? quality = null)
    {
      if (quality == 0) { quality = null; }
      var memoryStream = new MemoryStream();
      IImageEncoder imageEncoder;
      string mimeType = JpegFormat.Instance.DefaultMimeType;
      imageEncoder = new JpegEncoder
      {
        Quality = quality,
      };

      await image.SaveAsync(memoryStream, imageEncoder);
      memoryStream.Position = 0;

      return new ImageWithMimeType(mimeType, memoryStream);
    }
  }
}
