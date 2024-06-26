﻿using elFinder.Net.Core;
using elFinder.Net.Core.Exceptions;
using elFinder.Net.Core.Extensions;
using elFinder.Net.Core.Helpers;
using elFinder.Net.Core.Models.Command;
using elFinder.Net.Core.Models.FileInfo;
using elFinder.Net.Core.Models.Options;
using elFinder.Net.Core.Models.Response;
using elFinder.Net.Core.Services;
using elFinder.Net.Core.Services.Drawing;
using elFinder.Net.Drivers.FileSystem.Extensions;
using elFinder.Net.Drivers.FileSystem.Helpers;
using elFinder.Net.Drivers.FileSystem.Services;
using elFinder.Net.Drivers.FileSystem.Streams;
using SixLabors.ImageSharp;
using System.IO.Compression;
using System.Net.Mime;
using System.Text;
using System;

namespace elFinder.Net.Drivers.FileSystem
{
  public class FileSystemDriver : IDriver
  {
    public const string ChunkingFolderPrefix = "_uploading_";
    public const string DefaultThumbExt = ".png";
    public const string CustomSearchMethodPrefix = "SearchMatch";
    private static readonly char[] _invalidFileNameChars = Path.GetInvalidFileNameChars();

    protected readonly IPathParser pathParser;
    protected readonly IPictureEditor pictureEditor;
    protected readonly IVideoEditor videoEditor;
    protected readonly IZipDownloadPathProvider zipDownloadPathProvider;
    protected readonly IZipFileArchiver zipFileArchiver;
    protected readonly IThumbnailBackgroundGenerator thumbnailBackgroundGenerator;
    protected readonly IConnector connector;
    protected readonly IConnectorManager connectorManager;
    protected readonly ICryptographyProvider cryptographyProvider;
    protected readonly ITempFileCleaner tempFileCleaner;

    public EventList<BeforeRemoveThumbAsync> OnBeforeRemoveThumb { get; } = [];
    public EventList<AfterRemoveThumbAsync> OnAfterRemoveThumb { get; } = [];
    public EventList<RemoveThumbErrorAsync> OnRemoveThumbError { get; } = [];
    public EventList<BeforeMakeDirAsync> OnBeforeMakeDir { get; } = [];
    public EventList<AfterMakeDirAsync> OnAfterMakeDir { get; } = [];
    public EventList<BeforeMakeFileAsync> OnBeforeMakeFile { get; } = [];
    public EventList<AfterMakeFileAsync> OnAfterMakeFile { get; } = [];
    public EventList<BeforeRenameAsync> OnBeforeRename { get; } = [];
    public EventList<AfterRenameAsync> OnAfterRename { get; } = [];
    public EventList<BeforeRemoveAsync> OnBeforeRemove { get; } = [];
    public EventList<AfterRemoveAsync> OnAfterRemove { get; } = [];
    public EventList<BeforeRollbackChunkAsync> OnBeforeRollbackChunk { get; } = [];
    public EventList<AfterRollbackChunkAsync> OnAfterRollbackChunk { get; } = [];
    public EventList<BeforeUploadAsync> OnBeforeUpload { get; } = [];
    public EventList<AfterUploadAsync> OnAfterUpload { get; } = [];
    public EventList<BeforeChunkMergedAsync> OnBeforeChunkMerged { get; } = [];
    public EventList<AfterChunkMergedAsync> OnAfterChunkMerged { get; } = [];
    public EventList<BeforeChunkTransferAsync> OnBeforeChunkTransfer { get; } = [];
    public EventList<AfterChunkTransferAsync> OnAfterChunkTransfer { get; } = [];
    public EventList<UploadErrorAsync> OnUploadError { get; } = [];
    public EventList<BeforeMoveAsync> OnBeforeMove { get; } = [];
    public EventList<AfterMoveAsync> OnAfterMove { get; } = [];
    public EventList<BeforeCopyAsync> OnBeforeCopy { get; } = [];
    public EventList<AfterCopyAsync> OnAfterCopy { get; } = [];
    public EventList<BeforeArchiveAsync> OnBeforeArchive { get; } = [];
    public EventList<AfterArchiveAsync> OnAfterArchive { get; } = [];
    public EventList<ArchiveErrorAsync> OnArchiveError { get; } = [];
    public EventList<BeforeExtractAsync> OnBeforeExtract { get; } = [];
    public EventList<AfterExtractAsync> OnAfterExtract { get; } = [];
    public EventList<BeforeExtractFileAsync> OnBeforeExtractFile { get; } = [];
    public EventList<AfterExtractFileAsync> OnAfterExtractFile { get; } = [];
    public EventList<BeforeWriteDataAsync> OnBeforeWriteData { get; } = [];
    public EventList<AfterWriteDataAsync> OnAfterWriteData { get; } = [];
    public EventList<BeforeWriteStreamAsync> OnBeforeWriteStream { get; } = [];
    public EventList<AfterWriteStreamAsync> OnAfterWriteStream { get; } = [];
    public EventList<BeforeWriteContentAsync> OnBeforeWriteContent { get; } = [];
    public EventList<AfterWriteContentAsync> OnAfterWriteContent { get; } = [];

    public FileSystemDriver(IPathParser pathParser,
        IPictureEditor pictureEditor,
        IVideoEditor videoEditor,
        IZipDownloadPathProvider zipDownloadPathProvider,
        IZipFileArchiver zipFileArchiver,
        IThumbnailBackgroundGenerator thumbnailBackgroundGenerator,
        ICryptographyProvider cryptographyProvider,
        IConnector connector,
        IConnectorManager connectorManager,
        ITempFileCleaner tempFileCleaner)
    {
      this.pathParser = pathParser;
      this.pictureEditor = pictureEditor;
      this.videoEditor = videoEditor;
      this.zipDownloadPathProvider = zipDownloadPathProvider;
      this.zipFileArchiver = zipFileArchiver;
      this.thumbnailBackgroundGenerator = thumbnailBackgroundGenerator;
      this.cryptographyProvider = cryptographyProvider;
      this.connector = connector;
      this.connectorManager = connectorManager;
      this.tempFileCleaner = tempFileCleaner;
    }

    public virtual async Task<LsResponse> LsAsync(LsCommand cmd, CancellationToken cancellationToken = default)
    {
      cancellationToken.ThrowIfCancellationRequested();

      LsResponse lsResp = new();
      PathInfo targetPath = cmd.TargetPath;
      IVolume volume = targetPath.Volume;

      if (!targetPath.Directory.ObjectAttribute.Read) throw new PermissionDeniedException();

      foreach (IFile item in await targetPath.Directory.GetFilesAsync(cmd.Mimes, verify: true, filter: null, cancellationToken: cancellationToken))
      {
        string itemName = item.Name;
        if (cmd.Intersect.Count > 0)
        {
          itemName = cmd.Intersect.FirstOrDefault(intersectItem => intersectItem.Equals(itemName, StringComparison.InvariantCultureIgnoreCase));
          if (itemName == null) continue;
        }

        var hash = item.GetHash(volume, pathParser);
        lsResp.list[hash] = itemName;
      }

      foreach (IDirectory item in await targetPath.Directory.GetDirectoriesAsync(cancellationToken: cancellationToken))
      {
        string itemName = item.Name;
        if (cmd.Intersect.Count > 0)
        {
          itemName = cmd.Intersect.FirstOrDefault(intersectItem => intersectItem.Equals(itemName, StringComparison.InvariantCultureIgnoreCase));
          if (itemName == null) continue;
        }

        var hash = item.GetHash(volume, pathParser);
        lsResp.list[hash] = itemName;
      }

      return lsResp;
    }

    public virtual async Task<MkdirResponse> MkdirAsync(MkdirCommand cmd, CancellationToken cancellationToken = default)
    {
      cancellationToken.ThrowIfCancellationRequested();

      if (!IsObjectNameValid(cmd.Name))
        throw new InvalidDirNameException();

      var mkdirResp = new MkdirResponse();
      PathInfo targetPath = cmd.TargetPath;
      IVolume volume = targetPath.Volume;
      var targetHash = targetPath.HashedTarget;

      if (!targetPath.Directory.CanCreateObject()) throw new PermissionDeniedException();

      if (!string.IsNullOrEmpty(cmd.Name))
      {
        var newDir = new FileSystemDirectory(PathHelper.SafelyCombine(targetPath.Directory.FullName,
            targetPath.Directory.FullName, cmd.Name), volume);

        await OnBeforeMakeDir.SafeInvokeAsync(newDir);
        await newDir.CreateAsync(cancellationToken: cancellationToken);
        await OnAfterMakeDir.SafeInvokeAsync(newDir);

        var hash = newDir.GetHash(volume, pathParser);
        mkdirResp.added.Add(await newDir.ToFileInfoAsync(hash, targetHash, volume, connector.Options, cancellationToken: cancellationToken));
      }

      foreach (string dir in cmd.Dirs)
      {
        string dirName = dir.StartsWith('/') ? dir.Substring(1) : dir;
        var newDir = new FileSystemDirectory(PathHelper.SafelyCombine(targetPath.Directory.FullName,
            targetPath.Directory.FullName, dirName), volume);

        await OnBeforeMakeDir.SafeInvokeAsync(newDir);
        await newDir.CreateAsync(cancellationToken: cancellationToken);
        await OnAfterMakeDir.SafeInvokeAsync(newDir);

        var hash = newDir.GetHash(volume, pathParser);
        var parentHash = newDir.GetParentHash(volume, pathParser);
        mkdirResp.added.Add(await newDir.ToFileInfoAsync(hash, parentHash, volume, connector.Options, cancellationToken: cancellationToken));

        string relativePath = volume.GetRelativePath(newDir);
        mkdirResp.hashes.Add($"/{dirName}", volume.VolumeId + pathParser.Encode(relativePath));
      }

      return mkdirResp;
    }

    public virtual async Task<MkfileResponse> MkfileAsync(MkfileCommand cmd, CancellationToken cancellationToken = default)
    {
      cancellationToken.ThrowIfCancellationRequested();

      if (!IsObjectNameValid(cmd.Name))
        throw new InvalidFileNameException();

      PathInfo targetPath = cmd.TargetPath;
      IVolume volume = targetPath.Volume;
      var targetHash = targetPath.HashedTarget;

      if (!targetPath.Directory.CanCreateObject()) throw new PermissionDeniedException();

      var newFile = new FileSystemFile(PathHelper.SafelyCombine(targetPath.Directory.FullName,
          targetPath.Directory.FullName, cmd.Name), volume);

      await OnBeforeMakeFile.SafeInvokeAsync(newFile);
      await newFile.CreateAsync(cancellationToken: cancellationToken);
      await OnAfterMakeFile.SafeInvokeAsync(newFile);

      var mkfileResp = new MkfileResponse();
      mkfileResp.added.Add(await newFile.ToFileInfoAsync(targetHash, volume, pathParser, pictureEditor, videoEditor, cancellationToken: cancellationToken));

      return mkfileResp;
    }

