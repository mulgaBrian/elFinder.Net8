using elFinder.Net.Core.Models.Response;

namespace elFinder.Net.Core.Exceptions
{
    public sealed class ArchiveTypeException : ConnectorException
    {
        public ArchiveTypeException()
        {
            ErrorResponse = new ErrorResponse(this)
            {
                error = ErrorResponse.ArchiveType
            };
        }
    }
}
