using elFinder.Net.Core.Models.FileInfo;
using elFinder.Net.Core.Models.Options;
using System.Collections.Generic;

namespace elFinder.Net.Core.Models.Response
{
  public class OpenResponse
  {
    private static readonly string[] _empty = [];
    private static readonly DebugResponse _debug = new();

    public OpenResponse()
    {
      files = [];
    }

    public OpenResponse(BaseInfoResponse cwd, ConnectorResponseOptions options,
        IVolume volume)
    {
      files = [];
      this.cwd = cwd;
      this.options = options;
      files.Add(cwd);
      uplMaxFile = volume.MaxUploadFiles;
    }

#pragma warning disable IDE1006 // Naming Styles
    public BaseInfoResponse cwd { get; protected set; }
#pragma warning disable CA1822 // Mark members as static
    public DebugResponse debug => _debug;
    public List<object> files { get; protected set; }
    public ConnectorResponseOptions options { get; protected set; }
    public IEnumerable<string> netDrivers => _empty;
#pragma warning restore CA1822 // Mark members as static
    public int? uplMaxFile { get; protected set; }
    public string uplMaxSize => options.uploadMaxSize;
#pragma warning restore IDE1006 // Naming Styles
  }
}
