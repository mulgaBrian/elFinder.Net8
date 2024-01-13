namespace elFinder.Net.Core.Models.Command
{
    public sealed class PutCommand : TargetCommand
    {
        public string Content { get; set; }
        public string Encoding { get; set; }

        public PathInfo ContentPath { get; set; }
    }
}
