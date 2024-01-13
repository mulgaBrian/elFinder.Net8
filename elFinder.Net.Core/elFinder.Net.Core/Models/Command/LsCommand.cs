using Microsoft.Extensions.Primitives;

namespace elFinder.Net.Core.Models.Command
{
    public sealed class LsCommand : TargetCommand
    {
        public StringValues Intersect { get; set; }
        public StringValues Mimes { get; set; }
    }
}
