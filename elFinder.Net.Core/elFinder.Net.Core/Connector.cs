using elFinder.Net.Core.Exceptions;
using elFinder.Net.Core.Extensions;
using elFinder.Net.Core.Models.Command;
using elFinder.Net.Core.Models.Response;
using elFinder.Net.Core.Models.Result;
using elFinder.Net.Core.Services;
using elFinder.Net.Core.Services.Drawing;
using System.Net;

namespace elFinder.Net.Core
{
  public interface IConnector
  {
    ConnectorOptions Options { get; }
    PluginManager PluginManager { get; }
    IList<IVolume> Volumes { get; set; }
    Task<ConnectorResult> ProcessAsync(ConnectorCommand cmd, CancellationTokenSource cancellationTokenSource = default!);
    Task<PathInfo> ParsePathAsync(string target,
        bool createIfNotExists = true, bool fileByDefault = true, CancellationToken cancellationToken = default);
    Task<IEnumerable<PathInfo>> ParsePathsAsync(IEnumerable<string> targets, CancellationToken cancellationToken = default);
    Task<ImageWithMimeType> GetThumbAsync(string target, CancellationToken cancellationToken = default);
    string AddVolume(IVolume volume);
    Task AbortAsync(RequestCommand cmd, CancellationToken cancellationToken = default);
  }

