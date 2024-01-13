namespace elFinder.Net.Core.Models.Command
{
    public sealed class GetCommand : TargetCommand
    {
        public string Current { get; set; }
        public string Conv { get; set; }

        public PathInfo CurrentPath { get; set; }
    }
}
