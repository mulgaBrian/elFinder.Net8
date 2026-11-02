using ImageMagick;
using Microsoft.AspNetCore.StaticFiles;
using ThumbGen;
using ThumbGen.Builder;

namespace elFinder.Net.Core.Services.Drawing
{
  public class DefaultVideoEditor : IVideoEditor
  {
    public bool CanProcessFile(string fileExtension)
    {
      //return false;
      string ext = fileExtension.ToLower();

      if (ext == ".mp4"
          || ext == ".mpeg"
          || ext == ".mpg"
          || ext == ".mkv"
          || ext == ".m4v"
          || ext == ".avi"
          || ext == ".mov"
          || ext == ".webm"
          || ext == ".wmv")
      {
        return true;
      }
      else { return false; }

    }

    public async Task<ImageWithMimeType> GenerateThumbnailAsync(IFile file, int size,
        bool keepAspectRatio, CancellationToken cancellationToken = default)
    {
      if (CanProcessFile(file.Extension))
      {
        ImageWithMimeType thumb = await CreateThumbnailUsingFfmpeg(file.FullName, size);

        return await Task.FromResult(thumb);
      }
      return null;
    }

    public async Task<ImageWithMimeType> GenerateThumbnailInBackgroundAsync(IFile file, int size,
        bool keepAspectRatio, CancellationToken cancellationToken = default)
    {
      ImageWithMimeType thumb = await CreateThumbnailUsingFfmpeg(file.FullName, size);

      return await Task.FromResult(thumb);
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
      //clean up extra jpg
      File.Delete(outFilePath);

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
      {
        //throw new DirectoryNotFoundException("The output directory does not exist.");
        Directory.CreateDirectory(directory);
      }
      var temporaryFile = Path.Combine(directory, $".thumbnail-{Guid.NewGuid():N}.jpg");

      try
      {

        var thumbnailGenerator = ThumbnailGeneratorBuilder
            .Create()
            .WithFFmpegVideoCapture()
            .UseMagickRenderer()
            .ConfigureRendering(renderingOpts => renderingOpts
                .WithFrameSize(48, 48)
                .WithTiling(options =>
                {
                  options.Columns = 1;
                  options.Rows = 1;
                })
                .PreserveFrameAspect(false))
            .Build(inputFile);

        var opts = new ThumbGenOptions()
            .WithStartTime(captureTime)
            .WithOutputFilename(Path.GetFullPath(temporaryFile));

        await thumbnailGenerator.ExecuteAsync(opts);
        File.Move(temporaryFile, outputFile, overwrite: true);
      }
      catch (Exception ex)
      {
        Console.WriteLine("ThumbGen {0}", ex.Message);
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
