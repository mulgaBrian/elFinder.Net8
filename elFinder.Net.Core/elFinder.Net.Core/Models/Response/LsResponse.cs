using System.Collections.Generic;

namespace elFinder.Net.Core.Models.Response
{
  public class LsResponse
  {
    public LsResponse()
    {
      list = [];
    }

#pragma warning disable IDE1006 // Naming Styles
    public Dictionary<string, string> list { get; }
#pragma warning restore IDE1006 // Naming Styles
  }
}