    public virtual async Task<OpenResponse> OpenAsync(OpenCommand cmd, CancellationToken cancellationToken = default)
    {
      cancellationToken.ThrowIfCancellationRequested();

      IVolume currentVolume = cmd.Volume;
      IDirectory cwd = null;
      string cwdHash, cwdParentHash;
      OpenResponse openResp;
      PathInfo targetPath = cmd.TargetPath;

      if (cmd.TargetPath?.Directory.ObjectAttribute.Read == false)
        throw new PermissionDeniedException();

      if (cmd.Init == 1)
      {
        if (targetPath == null)
        {
          if (currentVolume.StartDirectory != null && !currentVolume.IsRoot(currentVolume.StartDirectory)
              && Directory.Exists(currentVolume.StartDirectory))
          {
            cwd = new FileSystemDirectory(currentVolume.StartDirectory, currentVolume);
          }

          if (cwd == null || !cwd.ObjectAttribute.Read)
          {
            cwd = new FileSystemDirectory(currentVolume.RootDirectory, currentVolume);
            cwdHash = cwd.GetHash(currentVolume, pathParser);
            targetPath = new PathInfo(string.Empty, currentVolume, cwd, cwdHash);
          }
          else
          {
            cwdHash = cwd.GetHash(currentVolume, pathParser);
            targetPath = new PathInfo(currentVolume.GetRelativePath(cwd), currentVolume, cwd, cwdHash);
          }

          cmd.TargetPath = targetPath;
        }
        else
        {
          cwd = targetPath.Directory;
          cwdHash = targetPath.HashedTarget;
        }

        cwdParentHash = cwd.GetParentHash(currentVolume, pathParser);
        BaseInfoResponse fileInfo = await cwd.ToFileInfoAsync(
            cwdHash, cwdParentHash, currentVolume, connector.Options, cancellationToken: cancellationToken);

        InitResponse initResp;
        if (fileInfo is RootInfoResponse rootInfo)
        {
          initResp = new InitResponse(rootInfo, rootInfo.options, cwd.Volume);
        }
        else
        {
          initResp = new InitResponse(fileInfo,
              new ConnectorResponseOptions(cwd, connector.Options.DisabledUICommands, currentVolume.DirectorySeparatorChar),
              cwd.Volume);
          await AddParentsToListAsync(targetPath, initResp.files, cancellationToken: cancellationToken);
        }

        openResp = initResp;
      }
      else
      {
        cwd = targetPath.Directory;
        cwdHash = cwd.GetHash(currentVolume, pathParser);
        cwdParentHash = cwd.GetParentHash(currentVolume, pathParser);
        BaseInfoResponse fileInfo = await cwd.ToFileInfoAsync(cwdHash, cwdParentHash, currentVolume, connector.Options, cancellationToken: cancellationToken);

        if (fileInfo is RootInfoResponse rootInfo)
        {
          openResp = new OpenResponse(rootInfo, rootInfo.options, cwd.Volume);
        }
        else
        {
          openResp = new OpenResponse(fileInfo,
              new ConnectorResponseOptions(cwd, connector.Options.DisabledUICommands, currentVolume.DirectorySeparatorChar),
              cwd.Volume);
        }
      }

      foreach (IFile item in (await cwd.GetFilesAsync(cmd.Mimes, verify: true, filter: null, cancellationToken: cancellationToken)))
      {
        openResp.files.Add(await item.ToFileInfoAsync(cwdHash, currentVolume, pathParser, pictureEditor, videoEditor, cancellationToken: cancellationToken));
      }

      foreach (IDirectory item in (await cwd.GetDirectoriesAsync(cancellationToken: cancellationToken)))
      {
        var hash = item.GetHash(currentVolume, pathParser);
        openResp.files.Add(await item.ToFileInfoAsync(hash, cwdHash, currentVolume, connector.Options, cancellationToken: cancellationToken));
      }

      return openResp;
    }

    public virtual async Task<InfoResponse> InfoAsync(InfoCommand cmd, CancellationToken cancellationToken = default)
    {
      var infoResp = new InfoResponse();
      IVolume volume = cmd.TargetPaths.Select(p => p.Volume).First();

      foreach (PathInfo target in cmd.TargetPaths)
      {
        var targetHash = target.HashedTarget;
        var phash = target.FileSystem.GetParentHash(volume, pathParser);

        try
        {
          if (target.IsDirectory)
            infoResp.files.Add(await target.Directory.ToFileInfoAsync(targetHash, phash, volume, connector.Options, cancellationToken: cancellationToken));
          else
            infoResp.files.Add(await target.File.ToFileInfoAsync(phash, volume, pathParser, pictureEditor, videoEditor, cancellationToken: cancellationToken));
        }
        catch (Exception) { }
      }

      return infoResp;
    }

    public virtual async Task<ParentsResponse> ParentsAsync(ParentsCommand cmd, CancellationToken cancellationToken = default)
    {
      cancellationToken.ThrowIfCancellationRequested();

      var parentsResp = new ParentsResponse();
      PathInfo targetPath = cmd.TargetPath;
      IDirectory targetDir = targetPath.Directory;
      IVolume volume = targetPath.Volume;
      string targetHash = targetPath.HashedTarget;

      if (targetPath.IsRoot)
      {
        parentsResp.tree.Add(await targetDir.ToFileInfoAsync(targetHash, null, volume, connector.Options, cancellationToken: cancellationToken));
      }
      else
      {
        await AddParentsToListAsync(targetPath, parentsResp.tree, cancellationToken: cancellationToken);
      }

      return parentsResp;
    }

    public virtual Task<PathInfo> ParsePathAsync(string decodedPath, IVolume volume, string hashedTarget,
        bool createIfNotExists = true, bool fileByDefault = true, CancellationToken cancellationToken = default)
    {
      cancellationToken.ThrowIfCancellationRequested();

      string fullPath = volume.RootDirectory + decodedPath;

      if (Directory.Exists(fullPath) || (createIfNotExists && !fileByDefault)) return Task.FromResult(
           new PathInfo(decodedPath, volume, new FileSystemDirectory(fullPath, volume), hashedTarget));

      if (File.Exists(fullPath) || (createIfNotExists && fileByDefault)) return Task.FromResult(
           new PathInfo(decodedPath, volume, new FileSystemFile(fullPath, volume), hashedTarget));

      if (fileByDefault)
        throw new FileNotFoundException();
      throw new DirectoryNotFoundException();
    }

    public virtual async Task<RenameResponse> RenameAsync(RenameCommand cmd, CancellationToken cancellationToken = default)
    {
      cancellationToken.ThrowIfCancellationRequested();

      var renameResp = new RenameResponse();
      PathInfo targetPath = cmd.TargetPath;
      IVolume volume = targetPath.Volume;

      if (!IsObjectNameValid(cmd.Name))
        if (targetPath.IsDirectory)
          throw new InvalidDirNameException();
        else throw new InvalidFileNameException();

      await RemoveThumbsAsync(targetPath, cancellationToken: cancellationToken);

      if (targetPath.IsDirectory)
      {
        var prevName = targetPath.Directory.Name;

        await OnBeforeRename.SafeInvokeAsync(targetPath.Directory, cmd.Name);
        IDirectory renamedDir = await targetPath.Directory.RenameAsync(cmd.Name, cancellationToken: cancellationToken);
        await OnAfterRename.SafeInvokeAsync(targetPath.Directory, prevName);

        var hash = renamedDir.GetHash(volume, pathParser);
        var phash = renamedDir.GetParentHash(volume, pathParser);
        renameResp.added.Add(await renamedDir.ToFileInfoAsync(hash, phash, volume, connector.Options, cancellationToken: cancellationToken));
      }
      else
      {
        var prevName = targetPath.File.Name;

        await OnBeforeRename.SafeInvokeAsync(targetPath.File, cmd.Name);
        IFile renamedFile = await targetPath.File.RenameAsync(cmd.Name, cancellationToken: cancellationToken);
        await OnAfterRename.SafeInvokeAsync(targetPath.File, prevName);

        var phash = renamedFile.GetParentHash(volume, pathParser);
        renameResp.added.Add(await renamedFile.ToFileInfoAsync(phash, volume, pathParser, pictureEditor, videoEditor, cancellationToken: cancellationToken));
      }

      renameResp.removed.Add(targetPath.HashedTarget);

      return renameResp;
    }

    public virtual async Task<RmResponse> RmAsync(RmCommand cmd, CancellationToken cancellationToken = default)
    {
      cancellationToken.ThrowIfCancellationRequested();

      var rmResp = new RmResponse();

      foreach (PathInfo path in cmd.TargetPaths)
      {
        await RemoveThumbsAsync(path, cancellationToken: cancellationToken);

        if (path.IsDirectory)
        {
          if (await path.Directory.ExistsAsync)
          {
            await OnBeforeRemove.SafeInvokeAsync(path.Directory);
            await path.Directory.DeleteAsync(cancellationToken: cancellationToken);
            await OnAfterRemove.SafeInvokeAsync(path.Directory);
          }
        }
        else if (await path.File.ExistsAsync)
        {
          await OnBeforeRemove.SafeInvokeAsync(path.File);
          await path.File.DeleteAsync(cancellationToken: cancellationToken);
          await OnAfterRemove.SafeInvokeAsync(path.File);
        }

        rmResp.removed.Add(path.HashedTarget);
      }

      return rmResp;
    }

    public virtual Task SetupVolumeAsync(IVolume volume, CancellationToken cancellationToken = default)
    {
      cancellationToken.ThrowIfCancellationRequested();

      static void CreateAndHideDirectory(string str)
      {
        var dir = new DirectoryInfo(str);

        if (!dir.Exists)
          dir.Create();

        if (!dir.Attributes.HasFlag(FileAttributes.Hidden))
          dir.Attributes = FileAttributes.Hidden;
      }

      if (volume.ThumbnailDirectory != null)
      {
        CreateAndHideDirectory(volume.ThumbnailDirectory);
      }

      if (volume.TempDirectory != null)
      {
        CreateAndHideDirectory(volume.TempDirectory);
      }

      if (volume.TempArchiveDirectory != null)
      {
        CreateAndHideDirectory(volume.TempArchiveDirectory);
      }

      if (volume.ChunkDirectory != null)
      {
        CreateAndHideDirectory(volume.ChunkDirectory);
      }

      if (!Directory.Exists(volume.RootDirectory))
      {
        Directory.CreateDirectory(volume.RootDirectory);
      }

      return Task.CompletedTask;
    }

    public virtual async Task<TmbResponse> TmbAsync(TmbCommand cmd, CancellationToken cancellationToken = default)
    {
      cancellationToken.ThrowIfCancellationRequested();

      var tmbResp = new TmbResponse();
      IVolume volume = cmd.TargetPaths.Select(p => p.Volume).First();

      foreach (PathInfo target in cmd.TargetPaths)
      {
        if (target.IsDirectory) return null;

        (ImageWithMimeType thumb, IFile _, MediaType? _) = await CreateThumbAsync(target.File, cancellationToken: cancellationToken);

        if (thumb != null)
        {
          using (thumb) { }
        }

        tmbResp.images.Add(target.HashedTarget, target.HashedTarget);
      }

      return tmbResp;
    }

