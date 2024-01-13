using System.Collections.Generic;

namespace elFinder.Net.Core.Models.Options
{
  public class ArchiveOptions
  {
#pragma warning disable IDE1006 // Naming Styles
    public IEnumerable<string> create { get; set; }

    public IEnumerable<string> extract { get; set; }

    public IDictionary<string, string> createext { get; set; }
#pragma warning restore IDE1006 // Naming Styles
  }
}
