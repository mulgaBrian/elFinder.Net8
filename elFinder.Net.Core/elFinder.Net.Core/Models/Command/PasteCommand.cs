﻿using Microsoft.Extensions.Primitives;
using System.Collections.Generic;

namespace elFinder.Net.Core.Models.Command
{
    public sealed class PasteCommand : TargetsCommand
    {
        public PasteCommand()
        {
            Hashes = [];
        }

        public string Dst { get; set; }
        public byte Cut { get; set; }
        public StringValues Renames { get; set; }
        public string Suffix { get; set; } = "~";
        public Dictionary<string, string> Hashes { get; set; }

        public PathInfo DstPath { get; set; }
    }
}
