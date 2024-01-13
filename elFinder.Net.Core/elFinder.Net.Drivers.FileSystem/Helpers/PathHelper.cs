using elFinder.Net.Core.Exceptions;
using System.IO;

namespace elFinder.Net.Drivers.FileSystem.Helpers
{
  public static class PathHelper
  {
    private static readonly char[] _separatorChars = new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar };

    public static string GetFullPath(string path)
    {
      var fullPath = Path.GetFullPath(path);
      return fullPath;
    }

    public static string GetFullPathNormalized(string path)
    {
      var fullPath = Path.GetFullPath(path).TrimEnd(_separatorChars);
      return fullPath;
    }

    public static string NormalizePath(string fullPath)
    {
      return fullPath.TrimEnd(_separatorChars);
    }

    public static string SafelyCombine(string fromParent, params string[] paths)
    {
      var finalPath = Path.GetFullPath(Path.Join(paths))
          .Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);

      var parent = Path.GetFullPath(fromParent);

      if (!finalPath.StartsWith(parent.TrimEnd(_separatorChars) + Path.DirectorySeparatorChar))
        throw new PermissionDeniedException("Path must be inside parent");

      return finalPath;
    }
  }
}
