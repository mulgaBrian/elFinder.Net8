using elFinder.Net.Core.Models.Response;

namespace elFinder.Net.Core.Exceptions
{
    public sealed class InvalidDirNameException : ConnectorException
    {
        public InvalidDirNameException()
        {
            ErrorResponse = new ErrorResponse(this)
            {
                error = ErrorResponse.InvalidDirName
            };
        }
    }
}
