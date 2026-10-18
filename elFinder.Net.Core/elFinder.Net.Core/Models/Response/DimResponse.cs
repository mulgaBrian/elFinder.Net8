using ImageMagick;
using System.Drawing;

namespace elFinder.Net.Core.Models.Response
{
  public class DimResponse
  {
    public DimResponse()
    {
    }

    public DimResponse(Size size)
    {
      Dimesions = $"{size.Width}x{size.Height}";
    }

    public string Dimesions { get; set; } = default!;
  }
}
