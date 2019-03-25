﻿using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

// ReSharper disable once CheckNamespace
namespace QTHmon
{
    public partial class QthSwapHandler
    {
        public async Task<ScanResult> ProcessKeywordsAsync(CancellationToken token)
        {
            _thisScan = new ScanInfo { Ids = new List<int>(), OtherIds = new List<int>() };
            _newPosts = new List<Post>();
#if DEBUG
            File.Delete(_settings.SwapQthCom.KeywordSearch.ResultFile);
#endif
            if (_settings.SwapQthCom.KeywordSearch.MaxPages <= 0) return null;

            int startIndex = 0, pageNum = 0;
            var uri = new Uri("https://swap.qth.com/advsearchresults.php");
            var handler = new HttpClientHandler();

            if (File.Exists(_settings.SwapQthCom.KeywordSearch.ResultFile))
                _lastKeywordScan = JsonConvert.DeserializeObject<ScanInfo>(File.ReadAllText(_settings.SwapQthCom.KeywordSearch.ResultFile));

            using (var hc = new HttpClient(handler))
            {
                while (pageNum < _settings.SwapQthCom.KeywordSearch.MaxPages)
                {
                    var formData = new List<KeyValuePair<string, string>>
                    {
                        new KeyValuePair<string, string>("anywords", _settings.SwapQthCom.KeywordSearch.Keywords)
                    };

                    if (startIndex > 0)
                    {
                        formData.AddRange(new[]
                         {
                             new KeyValuePair<string, string>("startnum", startIndex.ToString()),
                             new KeyValuePair<string, string>("submit", "Next 10 Ads")
                         });
                    }

                    var content = new FormUrlEncodedContent(formData);

                    _logger.LogDebug($"Fetching \"{_settings.SwapQthCom.KeywordSearch.Keywords}\" page {pageNum + 1} of maximum {_settings.SwapQthCom.KeywordSearch.MaxPages}");
                    var res = await hc.PostAsync(uri, content, token);
                    if (token.IsCancellationRequested) break;
                    var msg = await res.Content.ReadAsStringAsync();
                    startIndex += 10;
                    pageNum++;
                    if (!ScanResults(msg, ScanType.Keyword)) break;
                }
            }

            _newPosts.ForEach(x => _thisScan.Ids.Add(x.Id));

            if (_newPosts.Count > 0)
                File.WriteAllText(_settings.SwapQthCom.KeywordSearch.ResultFile, JsonConvert.SerializeObject(_thisScan));

            return BuildResults(_lastKeywordScan);
        }

        private static Post ProcessKeywordPost(string html)
        {
            const string CATEGORY = "<font size=2 face=arial color=0000FF>";
            const string TITLE = "<font size=2 face=arial> - ";
            const string DESC = "<DD><font size=2 face=arial>";
            const string END = "</font>";
            const string ID = "<DD><font size=1 face=arial>Listing #";
            const string SUBMIT = "Submitted on ";
            const string MODIFIED = "Modified on ";
            const string IMAGE = "camera_icon.gif";

            var index = 0;
            var post = new Post
            {
                IsNew = html[0] == '2',
                Category = GetValue(html, CATEGORY, END, ref index).ToLower(),
                Title = GetValue(html, TITLE, END, ref index),
                HasImage = html.IndexOf(IMAGE, index, StringComparison.Ordinal) > 0,
                Description = HighlightPrices(GetValue(html, DESC, END, ref index)),
                Id = int.Parse(GetValue(html, ID, " -  ", ref index)),
                SubmittedOn = DateTime.Parse(GetValue(html, SUBMIT, " by ", ref index)),
                CallSign = GetCallSign(html, ref index)
            };

            DateTime.TryParse(GetValue(html, MODIFIED, " - IP:", ref index, false), out var dt);
            post.ModifiedOn = dt == DateTime.MinValue ? (DateTime?)null : dt;

            post.Price = GetPrice(post);

            return post;
        }
    }
}