    public virtual async Task<UploadResponse> UploadAsync(UploadCommand cmd, CancellationToken cancellationToken = default)
    {
      cancellationToken.ThrowIfCancellationRequested();

      if (cmd.Name.Any(name => !IsObjectNameValid(name)))
        throw new InvalidFileNameException();

      if (cmd.Renames.Any(name => !IsObjectNameValid(name)))
        throw new InvalidFileNameException();

      if (!IsObjectNameValid(cmd.Suffix) || !IsObjectNameValid(cmd.UploadName))
        throw new InvalidFileNameException();

      var isChunking = cmd.Chunk.ToString().Length > 0;
      var isChunkMerge = isChunking && cmd.Cid.ToString().Length == 0;
      var isFinalUploading = !isChunking || isChunkMerge;

      if (isChunking && (cmd.Chunk.Any(name => !IsObjectNameValid(name))))
        throw new InvalidFileNameException();

      if (!isFinalUploading && !IsObjectNameValid(cmd.ChunkInfo.UploadingFileName))
        throw new InvalidFileNameException();

      var uploadResp = new UploadResponse();
      PathInfo targetPath = cmd.TargetPath;
      IVolume volume = targetPath.Volume;
      List<object> warning = uploadResp.GetWarnings();
      List<ErrorResponse> warningDetails = uploadResp.GetWarningDetails();
      var setNewParents = new HashSet<IDirectory>();

      if (isFinalUploading)
      {
        foreach (PathInfo uploadPath in cmd.UploadPathInfos.Distinct())
        {
          IDirectory directory = uploadPath.Directory;
          string lastParentHash = null;

          while (!volume.IsRoot(directory))
          {
            var hash = lastParentHash ?? directory.GetHash(volume, pathParser);
            lastParentHash = directory.GetParentHash(volume, pathParser);

            if (!await directory.ExistsAsync && setNewParents.Add(directory))
              uploadResp.added.Add(await directory.ToFileInfoAsync(hash, lastParentHash, volume, connector.Options, cancellationToken: cancellationToken));

            directory = directory.Parent;
          }
        }
      }

      if (isChunkMerge)
      {
        FileSystemDirectory chunkingDir = null;
        FileSystemFile uploadFileInfo = null;

        try
        {
          string uploadingFileName = Path.GetFileName(cmd.UploadName);
          string chunkMergeName = Path.GetFileName(cmd.Chunk);

          IDirectory uploadDir = cmd.UploadPath.Count > 0 ? cmd.UploadPathInfos.Single().Directory : cmd.TargetPath.Directory;
          var uploadDirHash = cmd.UploadPath.Count > 0 ? cmd.UploadPath.Single() : cmd.Target;
          var chunkingDirFullName = PathHelper.SafelyCombine(uploadDir.Volume.ChunkDirectory,
              uploadDir.Volume.ChunkDirectory, chunkMergeName);
          chunkingDir = new FileSystemDirectory(chunkingDirFullName, volume);

          if (!await chunkingDir.ExistsAsync)
            throw new DirectoryNotFoundException();

          if (!uploadDir.CanCreateObject())
            throw new PermissionDeniedException($"Permission denied: {volume.GetRelativePath(uploadDir)}");

          var uploadFullName = PathHelper.SafelyCombine(uploadDir.FullName, uploadDir.FullName, uploadingFileName);
          uploadFileInfo = new FileSystemFile(uploadFullName, volume);
          var isOverwrite = false;

          if (await uploadFileInfo.ExistsAsync)
          {
            if (cmd.Renames.Contains(uploadingFileName))
            {
              var fileNameWithoutExt = Path.GetFileNameWithoutExtension(uploadingFileName);
              var ext = Path.GetExtension(uploadingFileName);
              var backupName = $"{fileNameWithoutExt}{cmd.Suffix}{ext}";
              var fullBakName = PathHelper.SafelyCombine(uploadFileInfo.Parent.FullName, uploadFileInfo.Parent.FullName, backupName);
              var bakFile = new FileSystemFile(fullBakName, volume);

              if (await bakFile.ExistsAsync)
                backupName = await bakFile.GetCopyNameAsync(cmd.Suffix, cancellationToken: cancellationToken);

              var prevName = uploadFileInfo.Name;
              await OnBeforeRename.SafeInvokeAsync(uploadFileInfo, backupName);
              await uploadFileInfo.RenameAsync(backupName, cancellationToken: cancellationToken);
              await OnAfterRename.SafeInvokeAsync(uploadFileInfo, prevName);

              uploadResp.added.Add(await uploadFileInfo.ToFileInfoAsync(uploadDirHash, volume, pathParser, pictureEditor, videoEditor, cancellationToken: cancellationToken));
              uploadFileInfo = new FileSystemFile(uploadFullName, volume);
            }
            else if (cmd.Overwrite == 0 || (cmd.Overwrite == null && !volume.UploadOverwrite))
            {
              string newName = await uploadFileInfo.GetCopyNameAsync(cmd.Suffix, cancellationToken: cancellationToken);
              uploadFullName = PathHelper.SafelyCombine(uploadFileInfo.DirectoryName, uploadFileInfo.DirectoryName, newName);
              uploadFileInfo = new FileSystemFile(uploadFullName, volume);
              isOverwrite = false;
            }
            else if (!uploadFileInfo.ObjectAttribute.Write)
              throw new PermissionDeniedException();
            else isOverwrite = true;
          }

          ChunkedUploadInfo chunkedUploadInfo = connectorManager.GetLock<ChunkedUploadInfo>(chunkingDir.FullName) ?? throw new ConnectionAbortedException();
          await OnBeforeChunkMerged.SafeInvokeAsync(uploadFileInfo, isOverwrite);
          chunkedUploadInfo.IsFileTouched = true;
          await MergeChunksAsync(uploadFileInfo, chunkingDir, isOverwrite, cancellationToken: cancellationToken);
          await OnAfterChunkMerged.SafeInvokeAsync(uploadFileInfo, isOverwrite);

          connectorManager.ReleaseLockCache(chunkingDir.FullName);

          await uploadFileInfo.RefreshAsync(cancellationToken);
          uploadResp.added.Add(await uploadFileInfo.ToFileInfoAsync(uploadDirHash, volume, pathParser, pictureEditor, videoEditor, cancellationToken: cancellationToken));
        }
        catch (Exception ex)
        {
          ChunkedUploadInfo chunkedUploadInfo = connectorManager.GetLock<ChunkedUploadInfo>(chunkingDir.FullName);

          if (chunkedUploadInfo != null)
          {
            lock (chunkedUploadInfo)
            {
              chunkedUploadInfo.Exception = ex.GetRootCause();

              if (chunkingDir != null)
              {
                chunkingDir.RefreshAsync(cancellationToken).Wait(cancellationToken);

                if (chunkingDir.ExistsAsync.Result)
                {
                  OnBeforeRollbackChunk.SafeInvokeAsync(chunkingDir).Wait(cancellationToken);
                  OnBeforeRemove.SafeInvokeAsync(chunkingDir).Wait(cancellationToken);
                  chunkingDir.DeleteAsync(cancellationToken: cancellationToken).Wait(cancellationToken);
                  OnAfterRemove.SafeInvokeAsync(chunkingDir).Wait(cancellationToken);
                  OnAfterRollbackChunk.SafeInvokeAsync(chunkingDir).Wait(cancellationToken);
                }
              }

              if (uploadFileInfo != null && chunkedUploadInfo.IsFileTouched)
              {
                uploadFileInfo.RefreshAsync(cancellationToken).Wait(cancellationToken);

                if (uploadFileInfo.ExistsAsync.Result)
                {
                  OnBeforeRollbackChunk.SafeInvokeAsync(uploadFileInfo).Wait(cancellationToken);
                  OnBeforeRemove.SafeInvokeAsync(uploadFileInfo).Wait(cancellationToken);
                  uploadFileInfo.DeleteAsync(cancellationToken: cancellationToken).Wait(cancellationToken);
                  OnAfterRemove.SafeInvokeAsync(uploadFileInfo).Wait(cancellationToken);
                  OnAfterRollbackChunk.SafeInvokeAsync(uploadFileInfo).Wait(cancellationToken);
                }
              }
            }
          }

          await OnUploadError.SafeInvokeAsync(ex);
          throw;
        }
      }
      else
      {
        var uploadCount = cmd.Upload.Count();
        for (var idx = 0; idx < uploadCount; idx++)
        {
          Core.Http.IFormFileWrapper formFile = cmd.Upload.ElementAt(idx);
          IDirectory dest = null;
          IDirectory finalDest = null;
          string destHash = null;
          string uploadingFileName = "unknown", cleanFileName;

          try
          {
            (string UploadFileName, int CurrentChunkNo, int TotalChunks)? chunkInfo = null;

            if (isChunking)
            {
              chunkInfo = cmd.ChunkInfo;
              uploadingFileName = Path.GetFileName(chunkInfo.Value.UploadFileName);
              cleanFileName = Path.GetFileName(cmd.Chunk);
            }
            else
            {
              uploadingFileName = Path.GetFileName(formFile.FileName);
              cleanFileName = uploadingFileName;
            }

            if (volume.UploadOrder != null)
            {
              MimeType mimeType = MimeHelper.GetMimeType(Path.GetExtension(uploadingFileName));
              var constraintMap = new Dictionary<UploadConstraintType, IEnumerable<string>>
              {
                [UploadConstraintType.Allow] = volume.UploadAllow,
                [UploadConstraintType.Deny] = volume.UploadDeny,
              };

              foreach (UploadConstraintType constraintType in volume.UploadOrder)
              {
                IEnumerable<string> constraint = constraintMap[constraintType];
                if (constraint == null) continue;
                switch (constraintType)
                {
                  case UploadConstraintType.Allow:
                    {
                      if (!constraint.Contains(UploadConstants.UploadConstraintAllValue)
                          && !constraint.Contains(mimeType)
                          && !constraint.Contains(mimeType.Type)) throw new FileTypeNotAllowException();
                      break;
                    }
                  case UploadConstraintType.Deny:
                    {
                      if (constraint.Contains(UploadConstants.UploadConstraintAllValue)
                          || constraint.Contains(mimeType)
                          || constraint.Contains(mimeType.Type)) throw new FileTypeNotAllowException();
                      break;
                    }
                }
              }
            }

            if (cmd.UploadPath.Count > idx)
            {
              if (isFinalUploading)
              {
                dest = cmd.UploadPathInfos.ElementAt(idx).Directory;
                finalDest = dest;
                destHash = cmd.UploadPath[idx];
              }
              else
              {
                finalDest = cmd.UploadPathInfos.ElementAt(idx).Directory;
                var tempDest = GetChunkDirectory(finalDest, uploadingFileName, cmd.Cid);
                dest = new FileSystemDirectory(tempDest, volume);
                destHash = cmd.UploadPath[idx];
              }
            }
            else
            {
              if (isFinalUploading)
              {
                dest = targetPath.Directory;
                finalDest = dest;
                destHash = targetPath.HashedTarget;
              }
              else
              {
                finalDest = targetPath.Directory;
                var tempDest = GetChunkDirectory(finalDest, uploadingFileName, cmd.Cid);
                dest = new FileSystemDirectory(tempDest, volume);
                destHash = targetPath.HashedTarget;
              }
            }

            if (isChunking)
            {
              ChunkedUploadInfo chunkedUploadInfo = connectorManager.GetLock(dest.FullName, _ => new ChunkedUploadInfo());
              lock (chunkedUploadInfo)
              {
                if (chunkedUploadInfo.Exception != null) throw chunkedUploadInfo.Exception;

                if (!dest.ExistsAsync.Result)
                {
                  if (!dest.CanCreate()) throw new PermissionDeniedException();

                  chunkedUploadInfo.TotalUploaded = 0;
                  OnBeforeMakeDir.SafeInvokeAsync(dest).Wait(cancellationToken);
                  dest.CreateAsync(cancellationToken: cancellationToken).Wait(cancellationToken);
                  OnAfterMakeDir.SafeInvokeAsync(dest).Wait(cancellationToken);

                  WriteStatusFileAsync(dest).Wait(cancellationToken);
                }
              }
            }

            if (!finalDest.CanCreateObject())
              throw new PermissionDeniedException($"Permission denied: {volume.GetRelativePath(finalDest)}");

            var uploadFullName = PathHelper.SafelyCombine(dest.FullName, dest.FullName, cleanFileName);
            var uploadFileInfo = new FileSystemFile(uploadFullName, volume);
            var finalUploadFullName = PathHelper.SafelyCombine(finalDest.FullName, finalDest.FullName, uploadingFileName);
            FileSystemFile finalUploadFileInfo = isChunking ? new FileSystemFile(finalUploadFullName, volume) : uploadFileInfo;
            var isOverwrite = false;

            if (!isFinalUploading && await uploadFileInfo.ExistsAsync)
            {
              throw new PermissionDeniedException();
            }

            if (await finalUploadFileInfo.ExistsAsync)
            {
              if (cmd.Renames.Contains(uploadingFileName))
              {
                var fileNameWithoutExt = Path.GetFileNameWithoutExtension(uploadingFileName);
                var ext = Path.GetExtension(uploadingFileName);
                var backupName = $"{fileNameWithoutExt}{cmd.Suffix}{ext}";
                var fullBakName = PathHelper.SafelyCombine(finalUploadFileInfo.Parent.FullName, finalUploadFileInfo.Parent.FullName, backupName);
                var bakFile = new FileSystemFile(fullBakName, volume);

                if (await bakFile.ExistsAsync)
                  backupName = await bakFile.GetCopyNameAsync(cmd.Suffix, cancellationToken: cancellationToken);

                var prevName = finalUploadFileInfo.Name;
                await OnBeforeRename.SafeInvokeAsync(finalUploadFileInfo, backupName);
                await finalUploadFileInfo.RenameAsync(backupName, cancellationToken: cancellationToken);
                await OnAfterRename.SafeInvokeAsync(finalUploadFileInfo, prevName);

                uploadResp.added.Add(await finalUploadFileInfo.ToFileInfoAsync(destHash, volume, pathParser, pictureEditor, videoEditor, cancellationToken: cancellationToken));
                finalUploadFileInfo = new FileSystemFile(finalUploadFullName, volume);
              }
              else if (cmd.Overwrite == 0 || (cmd.Overwrite == null && !volume.UploadOverwrite))
              {
                string newName = await finalUploadFileInfo.GetCopyNameAsync(cmd.Suffix, cancellationToken: cancellationToken);
                finalUploadFullName = PathHelper.SafelyCombine(finalUploadFileInfo.DirectoryName, finalUploadFileInfo.DirectoryName, newName);
                finalUploadFileInfo = new FileSystemFile(finalUploadFullName, volume);
                isOverwrite = false;
              }
              else if (!finalUploadFileInfo.ObjectAttribute.Write)
                throw new PermissionDeniedException();
              else isOverwrite = true;
            }

            uploadFileInfo = isChunking ? uploadFileInfo : finalUploadFileInfo;

            if (isChunking)
            {
              await WriteStatusFileAsync(dest);
            }

            await OnBeforeUpload.SafeInvokeAsync(uploadFileInfo, finalUploadFileInfo, formFile, isOverwrite, isChunking);
            using (Stream fileStream = await uploadFileInfo.OpenWriteAsync(cancellationToken: cancellationToken))
            {
              await formFile.CopyToAsync(fileStream, cancellationToken: cancellationToken);
            }
            await OnAfterUpload.SafeInvokeAsync(uploadFileInfo, finalUploadFileInfo, formFile, isOverwrite, isChunking);

            if (isFinalUploading)
            {
              await finalUploadFileInfo.RefreshAsync(cancellationToken);
              uploadResp.added.Add(await finalUploadFileInfo.ToFileInfoAsync(destHash, volume, pathParser, pictureEditor, videoEditor, cancellationToken: cancellationToken));
            }
            else
            {
              ChunkedUploadInfo chunkedUploadInfo = connectorManager.GetLock<ChunkedUploadInfo>(dest.FullName);

              if (chunkedUploadInfo != null)
              {
                lock (chunkedUploadInfo)
                {
                  if (chunkedUploadInfo.Exception != null) throw chunkedUploadInfo.Exception;

                  chunkedUploadInfo.TotalUploaded++;

                  if (chunkedUploadInfo.TotalUploaded == cmd.ChunkInfo.TotalChunks)
                  {
                    uploadResp._chunkmerged = dest.Name;
                    uploadResp._name = uploadingFileName;
                  }
                }
              }
            }
          }
          catch (Exception ex)
          {
            Exception rootCause = ex.GetRootCause();

            if (isChunking && dest != null)
            {
              ChunkedUploadInfo chunkedUploadInfo = connectorManager.GetLock<ChunkedUploadInfo>(dest.FullName);
              var isExceptionReturned = false;

              if (chunkedUploadInfo != null)
              {
                lock (chunkedUploadInfo)
                {
                  isExceptionReturned = chunkedUploadInfo.Exception != null;

                  if (!isExceptionReturned)
                  {
                    chunkedUploadInfo.Exception = rootCause;
                  }

                  if (dest != null)
                  {
                    dest.RefreshAsync(cancellationToken).Wait(cancellationToken);

                    if (dest.ExistsAsync.Result)
                    {
                      OnBeforeRollbackChunk.SafeInvokeAsync(dest).Wait(cancellationToken);
                      OnBeforeRemove.SafeInvokeAsync(dest).Wait(cancellationToken);
                      dest.DeleteAsync(cancellationToken: cancellationToken).Wait(cancellationToken);
                      OnAfterRemove.SafeInvokeAsync(dest).Wait(cancellationToken);
                      OnAfterRollbackChunk.SafeInvokeAsync(dest).Wait(cancellationToken);
                    }
                  }
                }
              }

              if (isExceptionReturned) return new UploadResponse();

              await OnUploadError.SafeInvokeAsync(ex);
              throw;
            }

            await OnUploadError.SafeInvokeAsync(ex);

            if (rootCause is PermissionDeniedException pEx)
            {
              warning.Add(string.IsNullOrEmpty(pEx.Message) ? $"Permission denied: {uploadingFileName}" : pEx.Message);
              warningDetails.Add(ErrorResponse.Factory.UploadFile(pEx, uploadingFileName));
            }
            else if (rootCause is FileTypeNotAllowException fileTypeEx)
            {
              throw fileTypeEx;
            }
            else
            {
              warning.Add($"Failed to upload: {uploadingFileName}");
              warningDetails.Add(ErrorResponse.Factory.UploadFile(ex, uploadingFileName));
            }
          }
        }
      }

      return uploadResp;
    }

