﻿using System;

namespace elFinder.Net.Core.Models.Response
{
  public class ErrorResponse(Exception ex)
  {
#pragma warning disable IDE1006 // Naming Styles
    public object error { get; set; }
#pragma warning restore IDE1006 // Naming Styles

    protected readonly Exception exception = ex;
    public Exception GetException()
    {
      return exception;
    }

    public static class Factory
    {
      public static ErrorResponse ConnectionAborted(Exception ex)
      {
        return new ErrorResponse(ex)
        {
          error = ErrorResponse.ConnectionAborted
        };
      }

      public static ErrorResponse AccessDenied(Exception ex)
      {
        return new ErrorResponse(ex)
        {
          error = ErrorResponse.AccessDenied
        };
      }

      public static ErrorResponse FileNotFound(Exception ex)
      {
        return new ErrorResponse(ex)
        {
          error = ErrorResponse.FileNotFound
        };
      }

      public static ErrorResponse CommandParams(Exception ex, string cmd)
      {
        return new ErrorResponse(ex)
        {
          error = new[] { ErrorResponse.CommandParams, cmd }
        };
      }

      public static ErrorResponse FolderNotFound(Exception ex)
      {
        return new ErrorResponse(ex)
        {
          error = ErrorResponse.FolderNotFound
        };
      }

      public static ErrorResponse UploadFile(Exception ex, string fileName)
      {
        return new ErrorResponse(ex)
        {
          error = new[] { ErrorResponse.UploadFile, fileName }
        };
      }

      public static ErrorResponse UploadMime(Exception ex)
      {
        return new ErrorResponse(ex)
        {
          error = ErrorResponse.UploadMime
        };
      }

      public static ErrorResponse Unknown(Exception ex)
      {
        return new ErrorResponse(ex)
        {
          error = ErrorResponse.Unknown
        };
      }
    }

    #region Constants
    public const string UnknownCommand = "errUnknownCmd";
    public const string ArchiveType = "errArcType";
    public const string Exists = "errExists";
    public const string CommandRequired = "errCmdReq";
    public const string MakeFile = "errMkfile";
    public const string AccessDenied = "errAccess";
    public const string FileNotFound = "errFileNotFound";
    public const string FolderNotFound = "errFolderNotFound";
    public const string CommandParams = "errCmdParams";
    public const string ConnectionAborted = "errAbort";
    public const string Unknown = "errUnknown";
    public const string PermissionDenied = "errPerm";
    public const string UploadFileSize = "errUploadFileSize";
    public const string CommandNoSupport = "errCmdNoSupport";
    public const string NotFile = "errNotFile";
    public const string UploadFile = "errUploadFile";
    public const string InvalidFileName = "errInvName";
    public const string InvalidDirName = "errInvDirname";
    public const string UploadMime = "errUploadMime";
    #endregion
  }
}
