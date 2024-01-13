using System.Collections.Generic;

namespace elFinder.Net.Core.Models.Response
{
  public class TreeResponse
  {
    public TreeResponse()
    {
      tree = [];
    }

#pragma warning disable IDE1006 // Naming Styles
    public List<object> tree { get; set; }
#pragma warning restore IDE1006 // Naming Styles
  }
}
