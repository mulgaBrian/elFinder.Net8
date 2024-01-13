namespace elFinder.Net.Core.Models.Response
{
  public class Zipdl1stResponse
  {
    public ZipdlData zipdl { get; set; }
  }

  public class ZipdlData
  {
#pragma warning disable IDE1006 // Naming Styles
    public string file { get; set; }
    public string name { get; set; }
    public string mime { get; set; }
#pragma warning restore IDE1006 // Naming Styles
  }
}
