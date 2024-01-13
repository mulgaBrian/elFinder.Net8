﻿using elFinder.Net.Core.Http;
using elFinder.Net.Core.Models.Command;
using elFinder.Net.Core.Services.Drawing;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;
using HttpMethod = elFinder.Net.Core.HttpMethod;

namespace elFinder.Net.AspNetCore.Helper
{
  public static class ConnectorHelper
  {
    public static CancellationTokenSource RegisterCcTokenSource(HttpContext httpContext)
    {
      var tokenSource = new CancellationTokenSource();

      httpContext.RequestAborted.Register(ccTokenSrc =>
      {
        (ccTokenSrc as CancellationTokenSource).Cancel();
      }, tokenSource);

      return tokenSource;
    }

    public static ConnectorCommand ParseCommand(HttpRequest request)
    {
      ArgumentNullException.ThrowIfNull(request);

      HttpMethod method = HttpMethods.IsGet(request.Method) ? HttpMethod.Get :
          HttpMethods.IsPost(request.Method) ? HttpMethod.Post : default;

      var cmd = new ConnectorCommand
      {
        Method = method,
        RequestHeaders = request.Headers.ToDictionary(o => o.Key, o => o.Value)
      };

      switch (method)
      {
        case HttpMethod.Get:
          {
            cmd.Query = request.Query.ToDictionary(k => k.Key, v => v.Value);
          }
          break;
        case HttpMethod.Post:
          {
            var form = request.Form.ToDictionary(k => k.Key, v => v.Value);
            IFormFileWrapper[] files = request.Form.Files.Select(file =>
                  {
                    Stream baseStream = file.OpenReadStream();
                    var formFile = new FormFileWrapper(baseStream, file.Length, file.Name, file.FileName)
                    {
                      Headers = file.Headers
                    };
                    return formFile as IFormFileWrapper;
                  }).ToArray();
            cmd.Form = form;
            cmd.Files = files;
          }
          break;
      }

      cmd.Cmd = cmd.Args.GetValueOrDefault(ConnectorCommand.Param_Cmd, default);


      if (cmd.Args.TryGetValue(ConnectorCommand.Param_ReqId, out StringValues reqId)
    || request.Headers.TryGetValue(ConnectorCommand.Header_ReqId, out reqId))
        cmd.ReqId = reqId;

      return cmd;
    }

    public static IActionResult GetThumbResult(ImageWithMimeType thumb)
    {
      return thumb != null ?
          new FileStreamResult(thumb.ImageStream, thumb.MimeType) as IActionResult :
          new EmptyResult();
    }
  }
}