    public Task AbortUploadAsync(UploadCommand cmd, CancellationToken cancellationToken = default)
    {
      cancellationToken.ThrowIfCancellationRequested();

      if (cmd.Name.Any(name => !IsObjectNameValid(name)))
        throw new InvalidFileNameException();

      if (cmd.Renames.Any(name => !IsObjectNameValid(name)))
        throw new InvalidFileNameException();

      if (!IsObjectNameValid(cmd.Suffix) || !IsObjectNameValid(cmd.UploadName))
        throw new InvalidFileNameException();

      var isChunking = cmd.Chunk.ToString().Length > 0;
      var isChunkMerge = isChunking && cmd.Cid.ToString().Length == 0;
      var isFinalUploading = !isChunking || isChunkMerge;

      if (isChunking && cmd.Chunk.Any(name => !IsObjectNameValid(name)))
        throw new InvalidFileNameException();

      if (!isFinalUploading && !IsObjectNameValid(cmd.ChunkInfo.UploadingFileName))
        throw new InvalidFileNameException();

      if (isFinalUploading) return Task.CompletedTask;

      PathInfo targetPath = cmd.TargetPath;
      IVolume volume = targetPath.Volume;
      FileSystemDirectory dest = null;

      (string UploadFileName, int CurrentChunkNo, int TotalChunks)? chunkInfo = null;
      string uploadingFileName, cleanFileName;

      chunkInfo = cmd.ChunkInfo;
      uploadingFileName = chunkInfo.Value.UploadFileName;
      cleanFileName = Path.GetFileName(cmd.Chunk);

      IDirectory uploadDir = cmd.UploadPathInfos.FirstOrDefault()?.Directory ?? cmd.TargetPath.Directory;
      var tempDest = GetChunkDirectory(uploadDir, uploadingFileName, cmd.Cid);
      dest = new FileSystemDirectory(tempDest, volume);

      ChunkedUploadInfo chunkedUploadInfo = connectorManager.GetLock<ChunkedUploadInfo>(dest.FullName);

      if (chunkedUploadInfo != null)
      {
        lock (chunkedUploadInfo)
        {
          chunkedUploadInfo.Exception = new ConnectionAbortedException();

          if (!dest.ExistsAsync.Result)
            throw new DirectoryNotFoundException();

          if (!dest.CanDelete())
            throw new PermissionDeniedException($"Permission denied: {volume.GetRelativePath(dest)}");

          OnBeforeRemove.SafeInvokeAsync(dest).Wait(cancellationToken);
          dest.DeleteAsync(cancellationToken: cancellationToken).Wait(cancellationToken);
          OnAfterRemove.SafeInvokeAsync(dest).Wait(cancellationToken);
        }
      }

      return Task.CompletedTask;
    }

    public virtual async Task<TreeResponse> TreeAsync(TreeCommand cmd, CancellationToken cancellationToken = default)
    {
      cancellationToken.ThrowIfCancellationRequested();

      var treeResp = new TreeResponse();
      PathInfo targetPath = cmd.TargetPath;
      IVolume volume = targetPath.Volume;

      if (!targetPath.Directory.ObjectAttribute.Read) throw new PermissionDeniedException();

      foreach (IDirectory item in await targetPath.Directory.GetDirectoriesAsync(cancellationToken: cancellationToken))
      {
        var hash = item.GetHash(volume, pathParser);
        treeResp.tree.Add(await item.ToFileInfoAsync(hash, targetPath.HashedTarget, volume, connector.Options, cancellationToken: cancellationToken));
      }

      return treeResp;
    }

    public virtual async Task<SizeResponse> SizeAsync(SizeCommand cmd, CancellationToken cancellationToken = default)
    {
      cancellationToken.ThrowIfCancellationRequested();

      var sizeResp = new SizeResponse();

      foreach (PathInfo path in cmd.TargetPaths)
      {
        if (!path.FileSystem.ObjectAttribute.Read) throw new PermissionDeniedException();

        if (path.IsDirectory)
        {
          sizeResp.dirCnt++;

          Core.Models.DirectorySizeAndCount sizeAndCount = await path.Directory.GetSizeAndCountAsync(cancellationToken: cancellationToken);
          sizeResp.dirCnt += sizeAndCount.DirectoryCount;
          sizeResp.fileCnt += sizeAndCount.FileCount;
          sizeResp.size += sizeAndCount.Size;
        }
        else
        {
          sizeResp.fileCnt++;
          sizeResp.size += await path.File.LengthAsync;
        }
      }

      return sizeResp;
    }

    public virtual async Task<DimResponse> DimAsync(DimCommand cmd, CancellationToken cancellationToken = default)
    {
      cancellationToken.ThrowIfCancellationRequested();

      IFile file = cmd.TargetPath.File;

      if (!file.ObjectAttribute.Read) throw new PermissionDeniedException();

      using Stream stream = await file.OpenReadAsync(cancellationToken: cancellationToken);
      Size size = pictureEditor.ImageSize(stream);
      return new DimResponse(size);
    }

