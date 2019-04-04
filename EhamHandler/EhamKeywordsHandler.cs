using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

// ReSharper disable once CheckNamespace
namespace QTHmon
{
    public partial class EhamHandler
    {
        public async Task<ScanResult> ProcessKeywordsAsync(HttpClient httpClient, CookieContainer cookies, CancellationToken token)
        {
            _thisScan = new ScanInfo { Ids = new List<int>(), OtherIds = new List<int>() };
            _newPosts = new List<Post>();
#if DEBUG && CLEARHIST
            File.Delete(_settings.EhamNet.KeywordSearch.ResultFile);
#endif
            if (_settings.EhamNet.KeywordSearch.MaxPosts <= 0) return null;

            var postNum = 0;

            if (File.Exists(_settings.EhamNet.KeywordSearch.ResultFile))
            {
                _lastKeywordScan = JsonConvert.DeserializeObject<ScanInfo>(File.ReadAllText(_settings.EhamNet.KeywordSearch.ResultFile));
                _lastKeywordScan.OtherIds = new List<int>();
            }

            var sessionCookie = await GetSessionCookie(httpClient, cookies);

            while (postNum < _settings.EhamNet.KeywordSearch.MaxPosts)
            {
                _logger.LogDebug($"Fetching \"{_settings.EhamNet.KeywordSearch.Keywords}\" page {postNum / PAGE_SIZE + 1} of maximum {_settings.EhamNet.KeywordSearch.MaxPosts / PAGE_SIZE} from eHam.net");

                var uri = new Uri($"https://www.eham.net/classifieds/?view=detail&page={postNum / PAGE_SIZE + 1}");

                var message = new HttpRequestMessage(HttpMethod.Get, uri);
                message.Headers.Add("Cache-Control", "no-cache");
                message.Headers.Add("Cookie", $"{sessionCookie.Name}={sessionCookie.Value}");
                var res = await httpClient.SendAsync(message);

                //var res = await httpClient.GetAsync(uri, token);
                if (token.IsCancellationRequested) break;

                var msg = await res.Content.ReadAsStringAsync();
                postNum += PAGE_SIZE;
                if (!await ScanResults(msg, ScanType.Keyword, httpClient)) break;
            }

            _newPosts.ForEach(x => _thisScan.Ids.Add(x.Id));

#if !DEBUG || SAVEHIST
            if (_newPosts.Count > 0)
                File.WriteAllText(_settings.EhamNet.KeywordSearch.ResultFile, JsonConvert.SerializeObject(_thisScan));
#endif

            return BuildResults(_lastKeywordScan);
        }
    }
}
