namespace elFinder.Net.Core.Models.Command
{
    public sealed class ArchiveCommand : TargetsCommand
    {
        public string Target { get; set; }
        public string Name { get; set; }
        public string Type { get; set; }

        public PathInfo TargetPath { get; set; }
    }
}
