using System.Collections.Generic;

namespace elFinder.Net.Core.Models.Response
{
  public class ParentsResponse
  {
    public ParentsResponse()
    {
      tree = [];
    }

#pragma warning disable IDE1006 // Naming Styles
    public List<object> tree { get; protected set; }
#pragma warning restore IDE1006 // Naming Styles
  }
}