  public class Connector(ConnectorOptions options, PluginManager pluginManager,
      IPathParser pathParser, IConnectorManager connectorManager) : IConnector
  {
    protected readonly ConnectorOptions options = options;
    protected readonly PluginManager pluginManager = pluginManager;
    protected readonly IPathParser pathParser = pathParser;
    protected readonly IConnectorManager connectorManager = connectorManager;

    public virtual ConnectorOptions Options => options;
    public virtual IList<IVolume> Volumes { get; set; } = new List<IVolume>();
    public virtual PluginManager PluginManager => pluginManager;

    public virtual async Task<ConnectorResult> ProcessAsync(ConnectorCommand cmd, CancellationTokenSource cancellationTokenSource = default!)
    {
      cancellationTokenSource ??= new CancellationTokenSource();
      CancellationToken cancellationToken = cancellationTokenSource.Token;
      cancellationToken.ThrowIfCancellationRequested();

      ArgumentNullException.ThrowIfNull(cmd);
      ArgumentNullException.ThrowIfNull(nameof(cmd.Args));

      var hasReqId = !string.IsNullOrEmpty(cmd.ReqId);

      if (hasReqId)
        connectorManager.AddCancellationTokenSource(new RequestCommand(cmd), cancellationTokenSource);

      var cookies = new Dictionary<string, string>();
      ConnectorResult connResult = await ProcessCoreAsync(cmd, cookies, cancellationToken);
      connResult.Cookies = cookies;

      if (hasReqId)
        connectorManager.ReleaseRequest(cmd.ReqId);

      return connResult;
    }

    protected virtual async Task<ConnectorResult> ProcessCoreAsync(ConnectorCommand cmd, Dictionary<string, string> cookies,
        CancellationToken cancellationToken = default)
    {
      cancellationToken.ThrowIfCancellationRequested();

      ArgumentNullException.ThrowIfNull(cmd);
      ArgumentNullException.ThrowIfNull(nameof(cmd.Args));

      ErrorResponse errResp;
      HttpStatusCode errSttCode = HttpStatusCode.OK;

      try
      {
        if (options.EnabledCommands?.Contains(cmd.Cmd) == false) throw new CommandNoSupportException();

        IReadOnlyDictionary<string, Microsoft.Extensions.Primitives.StringValues> args = cmd.Args;
        cmd.Cmd = args.GetValueOrDefault(ConnectorCommand.Param_Cmd)!;

        if (string.IsNullOrEmpty(cmd.Cmd))
          throw new CommandRequiredException();

        switch (cmd.Cmd)
        {
          case ConnectorCommand.Cmd_Abort:
            {
              var abortCmd = new AbortCommand
              {
                Id = args.GetValueOrDefault(ConnectorCommand.Param_Id)!
              };

              (bool success, RequestCommand reqCmd) = await connectorManager.AbortAsync(abortCmd.Id, cancellationToken: cancellationToken);

              if (success)
              {
                await AbortAsync(reqCmd, cancellationToken: cancellationToken);
              }

              //return ConnectorResult.NoContent(new AbortResponse { success = success });
              return ConnectorResult.Success(new AbortResponse
              {
                success = success
              });
            }
          case ConnectorCommand.Cmd_Info:
            {
              var infoCmd = new InfoCommand
              {
                Targets = args.GetValueOrDefault(ConnectorCommand.Param_Targets)
              };
              infoCmd.TargetPaths = await ParsePathsAsync(infoCmd.Targets, cancellationToken: cancellationToken);
              cmd.CmdObject = infoCmd;

              IVolume[] distinctVolumes = infoCmd.TargetPaths.Select(p => p.Volume).Distinct().ToArray();
              if (distinctVolumes.Length != 1) throw new CommandParamsException(ConnectorCommand.Cmd_Info);

              InfoResponse infoResp = await distinctVolumes[0].Driver.InfoAsync(infoCmd, cancellationToken);

              return ConnectorResult.Success(infoResp);
            }
          case ConnectorCommand.Cmd_Open:
            {
              var openCmd = new OpenCommand
              {
                Target = args.GetValueOrDefault(ConnectorCommand.Param_Target)!
              };
              openCmd.TargetPath = await ParsePathAsync(openCmd.Target, createIfNotExists: false, cancellationToken: cancellationToken);
              openCmd.Mimes = options.MimeDetect == MimeDetectOption.Internal
                  ? args.GetValueOrDefault(ConnectorCommand.Param_MimesArr)
                  : default;
              if (byte.TryParse(args.GetValueOrDefault(ConnectorCommand.Param_Init), out var init))
                openCmd.Init = init;
              if (byte.TryParse(args.GetValueOrDefault(ConnectorCommand.Param_Tree), out var tree))
                openCmd.Tree = tree;
              IVolume openVolume = openCmd.TargetPath?.Volume ?? Volumes.FirstOrDefault()!;
              openCmd.Volume = openVolume;
              cmd.CmdObject = openCmd;

              if (openVolume == null) throw new FileNotFoundException();

              OpenResponse openResp = await openVolume.Driver.OpenAsync(openCmd, cancellationToken);

              if (openCmd.Tree == 1)
              {
                for (var i = 0; i < Volumes.Count; i++)
                {
                  IVolume volume = Volumes[i];

                  if (openCmd.TargetPath!.IsRoot && volume == openVolume) continue;

                  IDirectory rootVolumeDir = volume.Driver.CreateDirectory(volume.RootDirectory, volume);
                  var hash = rootVolumeDir.GetHash(volume, pathParser);
                  Models.FileInfo.BaseInfoResponse dirInfo = await rootVolumeDir.ToFileInfoAsync(hash, null!, volume, Options, cancellationToken: cancellationToken);
                  openResp.files.Insert(i, dirInfo);
                }
              }

              return ConnectorResult.Success(openResp);
            }
          case ConnectorCommand.Cmd_Archive:
            {
              var archiveCmd = new ArchiveCommand
              {
                Target = args.GetValueOrDefault(ConnectorCommand.Param_Target)!
              };
              archiveCmd.TargetPath = await ParsePathAsync(archiveCmd.Target, cancellationToken: cancellationToken);
              archiveCmd.Name = args.GetValueOrDefault(ConnectorCommand.Param_Name)!;
              archiveCmd.Targets = args.GetValueOrDefault(ConnectorCommand.Param_Targets);
              archiveCmd.TargetPaths = await ParsePathsAsync(archiveCmd.Targets, cancellationToken: cancellationToken);
              archiveCmd.Type = args.GetValueOrDefault(ConnectorCommand.Param_Type)!;
              cmd.CmdObject = archiveCmd;
              IVolume volume = archiveCmd.TargetPath.Volume;

              if (archiveCmd.Targets.Count == 0 || archiveCmd.TargetPath?.IsDirectory != true)
                throw new CommandParamsException(ConnectorCommand.Cmd_Archive);

              IVolume[] distinctVolumes = archiveCmd.TargetPaths.Select(p => p.Volume).Distinct().ToArray();
              if (distinctVolumes.Length != 1 || distinctVolumes[0].VolumeId != volume.VolumeId)
                throw new CommandParamsException(ConnectorCommand.Cmd_Archive);

              ArchiveResponse archiveResp = await volume.Driver.ArchiveAsync(archiveCmd, cancellationToken);
              return ConnectorResult.Success(archiveResp);
            }
          case ConnectorCommand.Cmd_Extract:
            {
              var extractCmd = new ExtractCommand
              {
                Target = args.GetValueOrDefault(ConnectorCommand.Param_Target)!
              };
              extractCmd.TargetPath = await ParsePathAsync(extractCmd.Target, cancellationToken: cancellationToken);
              if (byte.TryParse(args.GetValueOrDefault(ConnectorCommand.Param_MakeDir), out var makeDir))
                extractCmd.MakeDir = makeDir;
              cmd.CmdObject = extractCmd;

              if (extractCmd.TargetPath.IsDirectory)
                throw new NotFileException();

              ExtractResponse extractResp = await extractCmd.TargetPath.Volume.Driver.ExtractAsync(extractCmd, cancellationToken);
              return ConnectorResult.Success(extractResp);
            }
          case ConnectorCommand.Cmd_Mkdir:
            {
              var mkdirCmd = new MkdirCommand
              {
                Target = args.GetValueOrDefault(ConnectorCommand.Param_Target)!
              };
              mkdirCmd.TargetPath = await ParsePathAsync(mkdirCmd.Target, cancellationToken: cancellationToken);
              mkdirCmd.Name = args.GetValueOrDefault(ConnectorCommand.Param_Name)!;
              mkdirCmd.Dirs = args.GetValueOrDefault(ConnectorCommand.Param_Dirs);
              cmd.CmdObject = mkdirCmd;

              MkdirResponse mkdirResp = await mkdirCmd.TargetPath.Volume.Driver.MkdirAsync(mkdirCmd, cancellationToken);
              return ConnectorResult.Success(mkdirResp);
            }
          case ConnectorCommand.Cmd_Mkfile:
            {
              var mkfileCmd = new MkfileCommand
              {
                Target = args.GetValueOrDefault(ConnectorCommand.Param_Target)!
              };
              mkfileCmd.TargetPath = await ParsePathAsync(mkfileCmd.Target, cancellationToken: cancellationToken);
              mkfileCmd.Name = args.GetValueOrDefault(ConnectorCommand.Param_Name)!;
              cmd.CmdObject = mkfileCmd;

              MkfileResponse mkFileResp = await mkfileCmd.TargetPath.Volume.Driver.MkfileAsync(mkfileCmd, cancellationToken);
              return ConnectorResult.Success(mkFileResp);
            }
          case ConnectorCommand.Cmd_Parents:
            {
              var parentsCmd = new ParentsCommand
              {
                Target = args.GetValueOrDefault(ConnectorCommand.Param_Target)!
              };
              parentsCmd.TargetPath = await ParsePathAsync(parentsCmd.Target, cancellationToken: cancellationToken);
              cmd.CmdObject = parentsCmd;

              ParentsResponse parentsResp = await parentsCmd.TargetPath.Volume.Driver.ParentsAsync(parentsCmd, cancellationToken);
              return ConnectorResult.Success(parentsResp);
            }
          case ConnectorCommand.Cmd_Tmb:
            {
              var tmbCmd = new TmbCommand
              {
                Targets = args.GetValueOrDefault(ConnectorCommand.Param_Targets)
              };
              tmbCmd.TargetPaths = await ParsePathsAsync(tmbCmd.Targets, cancellationToken: cancellationToken);
              cmd.CmdObject = tmbCmd;

              IVolume[] distinctVolumes = tmbCmd.TargetPaths.Select(p => p.Volume).Distinct().ToArray();
              if (distinctVolumes.Length != 1) throw new CommandParamsException(ConnectorCommand.Cmd_Tmb);

              TmbResponse tmbResp = await distinctVolumes[0].Driver.TmbAsync(tmbCmd, cancellationToken);
              return ConnectorResult.Success(tmbResp);
            }
          case ConnectorCommand.Cmd_Dim:
            {
              var dimCmd = new DimCommand
              {
                Target = args.GetValueOrDefault(ConnectorCommand.Param_Target)!
              };
              dimCmd.TargetPath = await ParsePathAsync(dimCmd.Target, cancellationToken: cancellationToken);
              cmd.CmdObject = dimCmd;

              DimResponse dimResp = await dimCmd.TargetPath.Volume.Driver.DimAsync(dimCmd, cancellationToken);
              return ConnectorResult.Success(dimResp);
            }
          case ConnectorCommand.Cmd_Duplicate:
            {
              var dupCmd = new DuplicateCommand
              {
                Targets = args.GetValueOrDefault(ConnectorCommand.Param_Targets)
              };
              dupCmd.TargetPaths = await ParsePathsAsync(dupCmd.Targets, cancellationToken);
              cmd.CmdObject = dupCmd;

              IVolume[] distinctVolumes = dupCmd.TargetPaths.Select(p => p.Volume).Distinct().ToArray();
              if (distinctVolumes.Length != 1) throw new CommandParamsException(ConnectorCommand.Cmd_Duplicate);

              DuplicateResponse dupResp = await distinctVolumes[0].Driver.DuplicateAsync(dupCmd, cancellationToken);
              return ConnectorResult.Success(dupResp);
            }
          case ConnectorCommand.Cmd_Paste:
            {
              var pasteCmd = new PasteCommand
              {
                Targets = args.GetValueOrDefault(ConnectorCommand.Param_Targets)
              };
              pasteCmd.TargetPaths = await ParsePathsAsync(pasteCmd.Targets, cancellationToken: cancellationToken);
              pasteCmd.Suffix = args.GetValueOrDefault(ConnectorCommand.Param_Suffix)!;
              pasteCmd.Renames = args.GetValueOrDefault(ConnectorCommand.Param_Renames);
              pasteCmd.Dst = args.GetValueOrDefault(ConnectorCommand.Param_Dst)!;
              pasteCmd.DstPath = await ParsePathAsync(pasteCmd.Dst, cancellationToken: cancellationToken);
              if (byte.TryParse(args.GetValueOrDefault(ConnectorCommand.Param_Cut), out var cut))
                pasteCmd.Cut = cut;
              pasteCmd.Hashes = args.Where(kvp => kvp.Key.StartsWith(ConnectorCommand.Param_Hashes_Start))
                  .ToDictionary(o => o.Key, o => (string)o.Value!)!;
              cmd.CmdObject = pasteCmd;

              IVolume[] distinctVolumes = pasteCmd.TargetPaths.Select(p => p.Volume).Distinct().ToArray();
              if (distinctVolumes.Length != 1) throw new CommandParamsException(ConnectorCommand.Cmd_Paste);

              PasteResponse pasteResp = await distinctVolumes[0].Driver.PasteAsync(pasteCmd, cancellationToken);
              return ConnectorResult.Success(pasteResp);
            }
          case ConnectorCommand.Cmd_Get:
            {
              var getCmd = new GetCommand
              {
                Target = args.GetValueOrDefault(ConnectorCommand.Param_Target)!
              };
              getCmd.TargetPath = await ParsePathAsync(getCmd.Target, cancellationToken: cancellationToken);
              getCmd.Current = args.GetValueOrDefault(ConnectorCommand.Param_Current)!;
              getCmd.CurrentPath = await ParsePathAsync(getCmd.Current, cancellationToken: cancellationToken);
              getCmd.Conv = args.GetValueOrDefault(ConnectorCommand.Param_Conv)!;
              cmd.CmdObject = getCmd;
              PathInfo targetPath = getCmd.TargetPath;

              if (targetPath.IsDirectory)
                throw new NotFileException();

              GetResponse getResp = await targetPath.Volume.Driver.GetAsync(getCmd, cancellationToken);
              return ConnectorResult.Success(getResp);
            }
          case ConnectorCommand.Cmd_File:
            {
              var fileCmd = new FileCommand
              {
                Target = args.GetValueOrDefault(ConnectorCommand.Param_Target)!
              };
              fileCmd.TargetPath = await ParsePathAsync(fileCmd.Target, cancellationToken: cancellationToken);
              if (byte.TryParse(args.GetValueOrDefault(ConnectorCommand.Param_Download), out var download))
                fileCmd.Download = download;
              fileCmd.ReqId = args.GetValueOrDefault(ConnectorCommand.Param_ReqId)!;
              fileCmd.CPath = args.GetValueOrDefault(ConnectorCommand.Param_CPath)!;
              cmd.CmdObject = fileCmd;
              PathInfo targetPath = fileCmd.TargetPath;
              IVolume volume = targetPath.Volume;

              if (!string.IsNullOrEmpty(fileCmd.CPath))
              {
                // API >= 2.1.39
                cookies[ConnectorResult.Cookie_Elfdl + fileCmd.ReqId] = "1";
              }

              if (targetPath.IsDirectory)
                throw new NotFileException();

              if (!await targetPath.File.ExistsAsync)
                throw new FileNotFoundException();

              FileResponse fileResp = await fileCmd.TargetPath.Volume.Driver.FileAsync(fileCmd, cancellationToken);
              return ConnectorResult.File(fileResp);
            }
          case ConnectorCommand.Cmd_Rm:
            {
              var rmCmd = new RmCommand
              {
                Targets = args.GetValueOrDefault(ConnectorCommand.Param_Targets)
              };
              rmCmd.TargetPaths = await ParsePathsAsync(rmCmd.Targets, cancellationToken: cancellationToken);
              cmd.CmdObject = rmCmd;

              IVolume[] distinctVolumes = rmCmd.TargetPaths.Select(p => p.Volume).Distinct().ToArray();
              if (distinctVolumes.Length != 1) throw new CommandParamsException(ConnectorCommand.Cmd_Rm);

              // Command 'rm' with parent and children items, it means "Empty the parent folder"
              var parents = rmCmd.TargetPaths.Select(path =>
                  path.FileSystem.Parent?.FullName).Where(name => !string.IsNullOrEmpty(name)).Distinct().ToArray();
              rmCmd.TargetPaths = rmCmd.TargetPaths.Where(o => !parents.Contains(o.FileSystem.FullName)).ToArray();

              RmResponse rmResp = await distinctVolumes[0].Driver.RmAsync(rmCmd, cancellationToken);
              return ConnectorResult.Success(rmResp);
            }
          case ConnectorCommand.Cmd_Ls:
            {
              var lsCmd = new LsCommand
              {
                Target = args.GetValueOrDefault(ConnectorCommand.Param_Target)!
              };
              lsCmd.TargetPath = await ParsePathAsync(lsCmd.Target, cancellationToken: cancellationToken);
              lsCmd.Intersect = args.GetValueOrDefault(ConnectorCommand.Param_Intersect);
              lsCmd.Mimes = options.MimeDetect == MimeDetectOption.Internal
                  ? args.GetValueOrDefault(ConnectorCommand.Param_MimesArr)
                  : default;
              cmd.CmdObject = lsCmd;

              LsResponse lsResp = await lsCmd.TargetPath.Volume.Driver.LsAsync(lsCmd, cancellationToken);
              return ConnectorResult.Success(lsResp);
            }
          case ConnectorCommand.Cmd_Put:
            {
              var putCmd = new PutCommand
              {
                Target = args.GetValueOrDefault(ConnectorCommand.Param_Target)!
              };
              putCmd.TargetPath = await ParsePathAsync(putCmd.Target, cancellationToken: cancellationToken);
              putCmd.Content = args.GetValueOrDefault(ConnectorCommand.Param_Content)!;
              putCmd.Encoding = args.GetValueOrDefault(ConnectorCommand.Param_Encoding)!;
              cmd.CmdObject = putCmd;
              PathInfo targetPath = putCmd.TargetPath;

              if (targetPath.IsDirectory)
                throw new NotFileException();

              if (putCmd.Encoding == "hash")
              {
                putCmd.ContentPath = await ParsePathAsync(putCmd.Content, cancellationToken: cancellationToken);
                if (putCmd.ContentPath.IsDirectory)
                  throw new NotFileException();
              }

              PutResponse putResp = await putCmd.TargetPath.Volume.Driver.PutAsync(putCmd, cancellationToken);
              return ConnectorResult.Success(putResp);
            }
          case ConnectorCommand.Cmd_Size:
            {
              var sizeCmd = new SizeCommand
              {
                Targets = args.GetValueOrDefault(ConnectorCommand.Param_Targets)
              };
              sizeCmd.TargetPaths = await ParsePathsAsync(sizeCmd.Targets, cancellationToken: cancellationToken);
              cmd.CmdObject = sizeCmd;

              IVolume[] distinctVolumes = sizeCmd.TargetPaths.Select(p => p.Volume).Distinct().ToArray();
              if (distinctVolumes.Length != 1) throw new CommandParamsException(ConnectorCommand.Cmd_Size);

              SizeResponse sizeResp = await distinctVolumes[0].Driver.SizeAsync(sizeCmd, cancellationToken);
              return ConnectorResult.Success(sizeResp);
            }
          case ConnectorCommand.Cmd_Rename:
            {
              var renameCmd = new RenameCommand
              {
                Target = args.GetValueOrDefault(ConnectorCommand.Param_Target)!
              };
              PathInfo targetPath = await ParsePathAsync(renameCmd.Target, cancellationToken: cancellationToken);
              IVolume volume = targetPath.Volume;
              renameCmd.TargetPath = targetPath;
              renameCmd.Name = args.GetValueOrDefault(ConnectorCommand.Param_Name)!;
              cmd.CmdObject = renameCmd;

              RenameResponse renameResp = await volume.Driver.RenameAsync(renameCmd, cancellationToken);
              return ConnectorResult.Success(renameResp);
            }
          case ConnectorCommand.Cmd_Tree:
            {
              var treeCmd = new TreeCommand
              {
                Target = args.GetValueOrDefault(ConnectorCommand.Param_Target)!
              };
              treeCmd.TargetPath = await ParsePathAsync(treeCmd.Target, cancellationToken: cancellationToken);
              cmd.CmdObject = treeCmd;

              TreeResponse treeResp = await treeCmd.TargetPath.Volume.Driver.TreeAsync(treeCmd, cancellationToken);
              return ConnectorResult.Success(treeResp);
            }
          case ConnectorCommand.Cmd_Resize:
            {
              var resizeCmd = new ResizeCommand
              {
                Target = args.GetValueOrDefault(ConnectorCommand.Param_Target)!
              };
              resizeCmd.TargetPath = await ParsePathAsync(resizeCmd.Target, cancellationToken: cancellationToken);
              resizeCmd.Mode = args.GetValueOrDefault(ConnectorCommand.Param_Mode)!;
              if (int.TryParse(args.GetValueOrDefault(ConnectorCommand.Param_Quality), out var quality))
                resizeCmd.Quality = quality;

              switch (resizeCmd.Mode)
              {
                case ResizeCommand.Mode_Resize:
                  {
                    if (int.TryParse(args.GetValueOrDefault(ConnectorCommand.Param_Width), out var width))
                      resizeCmd.Width = width;
                    if (int.TryParse(args.GetValueOrDefault(ConnectorCommand.Param_Height), out var height))
                      resizeCmd.Height = height;
                  }
                  break;
                case ResizeCommand.Mode_Crop:
                  {
                    if (int.TryParse(args.GetValueOrDefault(ConnectorCommand.Param_Width), out var width))
                      resizeCmd.Width = width;
                    if (int.TryParse(args.GetValueOrDefault(ConnectorCommand.Param_Height), out var height))
                      resizeCmd.Height = height;
                    if (int.TryParse(args.GetValueOrDefault(ConnectorCommand.Param_X), out var x))
                      resizeCmd.X = x;
                    if (int.TryParse(args.GetValueOrDefault(ConnectorCommand.Param_Y), out var y))
                      resizeCmd.Y = y;
                  }
                  break;
                case ResizeCommand.Mode_Rotate:
                  {
                    if (int.TryParse(args.GetValueOrDefault(ConnectorCommand.Param_Degree), out var degree))
                      resizeCmd.Degree = degree;
                    resizeCmd.Background = args.GetValueOrDefault(ConnectorCommand.Param_Bg)!;
                  }
                  break;
                default:
                  throw new UnknownCommandException();
              }
              cmd.CmdObject = resizeCmd;
              PathInfo targetPath = resizeCmd.TargetPath;

              ResizeResponse resizeResp = await resizeCmd.TargetPath.Volume.Driver.ResizeAsync(resizeCmd, cancellationToken);
              return ConnectorResult.Success(resizeResp);
            }
          case ConnectorCommand.Cmd_Search:
            {
              var searchCmd = new SearchCommand
              {
                Target = args.GetValueOrDefault(ConnectorCommand.Param_Target)!
              };
              searchCmd.TargetPath = await ParsePathAsync(searchCmd.Target, cancellationToken: cancellationToken);
              searchCmd.Q = args.GetValueOrDefault(ConnectorCommand.Param_Q)!;
              searchCmd.Mimes = args.GetValueOrDefault(ConnectorCommand.Param_MimesArr);
              searchCmd.Type = args.GetValueOrDefault(ConnectorCommand.Param_Type)!;
              cmd.CmdObject = searchCmd;

              SearchResponse finalSearchResp;
              if (searchCmd.TargetPath != null)
                finalSearchResp = await searchCmd.TargetPath.Volume.Driver.SearchAsync(searchCmd, cancellationToken);
              else
              {
                finalSearchResp = new SearchResponse();

                foreach (IVolume volume in Volumes)
                {
                  SearchResponse searchResp = await volume.Driver.SearchAsync(new SearchCommand
                  {
                    Mimes = searchCmd.Mimes,
                    Q = searchCmd.Q,
                    Target = volume.VolumeId,
                    TargetPath = await ParsePathAsync(volume.VolumeId, cancellationToken: cancellationToken)
                  }, cancellationToken);

                  finalSearchResp.Concat(searchResp);
                }
              }

              return ConnectorResult.Success(finalSearchResp);
            }

          // Remember to update AbortAsync
          case ConnectorCommand.Cmd_Upload:
            {
              var uploadCmd = new UploadCommand
              {
                Target = args.GetValueOrDefault(ConnectorCommand.Param_Target)!
              };
              uploadCmd.TargetPath = await ParsePathAsync(uploadCmd.Target, cancellationToken: cancellationToken);
              uploadCmd.Mimes = args.GetValueOrDefault(ConnectorCommand.Param_Mimes)!;
              uploadCmd.Upload = cmd.Files.Where(o => o.Name == ConnectorCommand.Param_Upload).ToArray();
              uploadCmd.UploadPath = args.GetValueOrDefault(ConnectorCommand.Param_UploadPath);
              uploadCmd.UploadPathInfos = await ParsePathsAsync(uploadCmd.UploadPath, cancellationToken);
              uploadCmd.MTime = args.GetValueOrDefault(ConnectorCommand.Param_MTime);
              uploadCmd.Name = args.GetValueOrDefault(ConnectorCommand.Param_Names);
              uploadCmd.Renames = args.GetValueOrDefault(ConnectorCommand.Param_Renames);
              uploadCmd.Suffix = args.GetValueOrDefault(ConnectorCommand.Param_Suffix)!;
              uploadCmd.Hashes = args.Where(kvp => kvp.Key.StartsWith(ConnectorCommand.Param_Hashes_Start))
                  .ToDictionary(o => o.Key, o => (string)o.Value!)!;
              if (byte.TryParse(args.GetValueOrDefault(ConnectorCommand.Param_Overwrite), out var overwrite))
                uploadCmd.Overwrite = overwrite;

              // Chunked upload processing
              uploadCmd.UploadName = args.GetValueOrDefault(ConnectorCommand.Param_Upload)!;
              uploadCmd.Chunk = args.GetValueOrDefault(ConnectorCommand.Param_Chunk);
              uploadCmd.Range = args.GetValueOrDefault(ConnectorCommand.Param_Range);
              uploadCmd.Cid = args.GetValueOrDefault(ConnectorCommand.Param_Cid);
              var isChunking = uploadCmd.Chunk.ToString().Length > 0;
              var isChunkMerge = isChunking && uploadCmd.Cid.ToString().Length == 0;
              var uploadCount = uploadCmd.Upload.Count();

              cmd.CmdObject = uploadCmd;
              IVolume volume = uploadCmd.TargetPath.Volume;

              if (uploadCmd.UploadPathInfos.Any(path => path.Volume != volume))
                throw new CommandParamsException(ConnectorCommand.Cmd_Upload);

              if (uploadCmd.UploadName == UploadCommand.ChunkFail && uploadCmd.Mimes == UploadCommand.ChunkFail)
              {
                await volume.Driver.AbortUploadAsync(uploadCmd, cancellationToken);
                throw new ConnectionAbortedException();
              }
              else if (isChunking && !isChunkMerge
                  && (uploadCmd.Upload.Count() != 1
                      || uploadCmd.Upload.Single().Length > uploadCmd.RangeInfo.TotalBytes))
              {
                throw new CommandParamsException(ConnectorCommand.Cmd_Upload);
              }
              else if (isChunkMerge && (string.IsNullOrWhiteSpace(uploadCmd.UploadName)
                      || string.IsNullOrWhiteSpace(uploadCmd.Chunk)))
              {
                throw new CommandParamsException(ConnectorCommand.Cmd_Upload);
              }

              if (volume.MaxUploadSize.HasValue)
              {
                if (uploadCmd.Upload.Any(file => file.Length > volume.MaxUploadSize))
                  throw new UploadFileSizeException();
              }

              UploadResponse uploadResp = await volume.Driver.UploadAsync(uploadCmd, cancellationToken);
              return ConnectorResult.Success(uploadResp);
            }
          case ConnectorCommand.Cmd_Zipdl:
            {
              var zipdlCommand = new ZipdlCommand
              {
                Targets = args.GetValueOrDefault(ConnectorCommand.Param_Targets)
              };

              if (byte.TryParse(args.GetValueOrDefault(ConnectorCommand.Param_Download), out var download))
                zipdlCommand.Download = download;

              cmd.CmdObject = zipdlCommand;

              if (zipdlCommand.Download != 1)
              {
                zipdlCommand.TargetPaths = await ParsePathsAsync(zipdlCommand.Targets, cancellationToken: cancellationToken);
                IVolume[] distinctVolumes = zipdlCommand.TargetPaths.Select(p => p.Volume).Distinct().ToArray();
                if (distinctVolumes.Length != 1) throw new CommandParamsException(ConnectorCommand.Cmd_Zipdl);

                Zipdl1stResponse zipdl1stResp = await distinctVolumes[0].Driver.ZipdlAsync(zipdlCommand, cancellationToken);
                return ConnectorResult.Success(zipdl1stResp);
              }

              PathInfo cwdPath = await ParsePathAsync(zipdlCommand.Targets.First()!, cancellationToken: cancellationToken);
              zipdlCommand.ArchiveFileKey = zipdlCommand.Targets[1]!;
              zipdlCommand.DownloadFileName = zipdlCommand.Targets[2]!;
              zipdlCommand.MimeType = zipdlCommand.Targets[3]!;
              zipdlCommand.CwdPath = cwdPath;

              FileResponse rawZipFile = await cwdPath.Volume.Driver.ZipdlRawAsync(zipdlCommand, cancellationToken);
              return ConnectorResult.File(rawZipFile);
            }
          default: throw new UnknownCommandException();
        }
      }
      catch (Exception generalEx)
      {
        Exception rootCause = generalEx.GetRootCause();

        if (rootCause is ConnectorException ex)
        {
          errResp = ex.ErrorResponse;
          if (ex.StatusCode != null) errSttCode = ex.StatusCode.Value;
        }
        else if (rootCause is UnauthorizedAccessException uaEx)
        {
          errResp = ErrorResponse.Factory.AccessDenied(uaEx);
        }
        else if (rootCause is FileNotFoundException fnfEx)
        {
          errResp = ErrorResponse.Factory.FileNotFound(fnfEx);
        }
        else if (rootCause is DirectoryNotFoundException dnfEx)
        {
          errResp = ErrorResponse.Factory.FolderNotFound(dnfEx);
        }
        else if (rootCause is TaskCanceledException taskEx)
        {
          errResp = ErrorResponse.Factory.ConnectionAborted(taskEx);
        }
        else if (rootCause is OperationCanceledException opEx)
        {
          errResp = ErrorResponse.Factory.ConnectionAborted(opEx);
        }
        else if (rootCause is IOException ioEx)
        {
          errResp = ErrorResponse.Factory.AccessDenied(ioEx);
        }
        else if (rootCause is ArgumentException argEx)
        {
          errResp = ErrorResponse.Factory.CommandParams(argEx, cmd.Cmd);
        }
        else
        {
          errResp = ErrorResponse.Factory.Unknown(generalEx);
          errSttCode = HttpStatusCode.InternalServerError;
        }
      }

      // If the error response is returned too fast, elFinder client will be likely to miss it.
      Thread.Sleep(options.DefaultErrResponseTimeoutMs);
      return ConnectorResult.Error(errResp, errSttCode);
    }

    public virtual async Task<PathInfo> ParsePathAsync(string target,
        bool createIfNotExists = true, bool fileByDefault = true, CancellationToken cancellationToken = default)
    {
      cancellationToken.ThrowIfCancellationRequested();

      if (string.IsNullOrEmpty(target)) return null!;

      var underscoreIdx = target.IndexOf('_');
      var volumeId = target[..(underscoreIdx + 1)];
      var pathHash = target[(underscoreIdx + 1)..];
      var filePath = pathParser.Decode(pathHash);

      IVolume volume = Volumes.FirstOrDefault(v => v.VolumeId == volumeId)!;

      return volume == null
        ? throw new FileNotFoundException()
        : await volume.Driver.ParsePathAsync(filePath, volume, target, createIfNotExists, fileByDefault, cancellationToken);
    }

    public virtual string AddVolume(IVolume volume)
    {
      Volumes.Add(volume);

      volume.VolumeId ??= $"{Volume.VolumePrefix}{Volumes.Count}{Volume.HashSeparator}";

      return volume.VolumeId;
    }

    public virtual async Task<ImageWithMimeType> GetThumbAsync(string target, CancellationToken cancellationToken = default)
    {
      cancellationToken.ThrowIfCancellationRequested();

      if (target != null)
      {
        PathInfo pathInfo = await ParsePathAsync(target, cancellationToken: cancellationToken);

        return await pathInfo.Volume.Driver.GetThumbAsync(pathInfo, cancellationToken: cancellationToken);
      }

      return null!;
    }

    public virtual async Task<IEnumerable<PathInfo>> ParsePathsAsync(IEnumerable<string> targets, CancellationToken cancellationToken = default)
    {
      cancellationToken.ThrowIfCancellationRequested();

      IEnumerable<Task<PathInfo>> tasks = targets.Select(async t => await ParsePathAsync(t, cancellationToken: cancellationToken));

      return await Task.WhenAll(tasks);
    }

    public async Task AbortAsync(RequestCommand cmd, CancellationToken cancellationToken = default)
    {
      cancellationToken.ThrowIfCancellationRequested();

      ArgumentNullException.ThrowIfNull(cmd);
      ArgumentNullException.ThrowIfNull(nameof(cmd.Args));

      if (options.EnabledCommands?.Contains(cmd.Cmd) == false) throw new CommandNoSupportException();

      IReadOnlyDictionary<string, Microsoft.Extensions.Primitives.StringValues> args = cmd.Args;
      cmd.Cmd = args.GetValueOrDefault(ConnectorCommand.Param_Cmd)!;

      if (string.IsNullOrEmpty(cmd.Cmd))
        throw new CommandRequiredException();

      switch (cmd.Cmd)
      {
        case ConnectorCommand.Cmd_Upload:
          {
            var uploadCmd = new UploadCommand
            {
              Target = args.GetValueOrDefault(ConnectorCommand.Param_Target)!
            };
            uploadCmd.TargetPath = await ParsePathAsync(uploadCmd.Target, cancellationToken: cancellationToken);
            uploadCmd.Mimes = args.GetValueOrDefault(ConnectorCommand.Param_Mimes)!;
            uploadCmd.UploadPath = args.GetValueOrDefault(ConnectorCommand.Param_UploadPath);
            uploadCmd.UploadPathInfos = await ParsePathsAsync(uploadCmd.UploadPath, cancellationToken);
            uploadCmd.MTime = args.GetValueOrDefault(ConnectorCommand.Param_MTime);
            uploadCmd.Name = args.GetValueOrDefault(ConnectorCommand.Param_Names);
            uploadCmd.Renames = args.GetValueOrDefault(ConnectorCommand.Param_Renames);
            uploadCmd.Suffix = args.GetValueOrDefault(ConnectorCommand.Param_Suffix)!;
            uploadCmd.Hashes = args.Where(kvp => kvp.Key.StartsWith(ConnectorCommand.Param_Hashes_Start))
                .ToDictionary(o => o.Key, o => (string)o.Value!)!;
            if (byte.TryParse(args.GetValueOrDefault(ConnectorCommand.Param_Overwrite), out var overwrite))
              uploadCmd.Overwrite = overwrite;

            // Chunked upload processing
            uploadCmd.UploadName = args.GetValueOrDefault(ConnectorCommand.Param_Upload)!;
            uploadCmd.Chunk = args.GetValueOrDefault(ConnectorCommand.Param_Chunk);
            uploadCmd.Range = args.GetValueOrDefault(ConnectorCommand.Param_Range);
            uploadCmd.Cid = args.GetValueOrDefault(ConnectorCommand.Param_Cid);
            var isChunking = uploadCmd.Chunk.ToString().Length > 0;
            var isChunkMerge = isChunking && uploadCmd.Cid.ToString().Length == 0;
            var uploadCount = uploadCmd.Upload.Count();

            if (uploadCmd.UploadName == UploadCommand.ChunkFail && uploadCmd.Mimes == UploadCommand.ChunkFail)
            {
              return;
            }

            if (isChunking && !isChunkMerge && uploadCmd.UploadPathInfos.Count() != 1)
              throw new CommandParamsException(ConnectorCommand.Cmd_Upload);

            if (isChunkMerge && (string.IsNullOrWhiteSpace(uploadCmd.UploadName)
                || string.IsNullOrWhiteSpace(uploadCmd.Chunk)))
              throw new CommandParamsException(ConnectorCommand.Cmd_Upload);

            IVolume volume = uploadCmd.TargetPath.Volume;

            if (volume.MaxUploadSize.HasValue)
            {
              if (uploadCmd.Upload.Any(file => file.Length > volume.MaxUploadSize))
                throw new UploadFileSizeException();
            }

            if (uploadCmd.UploadPathInfos.Any(path => path.Volume != volume))
              throw new CommandParamsException(ConnectorCommand.Cmd_Upload);

            await volume.Driver.AbortUploadAsync(uploadCmd, cancellationToken);
            return;
          }
      }
    }
  }
}
