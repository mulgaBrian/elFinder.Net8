using System;

namespace elFinder.Net.Core.Models.Response
{
  public class GetResponse
  {
    protected Exception exception;

#pragma warning disable IDE1006 // Naming Styles
    public object content { get; set; }
    public string encoding { get; set; }
    public string doconv { get; set; }
#pragma warning restore IDE1006 // Naming Styles

    public Exception GetException()
    {
      return exception;
    }

    public void SetException(Exception ex)
    {
      exception = ex;
    }
  }
}
