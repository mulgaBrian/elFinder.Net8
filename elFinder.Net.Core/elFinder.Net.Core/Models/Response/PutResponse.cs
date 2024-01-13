using System.Collections.Generic;

namespace elFinder.Net.Core.Models.Response
{
  public class PutResponse
  {
    public PutResponse()
    {
      changed = [];
    }

#pragma warning disable IDE1006 // Naming Styles
    public List<object> changed { get; set; }
#pragma warning restore IDE1006 // Naming Styles
  }
}