    public virtual async Task<FileResponse> FileAsync(FileCommand cmd, CancellationToken cancellationToken = default)
    {
      cancellationToken.ThrowIfCancellationRequested();

      PathInfo targetPath = cmd.TargetPath;
      IFile file = targetPath.File;

      if (!file.CanDownload()) throw new PermissionDeniedException();

      return new FileResponse
      {
        ContentType = MimeHelper.GetMimeType(file.Extension),
        FileStream = await file.OpenReadAsync(cancellationToken: cancellationToken),
        FileDownloadName = file.Name,
        ContentDisposition = cmd.Download == 1 ? DispositionTypeNames.Attachment : DispositionTypeNames.Inline
      };
    }

    public virtual async Task<GetResponse> GetAsync(GetCommand cmd, CancellationToken cancellationToken = default)
    {
      cancellationToken.ThrowIfCancellationRequested();

      var response = new GetResponse();
      PathInfo targetPath = cmd.TargetPath;
      var conv = cmd.Conv;
      var autoConv = conv == "1" || conv == "0";

      if (!targetPath.File.ObjectAttribute.Read) throw new PermissionDeniedException();

      try
      {
        Stream baseStream = await targetPath.File.OpenReadAsync(cancellationToken: cancellationToken);
        StreamReader reader = autoConv ? new StreamReader(baseStream, true) :
            new StreamReader(baseStream, Encoding.GetEncoding(conv));
        using (reader)
        {
          response.content = await GetInlineContentAsync(targetPath.File, reader, cancellationToken: cancellationToken);
          response.doconv = null;

          response.encoding = autoConv ? reader.CurrentEncoding.WebName.ToUpperInvariant() : conv;
          if (response.encoding.Equals(Encoding.UTF8.WebName, StringComparison.InvariantCultureIgnoreCase))
            response.encoding = null;
        }
      }
      catch (Exception ex)
      {
        response.SetException(ex);

        if (ex is NotSupportedException || ex is ArgumentException)
          switch (cmd.Conv)
          {
            case "1":
              response.content = false;
              break;
            case "0":
              response.doconv = "unknown";
              break;
            default:
              response.encoding = conv;
              break;
          }
        else throw;
      }

      return response;
    }

    public virtual async Task<PasteResponse> PasteAsync(PasteCommand cmd, CancellationToken cancellationToken = default)
    {
      cancellationToken.ThrowIfCancellationRequested();

      if (cmd.Renames.Any(name => !IsObjectNameValid(name)))
        throw new InvalidFileNameException();

      if (!IsObjectNameValid(cmd.Suffix))
        throw new InvalidFileNameException();

      var pasteResp = new PasteResponse();
      var isCut = cmd.Cut == 1;
      PathInfo dstPath = cmd.DstPath;
      IVolume dstVolume = dstPath.Volume;
      var copyOverwrite = dstPath.Volume.CopyOverwrite;

      foreach (PathInfo src in cmd.TargetPaths)
      {
        if (src.IsDirectory)
        {
          IDirectory pastedDir;
          var newDest = PathHelper.SafelyCombine(dstPath.Directory.FullName, dstPath.Directory.FullName, src.Directory.Name);
          var newDestDir = new FileSystemDirectory(newDest, dstPath.Volume);
          var exists = await newDestDir.ExistsAsync;

          if (exists && cmd.Renames.Contains(newDestDir.Name))
          {
            var backupName = $"{newDestDir.Name}{cmd.Suffix}";
            var fullBakName = PathHelper.SafelyCombine(newDestDir.Parent.FullName, newDestDir.Parent.FullName, backupName);
            var bakDir = new FileSystemDirectory(fullBakName, newDestDir.Volume);

            if (await bakDir.ExistsAsync)
              backupName = await bakDir.GetCopyNameAsync(cmd.Suffix, cancellationToken: cancellationToken);

            var prevName = newDestDir.Name;
            await OnBeforeRename.SafeInvokeAsync(newDestDir, backupName);
            await newDestDir.RenameAsync(backupName, cancellationToken: cancellationToken);
            await OnAfterRename.SafeInvokeAsync(newDestDir, prevName);

            var hash = newDestDir.GetHash(newDestDir.Volume, pathParser);
            pasteResp.added.Add(await newDestDir.ToFileInfoAsync(hash, dstPath.HashedTarget, newDestDir.Volume, connector.Options, cancellationToken: cancellationToken));
            newDestDir = new FileSystemDirectory(newDest, dstPath.Volume);

            exists = false;
          }

          if (isCut)
          {
            await RemoveThumbsAsync(src, cancellationToken: cancellationToken);

            if (exists)
            {
              await OnBeforeMove.SafeInvokeAsync(src.Directory, newDest, true);
              pastedDir = await MergeAsync(src.Directory, newDest, dstVolume, copyOverwrite, cancellationToken: cancellationToken);
              await OnBeforeRemove.SafeInvokeAsync(src.Directory);
              await src.Directory.DeleteAsync(cancellationToken: cancellationToken);
              await OnAfterRemove.SafeInvokeAsync(src.Directory);
              await OnAfterMove.SafeInvokeAsync(src.Directory, pastedDir, true);
            }
            else
            {
              await OnBeforeMove.SafeInvokeAsync(src.Directory, newDest, false);
              pastedDir = await src.Directory.MoveToAsync(newDest, dstVolume, cancellationToken: cancellationToken);
              await OnAfterMove.SafeInvokeAsync(src.Directory, pastedDir, false);
            }

            pasteResp.removed.Add(src.HashedTarget);
          }
          else
          {
            await OnBeforeCopy.SafeInvokeAsync(src.Directory, newDest, true);
            pastedDir = await CopyToAsync(src.Directory, newDest, dstVolume, copyOverwrite, cancellationToken: cancellationToken);
            await OnAfterCopy.SafeInvokeAsync(src.Directory, pastedDir, true);
          }

          if (pastedDir != null)
          {
            var hash = pastedDir.GetHash(dstPath.Volume, pathParser);
            pasteResp.added.Add(await pastedDir.ToFileInfoAsync(hash, dstPath.HashedTarget, dstPath.Volume, connector.Options, cancellationToken: cancellationToken));
          }
        }
        else
        {
          IFile pastedFile;
          IFile file = src.File;
          var newDest = PathHelper.SafelyCombine(dstPath.Directory.FullName, dstPath.Directory.FullName, file.Name);
          var newDestFile = new FileSystemFile(newDest, dstPath.Volume);
          var exists = await newDestFile.ExistsAsync;

          if (exists && cmd.Renames.Contains(newDestFile.Name))
          {
            var fileNameWithoutExt = Path.GetFileNameWithoutExtension(newDestFile.Name);
            var ext = Path.GetExtension(newDestFile.Name);
            var backupName = $"{fileNameWithoutExt}{cmd.Suffix}{ext}";
            var fullBakName = PathHelper.SafelyCombine(newDestFile.Parent.FullName, newDestFile.Parent.FullName, backupName);
            var bakFile = new FileSystemFile(fullBakName, newDestFile.Volume);

            if (await bakFile.ExistsAsync)
              backupName = await bakFile.GetCopyNameAsync(cmd.Suffix, cancellationToken: cancellationToken);

            var prevName = newDestFile.Name;
            await OnBeforeRename.SafeInvokeAsync(newDestFile, backupName);
            await newDestFile.RenameAsync(backupName, cancellationToken: cancellationToken);
            await OnAfterRename.SafeInvokeAsync(newDestFile, prevName);

            pasteResp.added.Add(await newDestFile.ToFileInfoAsync(dstPath.HashedTarget, newDestFile.Volume, pathParser, pictureEditor, videoEditor, cancellationToken: cancellationToken));
          }

          if (isCut)
          {
            await RemoveThumbsAsync(src, cancellationToken: cancellationToken);

            pastedFile = await SafeMoveToAsync(file, dstPath.Directory.FullName,
                dstVolume, dstVolume.CopyOverwrite, cancellationToken: cancellationToken);

            pasteResp.removed.Add(src.HashedTarget);
          }
          else
          {
            pastedFile = await SafeCopyToAsync(file, dstPath.Directory.FullName,
                dstVolume, dstVolume.CopyOverwrite, cancellationToken: cancellationToken);
          }

          pasteResp.added.Add(await pastedFile.ToFileInfoAsync(dstPath.HashedTarget, dstPath.Volume, pathParser, pictureEditor, videoEditor, cancellationToken: cancellationToken));
        }
      }

      return pasteResp;
    }

    public virtual async Task<DuplicateResponse> DuplicateAsync(DuplicateCommand cmd, CancellationToken cancellationToken = default)
    {
      cancellationToken.ThrowIfCancellationRequested();

      var dupResp = new DuplicateResponse();

      foreach (PathInfo src in cmd.TargetPaths)
      {
        IVolume dstVolume = src.Volume;

        if (src.IsDirectory)
        {
          var newName = await src.Directory.GetCopyNameAsync(cancellationToken: cancellationToken);
          var newDest = PathHelper.SafelyCombine(src.Directory.Parent.FullName, src.Directory.Parent.FullName, newName);

          await OnBeforeCopy.SafeInvokeAsync(src.Directory, newDest, true);
          IDirectory dupDir = await CopyToAsync(src.Directory, newDest, dstVolume, copyOverwrite: false, cancellationToken: cancellationToken);
          await OnAfterCopy.SafeInvokeAsync(src.Directory, dupDir, true);

          var hash = dupDir.GetHash(src.Volume, pathParser);
          var parentHash = dupDir.GetParentHash(src.Volume, pathParser);
          dupResp.added.Add(await dupDir.ToFileInfoAsync(hash, parentHash, src.Volume, connector.Options, cancellationToken: cancellationToken));
        }
        else
        {
          IFile dupFile = await SafeCopyToAsync(src.File, src.File.Parent.FullName, dstVolume,
              copyOverwrite: false, cancellationToken: cancellationToken);

          var parentHash = src.File.GetParentHash(src.Volume, pathParser);
          dupResp.added.Add(await dupFile.ToFileInfoAsync(parentHash, src.Volume, pathParser, pictureEditor, videoEditor, cancellationToken: cancellationToken));
        }
      }

      return dupResp;
    }

    public virtual async Task<SearchResponse> SearchAsync(SearchCommand cmd, CancellationToken cancellationToken = default)
    {
      cancellationToken.ThrowIfCancellationRequested();

      var searchResp = new SearchResponse();
      PathInfo targetPath = cmd.TargetPath;
      IVolume volume = targetPath.Volume;

      if (!targetPath.Directory.ObjectAttribute.Read) throw new PermissionDeniedException();

      if (!string.IsNullOrWhiteSpace(cmd.Type))
      {
        System.Reflection.MethodInfo method = GetType().GetMethod($"{CustomSearchMethodPrefix}{cmd.Type}",
            [typeof(SearchCommand), typeof(CancellationToken)]) ?? throw new KeyNotFoundException("Custom search function not found");
        if (method.ReturnType != typeof(Task<SearchResponse>))
        {
          throw new InvalidCastException("Invalid return type");
        }


        return method.Invoke(this, new object[] { cmd, cancellationToken }) is Task<SearchResponse> task
            ? await task
            : throw new InvalidOperationException("Invalid response from custom search function");
      }
      else
      {
        foreach (IFile item in await targetPath.Directory.GetFilesAsync(cmd.Q, cmd.Mimes,
            searchOption: SearchOption.AllDirectories, cancellationToken: cancellationToken))
        {
          var parentHash = item.Parent.Equals(targetPath.Directory) ? targetPath.HashedTarget :
              item.GetParentHash(volume, pathParser);
          searchResp.files.Add(await item.ToFileInfoAsync(parentHash, volume, pathParser, pictureEditor, videoEditor, cancellationToken: cancellationToken));
        }

        if (cmd.Mimes.Count == 0)
        {
          foreach (IDirectory item in await targetPath.Directory.GetDirectoriesAsync(cmd.Q,
              searchOption: SearchOption.AllDirectories, cancellationToken: cancellationToken))
          {
            var hash = item.GetHash(volume, pathParser);
            var parentHash = item.Parent.Equals(targetPath.Directory) ? targetPath.HashedTarget :
                item.GetParentHash(volume, pathParser);
            searchResp.files.Add(await item.ToFileInfoAsync(hash, parentHash, volume, connector.Options, cancellationToken: cancellationToken));
          }
        }

        return searchResp;
      }
    }

