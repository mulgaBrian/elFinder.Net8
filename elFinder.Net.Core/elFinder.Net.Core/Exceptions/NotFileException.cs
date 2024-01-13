using elFinder.Net.Core.Models.Response;

namespace elFinder.Net.Core.Exceptions
{
    public sealed class NotFileException : ConnectorException
    {
        public NotFileException()
        {
            ErrorResponse = new ErrorResponse(this)
            {
                error = ErrorResponse.NotFile
            };
        }
    }
}
