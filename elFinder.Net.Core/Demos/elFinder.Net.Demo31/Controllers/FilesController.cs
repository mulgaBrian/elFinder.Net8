using elFinder.Net.AspNetCore.Extensions;
using elFinder.Net.AspNetCore.Helper;
using elFinder.Net.Core;
using elFinder.Net.Drivers.FileSystem.Helpers;
using Microsoft.AspNetCore.Mvc;
using System.Runtime.InteropServices;
using ThumbGen;

namespace elFinder.Net.Demo8.Controllers
{
  [Route("/api/files")]
  public class FilesController : Controller
  {
    private readonly IConnector _connector;
    private readonly IDriver _driver;
    private readonly IWebHostEnvironment _env;

    public FilesController(IConnector connector, IDriver driver, IWebHostEnvironment env)
    {
      _env = env;
      _connector = connector;
      _driver = driver;
      //bool isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
      bool isLinux = RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
      bool isOsx = RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
      //string os = "runtimes/win-x64/native/";
      //string here = Path.GetFullPath(".");
      string os;
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
      try { 
        FFmpegBindings.Initialize(os);
      }
      catch(Exception ex) 
      {
        Console.WriteLine(ex.Message);
      }
    }

    [Route("connector")]
    public async Task<IActionResult> Connector()
    {
      try
    {
      await SetupConnectorAsync();
      Core.Models.Command.ConnectorCommand cmd = ConnectorHelper.ParseCommand(Request);
      CancellationTokenSource ccTokenSource = ConnectorHelper.RegisterCcTokenSource(HttpContext);
      Core.Models.Result.ConnectorResult conResult = await _connector.ProcessAsync(cmd, ccTokenSource);
      IActionResult actionResult = conResult.ToActionResult(HttpContext);
      return actionResult;
    }
      catch (Exception ex)
      {
        Console.WriteLine($"FileSystem.Connector {ex.Message}");
        return Json(new { error = ex.Message });
      }
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
        Console.WriteLine(ex.Message + Environment.NewLine);
        throw;
      }
    }

    private async Task SetupConnectorAsync()
    {
      // Volumes registration
      //for (var i = 0; i < 5; i++)
      //{
      var volume = new Volume(_driver,
          PathHelper.GetFullPath(Path.Join(_env.WebRootPath, "images")), //Startup.MapPath("images"),
          Startup.TempPath,
          $"",
          $"/api/files/thumb/",
          thumbnailDirectory: PathHelper.GetFullPath(Path.Join(_env.WebRootPath, ".thumb")))
      {
        StartDirectory = PathHelper.GetFullPath(Path.Join(_env.WebRootPath,"images")),
        Name = "Images",
        MaxUploadConnections = 3,
        VolumeId = "v1_"
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