    public virtual async Task<ArchiveResponse> ArchiveAsync(ArchiveCommand cmd, CancellationToken cancellationToken = default)
    {
      cancellationToken.ThrowIfCancellationRequested();

      if (!IsObjectNameValid(cmd.Name))
        throw new InvalidFileNameException();

      if (cmd.Type != MediaTypeNames.Application.Zip)
        throw new ArchiveTypeException();

      var archiveResp = new ArchiveResponse();
      PathInfo targetPath = cmd.TargetPath;
      IVolume volume = cmd.TargetPath.Volume;
      IDirectory directory = targetPath.Directory;

      var filename = cmd.Name ?? targetPath.Directory.Name;
      var zipExt = $".{FileExtensions.Zip}";

      if (!filename.EndsWith(zipExt))
        filename += zipExt;

      var archivePath = PathHelper.SafelyCombine(directory.FullName, directory.FullName, filename);
      var newFile = new FileSystemFile(archivePath, volume);

      if (!await newFile.CanArchiveToAsync(cancellationToken: cancellationToken))
        throw new PermissionDeniedException();

      if (newFile.DirectoryExists())
        throw new ExistsException(newFile.Name);

      try
      {
        await OnBeforeArchive.SafeInvokeAsync(newFile);
        using (ZipArchive fileStream = ZipFile.Open(archivePath, ZipArchiveMode.Update))
        {
          foreach (PathInfo path in cmd.TargetPaths)
          {
            if (!path.FileSystem.CanBeArchived()) throw new PermissionDeniedException();

            if (path.IsDirectory)
            {
              await zipFileArchiver.AddDirectoryAsync(fileStream, path.Directory,
                  fromDir: string.Empty, isDownload: false, cancellationToken: cancellationToken);
            }
            else
            {
              zipFileArchiver.CreateEntryFromFile(fileStream, path.File, path.File.Name);
            }
          }
        }
        await OnAfterArchive.SafeInvokeAsync(newFile);
      }
      catch (Exception e)
      {
        await OnArchiveError.SafeInvokeAsync(e, newFile);
        throw;
      }

      await newFile.RefreshAsync(cancellationToken);
      archiveResp.added.Add(await newFile.ToFileInfoAsync(targetPath.HashedTarget, volume, pathParser, pictureEditor, videoEditor, cancellationToken: cancellationToken));
      return archiveResp;
    }

    public virtual async Task<ExtractResponse> ExtractAsync(ExtractCommand cmd, CancellationToken cancellationToken = default)
    {
      cancellationToken.ThrowIfCancellationRequested();

      var extractResp = new ExtractResponse();
      PathInfo targetPath = cmd.TargetPath;
      IVolume volume = targetPath.Volume;
      IDirectory targetParent = targetPath.File.Parent;
      var fromPath = targetParent.FullName;
      var parentDir = new FileSystemDirectory(fromPath, volume);
      FileSystemDirectory fromDir = parentDir;
      var makedir = cmd.MakeDir == 1;

      if (!targetPath.File.CanExtract()) throw new PermissionDeniedException();

      if (makedir)
      {
        fromPath = PathHelper.SafelyCombine(fromPath, fromPath, Path.GetFileNameWithoutExtension(targetPath.File.Name));
        fromDir = new FileSystemDirectory(fromPath, volume);

        if (!await fromDir.CanExtractToAsync(cancellationToken: cancellationToken))
          throw new PermissionDeniedException();

        if (fromDir.FileExists())
          throw new ExistsException(fromDir.Name);

        if (!await fromDir.ExistsAsync)
        {
          await OnBeforeMakeDir.SafeInvokeAsync(fromDir);
          await fromDir.CreateAsync(cancellationToken: cancellationToken);
          await OnAfterMakeDir.SafeInvokeAsync(fromDir);
        }

        var hash = fromDir.GetHash(volume, pathParser);
        var parentHash = fromDir.GetParentHash(volume, pathParser);
        extractResp.added.Add(await fromDir.ToFileInfoAsync(hash, parentHash, volume, connector.Options, cancellationToken: cancellationToken));
      }

      await OnBeforeExtract.SafeInvokeAsync(parentDir, fromDir, targetPath.File);
      using (ZipArchive archive = ZipFile.OpenRead(targetPath.File.FullName))
      {
        foreach (ZipArchiveEntry entry in archive.Entries)
        {
          string fullName = PathHelper.GetFullPathNormalized(
              PathHelper.SafelyCombine(fromPath, fromPath, entry.FullName));

          if (string.IsNullOrEmpty(entry.Name))
          {
            var dir = new FileSystemDirectory(fullName, volume);
            if (!await dir.CanExtractToAsync(cancellationToken: cancellationToken))
              throw new PermissionDeniedException();

            if (dir.FileExists())
              throw new ExistsException(dir.Name);

            if (!await dir.ExistsAsync)
            {
              await OnBeforeMakeDir.SafeInvokeAsync(dir);
              await dir.CreateAsync(cancellationToken: cancellationToken);
              await OnAfterMakeDir.SafeInvokeAsync(dir);
            }

            if (!makedir)
            {
              var parentHash = dir.GetParentHash(volume, pathParser);
              var hash = dir.GetHash(volume, pathParser);
              extractResp.added.Add(await dir.ToFileInfoAsync(hash, parentHash, volume, connector.Options, cancellationToken: cancellationToken));
            }
          }
          else
          {
            var file = new FileSystemFile(fullName, volume);

            if (!await file.CanExtractToAsync(cancellationToken: cancellationToken)) throw new PermissionDeniedException();

            if (file.DirectoryExists())
              throw new ExistsException(file.Name);

            ArchivedFileEntry entryModel = entry.ToEntry();
            var isOverwrite = await file.ExistsAsync;

            await OnBeforeExtractFile.SafeInvokeAsync(entryModel, file, isOverwrite);
            await zipFileArchiver.ExtractToAsync(entry, file, isOverwrite, cancellationToken: cancellationToken);
            await OnAfterExtractFile.SafeInvokeAsync(entryModel, file, isOverwrite);

            if (!makedir)
            {
              await file.RefreshAsync(cancellationToken);
              var parentHash = file.GetParentHash(volume, pathParser);
              extractResp.added.Add(await file.ToFileInfoAsync(parentHash, volume, pathParser, pictureEditor, videoEditor, cancellationToken: cancellationToken));
            }
          }
        }
      }

      await OnAfterExtract.SafeInvokeAsync(parentDir, fromDir, targetPath.File);

      return extractResp;
    }

    public virtual async Task<PutResponse> PutAsync(PutCommand cmd, CancellationToken cancellationToken = default)
    {
      cancellationToken.ThrowIfCancellationRequested();

      var putResp = new PutResponse();
      PathInfo targetPath = cmd.TargetPath;
      IFile targetFile = targetPath.File;
      IVolume volume = targetPath.Volume;

      if (!targetFile.ObjectAttribute.Write) throw new PermissionDeniedException();

      if (cmd.Encoding == "scheme")
      {
        if (cmd.Content.StartsWith(WebConsts.UriScheme.Data))
        {
          var data = ParseDataURIScheme(cmd.Content, nameof(ConnectorCommand.Cmd_Put));

          await OnBeforeWriteData.SafeInvokeAsync(data, targetFile);
          using (Stream fileStream = await targetFile.OpenWriteAsync(cancellationToken: cancellationToken))
          {
            fileStream.Write(data, 0, data.Length);
          }
          await OnAfterWriteData.SafeInvokeAsync(data, targetFile);
        }
        else
        {
          using var client = new HttpClient();
          Func<Task<Stream>> openFunc = async () => await client.GetStreamAsync(cmd.Content);
          using Stream dataStream = await openFunc();
          await OnBeforeWriteStream.SafeInvokeAsync(openFunc, targetFile);
          using (Stream fileStream = await targetFile.OpenWriteAsync(cancellationToken: cancellationToken))
          {
            await dataStream.CopyToAsync(fileStream, StreamConstants.DefaultBufferSize, cancellationToken: cancellationToken);
          }
          await OnAfterWriteStream.SafeInvokeAsync(openFunc, targetFile);
        }
      }
      else if (cmd.Encoding == "hash")
      {
        Func<Task<Stream>> openStreamFunc = async () => await cmd.ContentPath.File.OpenReadAsync(cancellationToken: cancellationToken);

        await OnBeforeWriteStream.SafeInvokeAsync(openStreamFunc, targetFile);
        using (Stream readStream = await openStreamFunc())
        {
          await targetFile.OverwriteAsync(readStream, cancellationToken: cancellationToken);
        }
        await OnAfterWriteStream.SafeInvokeAsync(openStreamFunc, targetFile);
      }
      else
      {
        await OnBeforeWriteContent.SafeInvokeAsync(cmd.Content, cmd.Encoding, targetFile);
        using (Stream fileStream = await targetFile.OpenWriteAsync(cancellationToken: cancellationToken))
        using (var writer = new StreamWriter(fileStream, Encoding.GetEncoding(cmd.Encoding)))
        {
          writer.Write(cmd.Content);
        }
        await OnAfterWriteContent.SafeInvokeAsync(cmd.Content, cmd.Encoding, targetFile);
      }

      await targetFile.RefreshAsync(cancellationToken);
      var parentHash = targetFile.GetParentHash(volume, pathParser);
      putResp.changed.Add(await targetFile.ToFileInfoAsync(parentHash, volume, pathParser, pictureEditor, videoEditor, cancellationToken: cancellationToken));

      return putResp;
    }

