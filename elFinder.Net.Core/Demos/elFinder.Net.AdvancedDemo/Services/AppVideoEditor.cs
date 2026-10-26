using elFinder.Net.Core;
using elFinder.Net.Core.Services.Drawing;
using ImageMagick;
using Microsoft.AspNetCore.StaticFiles;
using ThumbGen;
using ThumbGen.Builder;

namespace elFinder.Net.AdvancedDemo.Services
{
  public class AppVideoEditor : IVideoEditor
  {
    public static readonly IEnumerable<string> VideoExts = new[]
    {
            ".mp4" , ".avi" , ".mxf" , ".webm" , ".mkv" , ".flv", ".mpeg" , ".mov"
        };

    static AppVideoEditor()
    {
      ///FFmpeg.SetExecutablesPath(Path.Combine(AppContext.BaseDirectory, FFmpegDirectory));
    }

    private readonly IPictureEditor _pictureEditor;

    public AppVideoEditor(IPictureEditor pictureEditor)
    {
      _pictureEditor = pictureEditor;
    }

    public bool CanProcessFile(string fileExtension)
    {
      //return false;
      return VideoExts.Contains(fileExtension, StringComparer.InvariantCultureIgnoreCase);

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


    protected static async Task<ImageWithMimeType> CreateThumbnailUsingFfmpeg(string mediaFilePath, int size)
    {
      TimeSpan captureTime = TimeSpan.FromSeconds(5);
      string outFileName = Path.ChangeExtension(mediaFilePath, ".jpg");
      string mediaFileName = Path.GetFileName(outFileName);
      string outFilePath = Path.Combine("wwwroot", ".thumb", mediaFileName);
      string mimeType = await GetMimeType(mediaFileName);
      await GenerateAsync(mediaFilePath, outFilePath, captureTime);

      MagickImage img = new(outFilePath);
      var memoryStream = new MemoryStream();
      await img.WriteAsync(memoryStream, MagickFormat.Jpg);
      memoryStream.Position = 0;

      return await Task.FromResult(new ImageWithMimeType(mimeType, memoryStream));
    }
    public static async Task GenerateAsync(string inputFile, string outputFile, TimeSpan captureTime)
    {
      if (captureTime < TimeSpan.Zero)
        throw new ArgumentException("The timestamp must not be negative.");

      inputFile = Path.GetFullPath(inputFile);
      outputFile = Path.GetFullPath(outputFile);
      if (!File.Exists(inputFile))
        throw new FileNotFoundException("The input video does not exist.", inputFile);
      if (!Path.GetExtension(outputFile).Equals(".jpg", StringComparison.OrdinalIgnoreCase))
        throw new ArgumentException("Use a .jpg output filename.");
      //if (File.Exists(outputFile) || Directory.Exists(outputFile))
      //  throw new IOException("The destination already exists. Choose a new filename.");

      var directory = Path.GetDirectoryName(outputFile)!;
      if (!Directory.Exists(directory))
        Directory.CreateDirectory(directory);
        //throw new DirectoryNotFoundException("The output directory does not exist.");
      var temporaryFile = Path.Combine(directory, $".thumbnail-{Guid.NewGuid():N}.jpg");

      try
      {
        var thumbnailGenerator = ThumbnailGeneratorBuilder
            .Create()
            .WithFFmpegVideoCapture()
            .UseMagickRenderer()
            .ConfigureRendering(renderingOpts => renderingOpts
                .WithFrameSize(-1, 108)
                .WithTiling(options =>
                {
                  options.Columns = 1;
                  options.Rows = 1;
                })
                .PreserveFrameAspect(true))
            .Build(inputFile);

        var opts = new ThumbGenOptions()
            .WithStartTime(TimeSpan.FromSeconds(5))
            .WithOutputFilename(Path.GetFullPath(temporaryFile));

        await thumbnailGenerator.ExecuteAsync(opts);
        File.Move(temporaryFile, outputFile, overwrite: true);
      }
      finally
      {
        File.Delete(temporaryFile);
      }
    }


    public async static Task<string> GetMimeType(string fileName)
    {
      var provider = new FileExtensionContentTypeProvider();
      const string defaultContentType = "application/octet-stream";

      if (!provider.TryGetContentType(fileName, out string contentType))
      {
        contentType = defaultContentType;
      }

      return contentType;
    }
  }
}
