using elFinder.Net.Core.Models.Options;

namespace elFinder.Net.Core.Models.FileInfo
{
  public class RootInfoResponse : DirectoryInfoResponse
  {
#pragma warning disable IDE1006 // Naming Styles
    public byte isroot { get; set; }
    public ConnectorResponseOptions options { get; set; }
#pragma warning restore IDE1006 // Naming Styles
  }
}
