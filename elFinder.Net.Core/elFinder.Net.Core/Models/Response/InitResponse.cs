using elFinder.Net.Core.Models.FileInfo;
using elFinder.Net.Core.Models.Options;

namespace elFinder.Net.Core.Models.Response
{
  public class InitResponse : OpenResponse
  {
    public InitResponse() : base() { }

    public InitResponse(BaseInfoResponse cwd, ConnectorResponseOptions options, IVolume volume) : base(cwd, options, volume)
    {
    }

#pragma warning disable CA1822 // Mark members as static
#pragma warning disable IDE1006 // Naming Styles
    public string api => ApiValues.Version;
#pragma warning restore IDE1006 // Naming Styles
#pragma warning restore CA1822 // Mark members as static
  }
}
