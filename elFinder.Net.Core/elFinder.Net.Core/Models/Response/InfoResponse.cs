using System.Collections.Generic;

namespace elFinder.Net.Core.Models.Response
{
  public class InfoResponse
  {
    public InfoResponse()
    {
      files = [];
    }

#pragma warning disable IDE1006 // Naming Styles
    public List<object> files { get; protected set; }
#pragma warning restore IDE1006 // Naming Styles
  }
}
