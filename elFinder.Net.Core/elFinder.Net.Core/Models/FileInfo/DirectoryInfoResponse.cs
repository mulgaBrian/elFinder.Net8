namespace elFinder.Net.Core.Models.FileInfo
{
  public class DirectoryInfoResponse : BaseInfoResponse
  {
#pragma warning disable IDE1006 // Naming Styles
    public string volumeid { get; set; }
    public byte dirs { get; set; }
    public string phash { get; set; }
#pragma warning restore IDE1006 // Naming Styles
  }
}
