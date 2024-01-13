using elFinder.Net.AspNetCore.Extensions;
using elFinder.Net.AspNetCore.Helper;
using elFinder.Net.Core;
using elFinder.Net.Drivers.FileSystem.Helpers;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;

namespace elFinder.Net.Demo31.Controllers
{
  [Route("api/files")]
  public class FilesController : Controller
  {
    private readonly IConnector _connector;
    private readonly IDriver _driver;

    public FilesController(IConnector connector, IDriver driver)
    {
      _connector = connector;
      _driver = driver;
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
      catch
      {
        //var msg = ex.Message;
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
        StartDirectory = Startup.MapPath("images/articleThumbs"),
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
