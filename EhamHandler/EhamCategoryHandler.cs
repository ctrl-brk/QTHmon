using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

// ReSharper disable once CheckNamespace
namespace QTHmon
{
    public partial class EhamHandler
    {
        public async Task<IEnumerable<ScanResult>> ProcessCategoriesAsync(HttpClient httpClient, CookieContainer cookies, CancellationToken token)
        {
            if (_settings.EhamNet.CategorySearch.MaxPosts <= 0) return null;

            _thisScan = new ScanInfo { Ids = new List<int>() };
#if DEBUG && CLEARHIST
            File.Delete(_settings.EhamNet.CategorySearch.ResultFile);
#endif
            if (File.Exists(_settings.EhamNet.CategorySearch.ResultFile))
            {
                _lastCategoryScan = JsonConvert.DeserializeObject<ScanInfo>(File.ReadAllText(_settings.EhamNet.CategorySearch.ResultFile));
                _lastCategoryScan.OtherIds = new List<int>(_lastKeywordScan.Ids);
            }

            var res = new List<ScanResult>();

            foreach (var category in _settings.EhamNet.CategorySearch.Categories.Split(','))
            {
                if (token.IsCancellationRequested) break;
                _newPosts = new List<Post>();
                res.Add(await ProcessCategory(httpClient, category, cookies, token));
            }

            return res;
        }

        private async Task<ScanResult> ProcessCategory(HttpClient httpClient, string category, CookieContainer cookies, CancellationToken token)
        {
            _logger.LogDebug($"Fetching {category} category from eHam.net");

            var uri = new Uri($"https://www.eham.net/classifieds/results/{_categories.First(x => x.Key == category.ToLower()).Value}");

            var sessionCookie = await GetSessionCookie(httpClient, cookies);

            var message = new HttpRequestMessage(HttpMethod.Get, uri);
            message.Headers.Add("Cache-Control", "no-cache");
            message.Headers.Add("Cookie", $"{sessionCookie.Name}={sessionCookie.Value}");

            var res = await httpClient.SendAsync(message);
            if (token.IsCancellationRequested) return null;
            var msg = await res.Content.ReadAsStringAsync();
            if (!await ScanResults(msg, ScanType.Category, httpClient)) return null;

            _newPosts.ForEach(x => _thisScan.Ids.Add(x.Id));

#if SAVEHIST
            if (_newPosts.Count > 0)
                File.WriteAllText(_settings.EhamNet.CategorySearch.ResultFile, JsonConvert.SerializeObject(_thisScan));
#endif
            return BuildResults(_lastCategoryScan);
        }
    }
}
