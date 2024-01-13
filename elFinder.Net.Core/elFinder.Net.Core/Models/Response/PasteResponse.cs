using System.Collections.Generic;

namespace elFinder.Net.Core.Models.Response
{
  public class PasteResponse
  {
    public PasteResponse()
    {
      added = [];
      removed = [];
    }

#pragma warning disable IDE1006 // Naming Styles
    public List<object> added { get; set; }
    public List<string> removed { get; set; }
#pragma warning restore IDE1006 // Naming Styles
  }
}
