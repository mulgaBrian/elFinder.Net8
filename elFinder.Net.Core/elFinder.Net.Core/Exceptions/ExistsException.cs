using elFinder.Net.Core.Models.Response;

namespace elFinder.Net.Core.Exceptions
{
    public sealed class ExistsException : ConnectorException
    {
        public ExistsException(string name)
        {
            Name = name;
            ErrorResponse = new ErrorResponse(this)
            {
                error = new[] { ErrorResponse.Exists, name }
            };
        }

        public string Name { get; }
    }
}