    public virtual async Task<ResizeResponse> ResizeAsync(ResizeCommand cmd, CancellationToken cancellationToken = default)
    {
      cancellationToken.ThrowIfCancellationRequested();

      var resizeResp = new ResizeResponse();
      PathInfo targetPath = cmd.TargetPath;
      IVolume volume = targetPath.Volume;
      IFile targetFile = targetPath.File;

      if (!targetFile.CanEditImage()) throw new PermissionDeniedException();

      switch (cmd.Mode)
      {
        case ResizeCommand.Mode_Resize:
          {
            await RemoveThumbsAsync(targetPath, cancellationToken: cancellationToken);

            Func<Task<ImageWithMimeType>> getImageFunc = async () =>
            {
              using Stream stream = await targetFile.OpenReadAsync(cancellationToken: cancellationToken);
              var image = Image.Load(stream);
              return await pictureEditor
                              .ScaleAsync(image, cmd.Width, cmd.Height, cmd.Quality);
            };

            Func<Task<Stream>> openStreamFunc = async () => (await getImageFunc()).ImageStream;

            ImageWithMimeType image = await getImageFunc();

            await OnBeforeWriteStream.SafeInvokeAsync(openStreamFunc, targetFile);
            using (Stream stream = await targetFile.OpenWriteAsync(cancellationToken: cancellationToken))
            {
              await image.ImageStream.CopyToAsync(stream, StreamConstants.DefaultBufferSize, cancellationToken: cancellationToken);
            }
            await OnAfterWriteStream.SafeInvokeAsync(openStreamFunc, targetFile);
          }
          break;
        case ResizeCommand.Mode_Crop:
          {
            await RemoveThumbsAsync(targetPath, cancellationToken: cancellationToken);

            Func<Task<ImageWithMimeType>> getImageFunc = async () =>
            {
              using Stream stream = await targetFile.OpenReadAsync(cancellationToken: cancellationToken);
              var image = Image.Load(stream);
              return await pictureEditor.CropAsync(image, cmd.X, cmd.Y,
                              cmd.Width, cmd.Height, cmd.Quality);
            };

            Func<Task<Stream>> openStreamFunc = async () => (await getImageFunc()).ImageStream;

            ImageWithMimeType image = await getImageFunc();

            await OnBeforeWriteStream.SafeInvokeAsync(openStreamFunc, targetFile);
            using (Stream stream = await targetFile.OpenWriteAsync(cancellationToken: cancellationToken))
            {
              await image.ImageStream.CopyToAsync(stream, StreamConstants.DefaultBufferSize, cancellationToken: cancellationToken);
            }
            await OnAfterWriteStream.SafeInvokeAsync(openStreamFunc, targetFile);
          }
          break;
        case ResizeCommand.Mode_Rotate:
          {
            await RemoveThumbsAsync(targetPath, cancellationToken: cancellationToken);

            Func<Task<ImageWithMimeType>> getImageFunc = async () =>
            {
              using Stream stream = await targetFile.OpenReadAsync(cancellationToken: cancellationToken);
              return await pictureEditor
                          .RotateAsync(stream, cmd.Degree, cmd.Background, cmd.Quality);
            };

            Func<Task<Stream>> openStreamFunc = async () => (await getImageFunc()).ImageStream;

            ImageWithMimeType image = await getImageFunc();

            await OnBeforeWriteStream.SafeInvokeAsync(openStreamFunc, targetFile);
            using (Stream stream = await targetFile.OpenWriteAsync(cancellationToken: cancellationToken))
            {
              await image.ImageStream.CopyToAsync(stream, StreamConstants.DefaultBufferSize, cancellationToken: cancellationToken);
            }
            await OnAfterWriteStream.SafeInvokeAsync(openStreamFunc, targetFile);
          }
          break;
        default:
          throw new UnknownCommandException();
      }

      await targetFile.RefreshAsync(cancellationToken);
      var parentHash = targetFile.GetParentHash(volume, pathParser);
      resizeResp.changed.Add(await targetFile.ToFileInfoAsync(parentHash, volume, pathParser, pictureEditor, videoEditor, cancellationToken: cancellationToken));
      return resizeResp;
    }

    public virtual async Task<Zipdl1stResponse> ZipdlAsync(ZipdlCommand cmd, CancellationToken cancellationToken = default)
    {
      cancellationToken.ThrowIfCancellationRequested();

      var zipdlData = new ZipdlData();
      IEnumerable<PathInfo> targetPaths = cmd.TargetPaths;
      IVolume volume = cmd.TargetPaths.Select(p => p.Volume).First();
      var zipExt = $".{FileExtensions.Zip}";

      (string archivePath, string archiveFileKey) = await zipDownloadPathProvider.GetFileForArchivingAsync(
          volume.TempArchiveDirectory, cancellationToken: cancellationToken);
      var newFile = new FileSystemFile(archivePath, volume);

      try
      {
        using ZipArchive fileStream = ZipFile.Open(archivePath, ZipArchiveMode.Update);
        foreach (PathInfo path in cmd.TargetPaths)
        {
          if (!path.FileSystem.CanDownload())
            throw new PermissionDeniedException();

          if (path.IsDirectory)
          {
            await zipFileArchiver.AddDirectoryAsync(fileStream, path.Directory,
                fromDir: string.Empty, true, path.IsRoot ? volume.Name : null, cancellationToken: cancellationToken);
          }
          else
          {
            zipFileArchiver.CreateEntryFromFile(fileStream, path.File, path.File.Name);
          }
        }
      }
      catch
      {
        if (File.Exists(archivePath)) File.Delete(archivePath);
        throw;
      }

      zipdlData.mime = MediaTypeNames.Application.Zip;
      zipdlData.name = DownloadHelper.GetZipDownloadName(cmd.TargetPaths) + zipExt;
      zipdlData.file = archiveFileKey;

      return new Zipdl1stResponse
      {
        zipdl = zipdlData
      };
    }

    public virtual async Task<FileResponse> ZipdlRawAsync(ZipdlCommand cmd, CancellationToken cancellationToken = default)
    {
      cancellationToken.ThrowIfCancellationRequested();

      if (!IsObjectNameValid(cmd.DownloadFileName))
        throw new InvalidFileNameException();

      IVolume volume = cmd.CwdPath.Volume;
      var archiveFile = await zipDownloadPathProvider.ParseArchiveFileKeyAsync(
          volume.TempArchiveDirectory, cmd.ArchiveFileKey, cancellationToken);
      var tempFileInfo = new FileInfo(archiveFile);

      if (!tempFileInfo.Exists) throw new PermissionDeniedException($"Malformed key");

      var memStream = new MemoryStream();
      using (FileStream fileStream = tempFileInfo.OpenRead())
      {
        await fileStream.CopyToAsync(memStream, StreamConstants.DefaultBufferSize, cancellationToken: cancellationToken);
      }
      tempFileInfo.Delete();
      memStream.Position = 0;

      return new FileResponse
      {
        ContentType = cmd.MimeType,
        FileStream = memStream,
        FileDownloadName = cmd.DownloadFileName
      };
    }

    public virtual async Task<ImageWithMimeType> GetThumbAsync(PathInfo target, CancellationToken cancellationToken = default)
    {
      cancellationToken.ThrowIfCancellationRequested();

      if (target.IsDirectory) return null;

      (ImageWithMimeType thumb, IFile thumbFile, MediaType? mediaType) = await CreateThumbAsync(target.File, cancellationToken: cancellationToken);

      if (thumb != null)
      {
        thumb.ImageStream.Position = 0;
        return thumb;
      }

      if (thumbFile == null || !await thumbFile.ExistsAsync)
      {
        thumbnailBackgroundGenerator.TryAddToQueue(target.File, thumbFile, target.File.Volume.ThumbnailSize, true, mediaType);
        return null;
      }

      string mimeType = MimeHelper.GetMimeType(thumbFile.Extension);
      return new ImageWithMimeType(mimeType, await thumbFile.OpenReadAsync(cancellationToken: cancellationToken));
    }

    public virtual async Task<string> GenerateThumbPathAsync(IFile file, CancellationToken cancellationToken = default)
    {
      cancellationToken.ThrowIfCancellationRequested();

      IVolume volume = file.Volume;
      var volumeTmbDir = volume.ThumbnailDirectory;
      var volumeSeparator = volume.DirectorySeparatorChar;

      if (volumeTmbDir == null)
        return null;

      if (file.FullName.StartsWith(volumeTmbDir + volumeSeparator))
        return file.FullName;

      volumeTmbDir = PathHelper.GetFullPathNormalized(volumeTmbDir);
      string relativePath = volume.GetRelativePath(file);
      string thumbDir = PathHelper.GetFullPathNormalized(Path.GetDirectoryName($"{volumeTmbDir}{relativePath}"));
      //string md5 = await file.GetFileMd5Async(cancellationToken);

      var ticks = (await file.LastWriteTimeUtcAsync).Ticks;
      //string thumbName = $"{Path.GetFileNameWithoutExtension(file.Name)}_{ticks}{DefaultThumbExt}";
      string thumbName = Path.GetFileName(file.Name);

      return $"{thumbDir}{volumeSeparator}{thumbName}";
    }

    public virtual Task<string> GenerateThumbPathAsync(IDirectory directory, CancellationToken cancellationToken = default)
    {
      cancellationToken.ThrowIfCancellationRequested();

      IVolume volume = directory.Volume;
      var volumeTmbDir = volume.ThumbnailDirectory;
      var volumeSeparator = volume.DirectorySeparatorChar;

      if (volumeTmbDir == null)
        return Task.FromResult(default(string));

      if (directory.FullName.StartsWith(volumeTmbDir + volumeSeparator))
        return Task.FromResult(directory.FullName);

      volumeTmbDir = PathHelper.GetFullPathNormalized(volumeTmbDir);
      string relativePath = volume.GetRelativePath(directory);
      string thumbDir = volumeTmbDir + relativePath;
      return Task.FromResult(thumbDir);
    }

    public async Task<(ImageWithMimeType Thumb, IFile ThumbFile, MediaType? MediaType)> CreateThumbAsync(IFile file,
        bool verify = true, CancellationToken cancellationToken = default)
    {
      cancellationToken.ThrowIfCancellationRequested();

      MediaType? mediaType = file.CanGetThumb(pictureEditor, videoEditor, verify);

      if (mediaType == null) return (null, null, mediaType);

      var thumbPath = await GenerateThumbPathAsync(file, cancellationToken: cancellationToken);

      if (thumbPath == null) return (null, null, mediaType);

      IVolume volume = file.Volume;
      IFile thumbFile = volume.Driver.CreateFile(thumbPath, volume);

      if (!await thumbFile.ExistsAsync)
      {
        if (mediaType == MediaType.Image)
        {
          ImageWithMimeType thumb = await thumbFile.CreateThumbAsync(
              file, volume.ThumbnailSize, pictureEditor, cancellationToken: cancellationToken);

          return (thumb, thumbFile, mediaType);
        }
        else if (mediaType == MediaType.Video)
        {
          ImageWithMimeType thumb = await thumbFile.CreateThumbAsync(
              file, volume.ThumbnailSize, videoEditor, cancellationToken: cancellationToken);

          return (thumb, thumbFile, mediaType);
        }
      }

      return (null, thumbFile, mediaType);
    }

    public IFile CreateFile(string fullPath, IVolume volume)
    {
      return new FileSystemFile(fullPath, volume);
    }

    public IDirectory CreateDirectory(string fullPath, IVolume volume)
    {
      return new FileSystemDirectory(fullPath, volume);
    }

    // Or use Path.GetFileName(name) to remove all paths and keep the fileName only.
    protected static bool IsObjectNameValid(string name)
    {
      return name == null || !name.Any(ch => _invalidFileNameChars.Contains(ch));
    }

    protected virtual async Task<IFile> SafeCopyToAsync(IFile file, string newDir,
        IVolume destVolume, bool copyOverwrite = true, string suffix = null,
        CancellationToken cancellationToken = default)
    {
      cancellationToken.ThrowIfCancellationRequested();

      if (!file.CanCopy()) throw new PermissionDeniedException();

      string newPath = PathHelper.SafelyCombine(newDir, newDir, file.Name);
      IFile newFile = new FileSystemFile(newPath, destVolume);
      var isOverwrite = true;

      if (File.Exists(newPath))
      {
        if (!copyOverwrite)
        {
          var newName = await newFile.GetCopyNameAsync(suffix, cancellationToken: cancellationToken);
          newPath = PathHelper.SafelyCombine(newDir, newDir, newName);
          isOverwrite = false;
        }
      }

      await OnBeforeCopy.SafeInvokeAsync(file, newPath, isOverwrite);
      newFile = await file.CopyToAsync(newPath, destVolume, copyOverwrite, cancellationToken: cancellationToken);
      await OnAfterCopy.SafeInvokeAsync(file, newFile, isOverwrite);

      return newFile;
    }

