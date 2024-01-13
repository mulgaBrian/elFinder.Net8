using System;
using System.Collections.Generic;
using System.Linq;

namespace elFinder.Net.Core.Helpers
{
  public static class DownloadHelper
  {
    public static string GetZipDownloadName(IEnumerable<PathInfo> paths,
        string defaultName = DownloadConsts.ZipDownloadDefaultName)
    {
      if (paths?.Any() != true) throw new ArgumentNullException(nameof(paths));

      PathInfo firstPath = paths.First();

      if (paths.Count() == 1) return firstPath.IsRoot ? firstPath.Volume.Name : firstPath.FileSystem.Name;

      IDirectory parent = firstPath.FileSystem.Parent;

      var isParentRoot = parent == null || firstPath.Volume.IsRoot(parent);

      return (isParentRoot ? firstPath.Volume.Name : parent?.Name) ?? defaultName;
    }
  }
}
