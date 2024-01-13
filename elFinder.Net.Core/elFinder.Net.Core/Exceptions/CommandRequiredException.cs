using elFinder.Net.Core.Models.Response;

namespace elFinder.Net.Core.Exceptions
{
    public sealed class CommandRequiredException : ConnectorException
    {
        public CommandRequiredException()
        {
            ErrorResponse = new ErrorResponse(this)
            {
                error = ErrorResponse.CommandRequired
            };
        }
    }
}
