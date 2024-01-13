﻿using HeyRed.ImageSharp.AVCodecFormats;
using HeyRed.ImageSharp.AVCodecFormats.Webm;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;

namespace elFinder.Net.Core.Services.Drawing
{
  public class DefaultVideoEditor : IVideoEditor
  {
    public bool CanProcessFile(string fileExtension)
    {
      //return false;
      string ext = fileExtension.ToLower();

      return ext == ".mp4"
          || ext == ".mpeg"
          || ext == ".mkv"
          || ext == ".m4v"
          || ext == ".avi"
          || ext == ".mov"
          || ext == ".webm"
          || ext == ".wmv";

    }

    public async Task<ImageWithMimeType> GenerateThumbnailAsync(IFile file, int size,
        bool keepAspectRatio, CancellationToken cancellationToken = default)
    {
      ImageWithMimeType thumb = await CreateThumbnailUsingFfmpeg(file.FullName, size);

      return await Task.FromResult(thumb);
      //return Task.FromResult(default(ImageWithMimeType));
    }

    public async Task<ImageWithMimeType> GenerateThumbnailInBackgroundAsync(IFile file, int size,
        bool keepAspectRatio, CancellationToken cancellationToken = default)
    {
      ImageWithMimeType thumb = await CreateThumbnailUsingFfmpeg(file.FullName, size);

      return await Task.FromResult(thumb);
      //return await Task.FromResult(default(ImageWithMimeType));
    }

    protected static Task<ImageWithMimeType> CreateThumbnailUsingFfmpeg(string mediaFilePath, int size)
    {
      // Create custom configuration with all available decoders
      Configuration configuration = new Configuration().WithAVDecoders();
      // NOTE: Don't forget to set max frames.
      // Without this limit you can run into huge memory usage.
      var decoderOptions = new AVDecoderOptions
      {
        PreserveAspectRatio = true,
        GeneralOptions = new DecoderOptions
        {
          MaxFrames = 50,
          Configuration = configuration,
          TargetSize = new Size(size*2),
        },
      };

      using FileStream inputStream = File.OpenRead(mediaFilePath);
      using var image = Image.Load(decoderOptions.GeneralOptions, inputStream);

      var format = new JpegEncoder(){ Quality = 40 };
      var memoryStream = new MemoryStream();
      string mimeType;
      image.Frames
        .CloneFrame((int)decoderOptions.GeneralOptions.MaxFrames - 1)
        .SaveAsync(memoryStream, format);
      image.Dispose();
      //image.SaveAsync(memoryStream, format);
      mimeType = "image/jpeg";
      memoryStream.Position = 0;

      return Task.FromResult(new ImageWithMimeType(mimeType, memoryStream));

    }
  }
}
