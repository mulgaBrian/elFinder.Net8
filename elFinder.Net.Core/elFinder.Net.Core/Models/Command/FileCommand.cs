namespace elFinder.Net.Core.Models.Command
{
    public sealed class FileCommand : TargetCommand
    {
        public byte Download { get; set; }
        public string ReqId { get; set; }
        public string CPath { get; set; }
    }
}
