using System.Collections.Generic;

namespace elFinder.Net.Core.Models.Response
{
  public class MkdirResponse : MkfileResponse
  {
    public MkdirResponse() : base()
    {
      hashes = [];
    }

#pragma warning disable IDE1006 // Naming Styles
    public Dictionary<string, string> hashes { get; set; }
#pragma warning restore IDE1006 // Naming Styles
  }
}
