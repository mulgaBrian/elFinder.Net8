using System.Collections.Generic;

namespace elFinder.Net.Core.Plugins
{
    public sealed class PluginCollection
    {
        public PluginCollection()
        {
            Captures = new List<PluginCapture>();
        }

        public List<PluginCapture> Captures { get; }
    }
}
