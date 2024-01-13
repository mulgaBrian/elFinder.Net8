using elFinder.Net.Core.Models.Response;

namespace elFinder.Net.Core.Exceptions
{
    public sealed class UnknownCommandException : ConnectorException
    {
        public UnknownCommandException()
        {
            ErrorResponse = new ErrorResponse(this)
            {
                error = ErrorResponse.UnknownCommand
            };
        }
    }
}
