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
    public partial class QthHandler
    {
        public async Task<IEnumerable<ScanResult>> ProcessCategoriesAsync(HttpClient httpClient, CookieContainer cookies, CancellationToken token)
        {
            if (_settings.QthCom.CategorySearch.MaxPosts <= 0) return null;

            _thisScan = new ScanInfo {Ids = new List<int>()};
#if DEBUG && CLEARHIST
            File.Delete(_settings.QthCom.CategorySearch.ResultFile);
#endif
            if (File.Exists(_settings.QthCom.CategorySearch.ResultFile))
            {
                _lastCategoryScan = JsonConvert.DeserializeObject<ScanInfo>(File.ReadAllText(_settings.QthCom.CategorySearch.ResultFile));
                _lastCategoryScan.OtherIds = new List<int>(_lastKeywordScan.Ids);
            }

            var res = new List<ScanResult>();

            foreach (var category in _settings.QthCom.CategorySearch.Categories.Split(','))
            {
                if (token.IsCancellationRequested) break;
                _newPosts = new List<Post>();
                res.Add(await ProcessCategory(httpClient, category, token));
            }

            return res;
        }

        private async Task<ScanResult> ProcessCategory(HttpClient httpClient, string category, CancellationToken token)
        {
            var postNum = 0;

            while (postNum < _settings.QthCom.CategorySearch.MaxPosts)
            {
                var uri = new Uri($"https://swap.qth.com/c_{category}.php?page={postNum/PAGE_SIZE + 1}");

                _logger.LogDebug($"Fetching {category} page {postNum/PAGE_SIZE + 1} of maximum {_settings.QthCom.CategorySearch.MaxPosts/PAGE_SIZE} from qth.com");
                var message = new HttpRequestMessage(HttpMethod.Get, uri);
                message.Headers.Add("Cache-Control", "no-cache");
                var res = await httpClient.SendAsync(message, token);
                if (token.IsCancellationRequested) break;
                var msg = await res.Content.ReadAsStringAsync();
                postNum += PAGE_SIZE;
                if (!await ScanResults(msg, ScanType.Category, httpClient)) break;
            }

            _newPosts.ForEach(x => _thisScan.Ids.Add(x.Id));

#if !DEBUG || SAVEHIST
            if (_newPosts.Count > 0)
                File.WriteAllText(_settings.QthCom.CategorySearch.ResultFile, JsonConvert.SerializeObject(_thisScan));
#endif
            return BuildResults(_lastCategoryScan);
        }
    }
}
