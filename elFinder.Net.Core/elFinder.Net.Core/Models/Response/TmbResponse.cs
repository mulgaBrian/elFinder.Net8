using System.Collections.Generic;

namespace elFinder.Net.Core.Models.Response
{
  public class TmbResponse
  {
    public TmbResponse()
    {
      images = [];
    }

#pragma warning disable IDE1006 // Naming Styles
    public Dictionary<string, string> images { get; }
#pragma warning restore IDE1006 // Naming Styles
  }
}
