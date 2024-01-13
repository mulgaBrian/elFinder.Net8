using SixLabors.ImageSharp;

namespace elFinder.Net.Core.Models.Response
{
  public class DimResponse
  {
    public DimResponse()
    {
    }

    public DimResponse(Size size)
    {
      dim = $"{size.Width}x{size.Height}";
    }

#pragma warning disable IDE1006 // Naming Styles
    public string dim { get; set; }
#pragma warning restore IDE1006 // Naming Styles
  }
}
