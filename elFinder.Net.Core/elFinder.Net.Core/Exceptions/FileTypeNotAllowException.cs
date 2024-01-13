using elFinder.Net.Core.Models.Response;

namespace elFinder.Net.Core.Exceptions
{
    public sealed class FileTypeNotAllowException : ConnectorException
    {
        public FileTypeNotAllowException()
        {
            ErrorResponse = new ErrorResponse(this)
            {
                error = ErrorResponse.UploadMime
            };
        }
    }
}