    protected virtual async Task<IFile> SafeMoveToAsync(IFile file, string newDir,
        IVolume destVolume, bool copyOverwrite = true, string suffix = null,
        CancellationToken cancellationToken = default)
    {
      cancellationToken.ThrowIfCancellationRequested();

      if (!file.CanMove()) throw new PermissionDeniedException();

      string newPath = PathHelper.SafelyCombine(newDir, newDir, file.Name);
      IFile newFile = new FileSystemFile(newPath, destVolume);

      if (await newFile.ExistsAsync)
      {
        if (!copyOverwrite)
        {
          var newName = await newFile.GetCopyNameAsync(suffix, cancellationToken: cancellationToken);
          newPath = PathHelper.SafelyCombine(newDir, newDir, newName);
        }
        else
        {
          await OnBeforeMove.SafeInvokeAsync(file, newPath, true);

          await OnBeforeCopy.SafeInvokeAsync(file, newPath, true);
          newFile = await file.CopyToAsync(newPath, destVolume, true, cancellationToken: cancellationToken);
          await OnAfterCopy.SafeInvokeAsync(file, newFile, true);

          await OnBeforeRemove.SafeInvokeAsync(file);
          await file.DeleteAsync(cancellationToken: cancellationToken);
          await OnAfterRemove.SafeInvokeAsync(file);

          await OnAfterMove.SafeInvokeAsync(file, newFile, true);

          return newFile;
        }
      }

      await OnBeforeMove.SafeInvokeAsync(file, newPath, false);
      newFile = await file.MoveToAsync(newPath, destVolume, cancellationToken: cancellationToken);
      await OnAfterMove.SafeInvokeAsync(file, newFile, false);

      return newFile;
    }

    protected virtual async Task<IDirectory> CopyToAsync(IDirectory directory, string newDest,
        IVolume destVolume, bool copyOverwrite, string suffix = null, CancellationToken cancellationToken = default)
    {
      cancellationToken.ThrowIfCancellationRequested();

      if (!directory.CanCopy()) throw new PermissionDeniedException();

      var destInfo = new FileSystemDirectory(newDest, destVolume);
      if (!await destInfo.CanCopyToAsync(cancellationToken: cancellationToken))
        throw new PermissionDeniedException();

      if (destInfo.FileExists())
        throw new ExistsException(destInfo.Name);

      var queue = new Queue<(IDirectory Dir, IDirectory Dest)>();
      queue.Enqueue((directory, destInfo));

      while (queue.Count > 0)
      {
        (IDirectory Dir, IDirectory Dest) currentItem = queue.Dequeue();
        IDirectory currentDir = currentItem.Dir;
        IDirectory currentNewDest = currentItem.Dest;

        if (!await currentNewDest.ExistsAsync)
        {
          await OnBeforeMakeDir.SafeInvokeAsync(currentNewDest);
          await currentNewDest.CreateAsync(cancellationToken: cancellationToken);
          await OnAfterMakeDir.SafeInvokeAsync(currentNewDest);
        }

        foreach (IDirectory dir in await currentDir.GetDirectoriesAsync(cancellationToken: cancellationToken))
        {
          var newDir = new FileSystemDirectory(PathHelper.SafelyCombine(
              currentNewDest.FullName, currentNewDest.FullName, dir.Name), currentNewDest.Volume);
          queue.Enqueue((dir, newDir));
        }

        foreach (IFile file in await currentDir.GetFilesAsync(cancellationToken: cancellationToken))
        {
          await SafeCopyToAsync(file, currentNewDest.FullName, destVolume, copyOverwrite, suffix, cancellationToken: cancellationToken);
        }
      }

      await destInfo.RefreshAsync(cancellationToken);
      return destInfo;
    }

    protected virtual async Task<IDirectory> MergeAsync(IDirectory srcDir, string newDest,
        IVolume destVolume, bool copyOverwrite, string suffix = null, CancellationToken cancellationToken = default)
    {
      cancellationToken.ThrowIfCancellationRequested();

      var destInfo = new FileSystemDirectory(newDest, destVolume);
      if (!await destInfo.CanMoveToAsync(cancellationToken: cancellationToken))
        throw new PermissionDeniedException();

      if (destInfo.FileExists())
        throw new ExistsException(destInfo.Name);

      var queue = new Queue<(IDirectory Dir, IDirectory Dest)>();
      queue.Enqueue((srcDir, destInfo));

      while (queue.Count > 0)
      {
        (IDirectory Dir, IDirectory Dest) currentItem = queue.Dequeue();
        IDirectory currentDir = currentItem.Dir;
        IDirectory currentNewDest = currentItem.Dest;

        if (!await currentNewDest.ExistsAsync)
        {
          await OnBeforeMakeDir.SafeInvokeAsync(currentNewDest);
          await currentNewDest.CreateAsync(cancellationToken: cancellationToken);
          await OnAfterMakeDir.SafeInvokeAsync(currentNewDest);
        }

        foreach (IDirectory dir in await currentDir.GetDirectoriesAsync(cancellationToken: cancellationToken))
        {
          var newDir = new FileSystemDirectory(PathHelper.SafelyCombine(
              currentNewDest.FullName, currentNewDest.FullName, dir.Name), currentNewDest.Volume);
          queue.Enqueue((dir, newDir));
        }

        foreach (IFile file in await currentDir.GetFilesAsync(cancellationToken: cancellationToken))
        {
          await SafeMoveToAsync(file, currentNewDest.FullName, destVolume, copyOverwrite, suffix, cancellationToken: cancellationToken);
        }
      }

      await destInfo.RefreshAsync(cancellationToken: cancellationToken);
      return destInfo;
    }

    protected virtual async Task AddParentsToListAsync(PathInfo pathInfo, List<object> list, CancellationToken cancellationToken = default)
    {
      cancellationToken.ThrowIfCancellationRequested();

      IDirectory currentDir = pathInfo.Directory;
      IVolume volume = pathInfo.Volume;
      string lastParentHash = null;

      do
      {
        currentDir = currentDir.Parent;

        var hash = lastParentHash ?? currentDir.GetHash(volume, pathParser);
        lastParentHash = currentDir.GetParentHash(volume, pathParser);

        foreach (IDirectory item in await currentDir.GetDirectoriesAsync(cancellationToken: cancellationToken))
        {
          var subHash = item.GetHash(volume, pathParser);
          list.Add(await item.ToFileInfoAsync(subHash, hash, volume, connector.Options, cancellationToken: cancellationToken));
        }
      }
      while (!volume.IsRoot(currentDir));
    }

    protected virtual async Task RemoveThumbsAsync(PathInfo path, CancellationToken cancellationToken = default)
    {
      cancellationToken.ThrowIfCancellationRequested();

      try
      {
        await OnBeforeRemoveThumb.SafeInvokeAsync(path);
        if (path.IsDirectory)
        {
          string thumbPath = await GenerateThumbPathAsync(path.Directory, cancellationToken: cancellationToken);
          if (!string.IsNullOrEmpty(thumbPath) && Directory.Exists(thumbPath))
          {
            Directory.Delete(thumbPath, true);
          }
        }
        else
        {
          string thumbPath = await GenerateThumbPathAsync(path.File, cancellationToken: cancellationToken);
          if (!string.IsNullOrEmpty(thumbPath) && File.Exists(thumbPath))
          {
            File.Delete(thumbPath);
          }
        }
        await OnAfterRemoveThumb.SafeInvokeAsync(path);
      }
      catch (Exception ex)
      {
        await OnRemoveThumbError.SafeInvokeAsync(ex);
      }
    }

    protected virtual async Task<string> GetInlineContentAsync(IFile file, StreamReader reader, CancellationToken cancellationToken = default)
    {
      cancellationToken.ThrowIfCancellationRequested();

      MimeType mime = MimeHelper.GetMimeType(file.Extension);

      if (mime.Type == ContentTypeNames.Text.Type)
        return reader.ReadToEnd();

      using var memStream = new MemoryStream();
      await reader.BaseStream.CopyToAsync(memStream, StreamConstants.DefaultBufferSize, cancellationToken: cancellationToken);
      var base64Str = Convert.ToBase64String(memStream.ToArray());
      var dataUri = $"data:{mime};charset={reader.CurrentEncoding.WebName};base64,{base64Str}";
      return dataUri;
    }

    protected virtual byte[] ParseDataURIScheme(string dataUri, string fromcmd)
    {
      var parts = dataUri.Split(',');
      if (parts.Length != 2)
        throw new CommandParamsException(fromcmd);

      return Convert.FromBase64String(parts[1]);
    }

    protected virtual async Task MergeChunksAsync(IFile destFile, IDirectory chunkingDir,
        bool isOverwrite, CancellationToken cancellationToken = default)
    {
      IFile[] files =
      [
        .. (await chunkingDir.GetFilesAsync(cancellationToken: cancellationToken))
                  .Where(f => f.Name != tempFileCleaner.Options.StatusFile)
                  .OrderBy(f => FileHelper.GetChunkInfo(f.Name).CurrentChunkNo),
      ];

      using (Stream fileStream = await destFile.OpenWriteAsync(cancellationToken: cancellationToken))
      {
        foreach (IFile file in files)
        {
          await WriteStatusFileAsync(chunkingDir);

          await OnBeforeChunkTransfer.SafeInvokeAsync(file, destFile, isOverwrite);
          using (Stream readStream = await file.OpenReadAsync(cancellationToken: cancellationToken))
          using (var memStream = new MemoryStream())
          {
            await readStream.CopyToAsync(memStream, cancellationToken);
            var bytes = memStream.ToArray();
            await fileStream.WriteAsync(bytes, cancellationToken);
          }
          await OnAfterChunkTransfer.SafeInvokeAsync(file, destFile, isOverwrite);

          await OnBeforeRemove.SafeInvokeAsync(file);
          await file.DeleteAsync(cancellationToken: cancellationToken);
          await OnAfterRemove.SafeInvokeAsync(file);
        }
      }

      await OnBeforeRemove.SafeInvokeAsync(chunkingDir);
      await chunkingDir.DeleteAsync(cancellationToken: cancellationToken);
      await OnAfterRemove.SafeInvokeAsync(chunkingDir);
    }

    private string GetChunkDirectory(IDirectory uploadDir, string uploadingFileName, string cid)
    {
      var bytes = Encoding.UTF8.GetBytes(uploadDir.FullName + uploadingFileName + cid);
      var signature = BitConverter.ToString(cryptographyProvider.HMACSHA1ComputeHash(
          nameof(GetChunkDirectory), bytes)).Replace("-", string.Empty);
      var tempFileName = $"{ChunkingFolderPrefix}_{signature}";
      var tempDest = PathHelper.SafelyCombine(uploadDir.Volume.ChunkDirectory, uploadDir.Volume.ChunkDirectory, tempFileName);
      return tempDest;
    }

    private async Task WriteStatusFileAsync(IDirectory directory)
    {
      try
      {
        if (await directory.ExistsAsync)
        {
          var statusFile = PathHelper.SafelyCombine(directory.FullName, directory.FullName, tempFileCleaner.Options.StatusFile);
          using FileStream file = File.OpenWrite(statusFile);
        }
      }
      catch (Exception) { }
    }

    class ChunkedUploadInfo : ConnectorLock
    {
      public Exception Exception { get; set; }
      public int TotalUploaded { get; set; }
      public bool IsFileTouched { get; set; }
    }
  }
}
