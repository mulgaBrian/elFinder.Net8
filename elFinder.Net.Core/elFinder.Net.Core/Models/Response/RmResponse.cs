using System.Collections.Generic;

namespace elFinder.Net.Core.Models.Response
{
  public class RmResponse
  {
    public RmResponse()
    {
      removed = [];
    }

#pragma warning disable IDE1006 // Naming Styles
    public List<string> removed { get; }
#pragma warning restore IDE1006 // Naming Styles
  }
}
