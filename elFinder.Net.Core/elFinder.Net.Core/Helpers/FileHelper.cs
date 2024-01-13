using System.Linq;

namespace elFinder.Net.Core.Helpers
{
    public static class FileHelper
    {
        public static (string UploadingFileName, int CurrentChunkNo, int TotalChunks) GetChunkInfo(string chunkName)
        {
            var fileName = chunkName[..chunkName.LastIndexOf('.')];
            fileName = fileName[..fileName.LastIndexOf('.')];
            var fileParts = chunkName.Split('.');
            var chunkInfo = fileParts[^2].Split('_');
            var totalChunks = int.Parse(chunkInfo[^1]) + 1;
            var chunkNo = int.Parse(chunkInfo[0]);
            return (fileName, chunkNo, totalChunks);
        }

        public static (long StartByte, long ChunkLength, long TotalBytes) GetRangeInfo(string range)
        {
            var rangeParts = range.Split(',').Select(r => long.Parse(r)).ToArray();

            return (rangeParts[0], rangeParts[1], rangeParts[2]);
        }
    }
}
