namespace elFinder.Net.Core.Models.Response
{
  public class DebugResponse
  {
    private static string _connectorName = typeof(DebugResponse).Assembly.GetName().Name;

#pragma warning disable CA1822 // Mark members as static
#pragma warning disable IDE1006 // Naming Styles
    public string connector => _connectorName;
#pragma warning restore IDE1006 // Naming Styles
#pragma warning restore CA1822 // Mark members as static
  }
}
