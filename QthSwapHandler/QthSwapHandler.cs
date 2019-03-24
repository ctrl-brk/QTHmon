using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Net.Http;

namespace QTHmon
{
    public partial class QthSwapHandler : IQthSwapHandler
    {
        private class ScanInfo
        {
            public DateTime Date { get; set; }
            public List<int> Ids { get; set; }
            [NonSerialized]
            public List<int> OtherIds { get; set; }
        }

        private readonly Uri _qthUri;
        private readonly HttpClientHandler _httpHandler;
        private readonly ILogger _logger;
        private readonly AppSettings _settings;

        private ScanInfo _lastKeywordScan = new ScanInfo { Date = DateTime.MinValue, Ids = new List<int>(), OtherIds = new List<int>() };
        private ScanInfo _lastCategoryScan = new ScanInfo { Date = DateTime.MinValue, Ids = new List<int>(), OtherIds = new List<int>() };
        private ScanInfo _thisScan;
        private List<Post> _newPosts;

        // ReSharper disable once SuggestBaseTypeForParameter
        public QthSwapHandler(ILogger<HostedService> logger, IOptions<AppSettings> settings)
        {
            _logger = logger;
            _settings = settings.Value;
            _qthUri = new Uri("https://swap.qth.com/advsearchresults.php");
            _httpHandler = new HttpClientHandler();
        }
    }
}
