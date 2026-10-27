using elFinder.Net.AspNetCore.Extensions;
using elFinder.Net.AspNetCore.Helper;
using elFinder.Net.Core;
using elFinder.Net.Drivers.FileSystem.Helpers;
using Microsoft.AspNetCore.Mvc;
using System.Runtime.InteropServices;
using ThumbGen;

namespace elFinder.Net.Demo31.Controllers
{
  [Route("api/files")]
  public class FilesController : Controller
  {
    private readonly IConnector _connector;
    private readonly IDriver _driver;

    public FilesController(IConnector connector, IDriver driver, IWebHostEnvironment env)
    {
      _connector = connector;
      _driver = driver;
      bool isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
      bool isLinux = RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
      bool isOsx = RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
      //string os = "runtimes/win-x64/native/";
      string here = Path.GetFullPath(".");
      string os = string.Empty;
      if (env.IsDevelopment())
      {
        os = Path.GetFullPath(".\\bin\\Debug\\net10.0\\runtimes\\win-x64\\native\\");
      }
      else
      {
        os = Path.GetFullPath(".\\runtimes\\win-x64\\native\\");
      }
      if (isLinux) { os = "/usr/lib/x86_64-linux-gnu"; }
      if (isOsx) { os = "/usr/local/lib/"; } // /opt/homebrew/lib/
      FFmpegBindings.Initialize(os);
    }

    [Route("connector")]
    public async Task<IActionResult> Connector()
    {
      await SetupConnectorAsync();
      Core.Models.Command.ConnectorCommand cmd = ConnectorHelper.ParseCommand(Request);
      CancellationTokenSource ccTokenSource = ConnectorHelper.RegisterCcTokenSource(HttpContext);
      Core.Models.Result.ConnectorResult conResult = await _connector.ProcessAsync(cmd, ccTokenSource);
      IActionResult actionResult = conResult.ToActionResult(HttpContext);
      return actionResult;
    }

    [Route("thumb/{target}")]
    public async Task<IActionResult> Thumb(string target)
    {
      try
      {
        await SetupConnectorAsync();
        Core.Services.Drawing.ImageWithMimeType thumb = await _connector.GetThumbAsync(target, HttpContext.RequestAborted);
        IActionResult actionResult = ConnectorHelper.GetThumbResult(thumb);
        return actionResult;
      }
      catch(Exception ex)
      {
        //var msg = ex.Message;
        Console.Write(ex.Message + Environment.NewLine);
        throw;
      }
    }

    private async Task SetupConnectorAsync()
    {
      // Volumes registration
      //for (var i = 0; i < 5; i++)
      //{
      var volume = new Volume(_driver,
          Startup.MapPath("images"),
          Startup.TempPath,
          "images",
          "/api/files/thumb/",
          thumbnailDirectory: PathHelper.GetFullPath("./thumb"))
      {
        StartDirectory = Startup.MapPath("images"),
        Name = "Images",
        MaxUploadConnections = 3
      };

      _connector.AddVolume(volume);
      await volume.Driver.SetupVolumeAsync(volume);
      //}

      // Events
      _driver.OnBeforeMove.Add((fileSystem, newDest, isOverwrite) =>
      {
        Console.WriteLine("Move: " + fileSystem.FullName);
        Console.WriteLine("To: " + newDest);
        return Task.CompletedTask;
      });
    }
  }
}
