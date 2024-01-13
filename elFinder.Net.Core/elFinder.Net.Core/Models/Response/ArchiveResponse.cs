using System.Collections.Generic;

namespace elFinder.Net.Core.Models.Response
{
  public class ArchiveResponse
  {
    public ArchiveResponse()
    {
      added = [];
    }

#pragma warning disable IDE1006 // Naming Styles
    public List<object> added { get; set; }
#pragma warning restore IDE1006 // Naming Styles
  }
}
