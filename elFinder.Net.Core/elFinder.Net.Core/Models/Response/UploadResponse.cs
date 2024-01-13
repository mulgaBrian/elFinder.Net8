using System.Collections.Generic;

namespace elFinder.Net.Core.Models.Response
{
  public class UploadResponse
  {
    protected List<ErrorResponse> warningDetails;

    public UploadResponse()
    {
      added = [];
      _warning = [];
      warningDetails = [];
    }

#pragma warning disable IDE1006 // Naming Styles
    public List<object> added { get; set; }

    private List<object> _warning;
    public List<object> warning => _warning.Count > 0 ? _warning : null;

    #region Chunked upload
    public string _chunkmerged { get; set; }
    public string _name { get; set; }
    #endregion
#pragma warning restore IDE1006 // Naming Styles

    public List<object> GetWarnings()
    {
      return _warning;
    }

    public List<ErrorResponse> GetWarningDetails()
    {
      return warningDetails;
    }
  }
}
