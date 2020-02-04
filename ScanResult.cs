using System;
using System.Diagnostics;

namespace QTHmon
{
    [DebuggerDisplay("Title={Title}, Count={Items}, LastScan={LastScan}")]
    public class ScanResult
    {
        public int Items { get; set; }
        public DateTime LastScan { get; set; }
        public string Title { get; set; }
        public string Html { get; set; }
    }
}
