using System.Collections.Generic;

namespace elFinder.Net.Core.Models.Response
{
  public class SearchResponse
  {
    public SearchResponse()
    {
      files = [];
    }

#pragma warning disable IDE1006 // Naming Styles
    public List<object> files { get; set; }
#pragma warning restore IDE1006 // Naming Styles

    public void Concat(SearchResponse another)
    {
      files.AddRange(another.files);
    }
  }
}
