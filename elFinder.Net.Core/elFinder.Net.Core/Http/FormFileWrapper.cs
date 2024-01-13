using Microsoft.Extensions.Primitives;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace elFinder.Net.Core.Http
{
  /// <summary>
  /// Represents a file sent with the HttpRequest.
  /// Reference: https://github.com/dotnet/aspnetcore/blob/main/src/Http/Http.Features/src/IFormFile.cs
  /// </summary>
  public interface IFormFileWrapper
  {
    /// <summary>
    /// Gets the raw Content-Type header of the uploaded file.
    /// </summary>
    string ContentType { get; }

    /// <summary>
    /// Gets the raw Content-Disposition header of the uploaded file.
    /// </summary>
    string ContentDisposition { get; }

    /// <summary>
    /// Gets the header dictionary of the uploaded file.
    /// </summary>
    IDictionary<string, StringValues> Headers { get; }

    /// <summary>
    /// Gets the file length in bytes.
    /// </summary>
    long Length { get; }

    /// <summary>
    /// Gets the form field name from the Content-Disposition header.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the file name from the Content-Disposition header.
    /// </summary>
    string FileName { get; }

    /// <summary>
    /// Opens the request stream for reading the uploaded file.
    /// </summary>
    Stream OpenReadStream();

    /// <summary>
    /// Copies the contents of the uploaded file to the <paramref name="target"/> stream.
    /// </summary>
    /// <param name="target">The stream to copy the file contents to.</param>
    void CopyTo(Stream target);

    /// <summary>
    /// Asynchronously copies the contents of the uploaded file to the <paramref name="target"/> stream.
    /// </summary>
    /// <param name="target">The stream to copy the file contents to.</param>
    /// <param name="cancellationToken"></param>
    Task CopyToAsync(Stream target, CancellationToken cancellationToken = default);
  }

  /// <summary>
  /// Default implementation of <see cref="IFormFileWrapper"/>.
  /// Reference: https://github.com/dotnet/aspnetcore/blob/main/src/Http/Http/src/FormFile.cs
  /// </summary>
  public class FormFileWrapper(Stream baseStream, long length, string name, string fileName) : IFormFileWrapper
  {
    public const int DefaultBufferSize = 80 * 1024;
    protected readonly Stream baseStream = baseStream;

    /// <summary>
    /// Gets the raw Content-Disposition header of the uploaded file.
    /// </summary>
    public string ContentDisposition
    {
      get { return Headers[ContentDisposition]; }
      set { Headers[ContentDisposition] = value; }
    }

    /// <summary>
    /// Gets the raw Content-Type header of the uploaded file.
    /// </summary>
    public string ContentType
    {
      //TODO: check removal of HeaderNames due to deprecation
      get { return Headers[ContentType]; }
      set { Headers[ContentType] = value; }
    }

    /// <summary>
    /// Gets the header dictionary of the uploaded file.
    /// </summary>
    public IDictionary<string, StringValues> Headers { get; set; } = default;

    /// <summary>
    /// Gets the file length in bytes.
    /// </summary>
    public long Length { get; } = length;

    /// <summary>
    /// Gets the name from the Content-Disposition header.
    /// </summary>
    public string Name { get; } = name;

    /// <summary>
    /// Gets the file name from the Content-Disposition header.
    /// </summary>
    public string FileName { get; } = fileName;

    /// <summary>
    /// Opens the request stream for reading the uploaded file.
    /// </summary>
    public Stream OpenReadStream()
    {
      return baseStream;
    }

    /// <summary>
    /// Copies the contents of the uploaded file to the <paramref name="target"/> stream.
    /// </summary>
    /// <param name="target">The stream to copy the file contents to.</param>
    public void CopyTo(Stream target)
    {
      ArgumentNullException.ThrowIfNull(target);

      using (baseStream)
      {
        baseStream.CopyTo(target);
      }
    }

    /// <summary>
    /// Asynchronously copies the contents of the uploaded file to the <paramref name="target"/> stream.
    /// </summary>
    /// <param name="target">The stream to copy the file contents to.</param>
    /// <param name="cancellationToken"></param>
    public async Task CopyToAsync(Stream target, CancellationToken cancellationToken = default)
    {
      ArgumentNullException.ThrowIfNull(target);

      using (baseStream)
      {
        await baseStream.CopyToAsync(target, DefaultBufferSize, cancellationToken);
      }
    }
  }
}
