using Microsoft.Extensions.Logging;
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
        public async Task<IEnumerable<ScanResult>> ProcessCategoriesAsync(CancellationToken token)
        {
            _thisScan = new ScanInfo {Ids = new List<int>()};
            _newPosts = new List<Post>();
#if DEBUG
            File.Delete(_settings.SwapQthCom.CategorySearch.ResultFile);
#endif
            if (_settings.SwapQthCom.CategorySearch.MaxPages <= 0) return null;

            if (File.Exists(_settings.SwapQthCom.CategorySearch.ResultFile))
            {
                _lastCategoryScan =
                    JsonConvert.DeserializeObject<ScanInfo>(
                        File.ReadAllText(_settings.SwapQthCom.CategorySearch.ResultFile));
                _lastCategoryScan.OtherIds = new List<int>();
            }

            _lastCategoryScan.OtherIds.AddRange(_lastKeywordScan.Ids);

            var res = new List<ScanResult>();

            foreach (var category in _settings.SwapQthCom.CategorySearch.Categories.Split(new[] {' ', ','}))
            {
                if (token.IsCancellationRequested) break;
                res.Add(await ProcessCategory(category, token));
            }

            return res;
        }

        private async Task<ScanResult> ProcessCategory(string category, CancellationToken token)
        {
            var pageNum = 0;

            using (var hc = new HttpClient(_httpHandler))
            {
                while (pageNum < _settings.SwapQthCom.CategorySearch.MaxPages)
                {
                    var uri = new Uri($"https://swap.qth.com/c_{category}.php?page={pageNum+1}");

                    _logger.LogDebug($"Fetching {category} page {pageNum + 1} of maximum {_settings.SwapQthCom.CategorySearch.MaxPages}");
                    var res = await hc.GetAsync(uri, token);
                    if (token.IsCancellationRequested) break;
                    var msg = await res.Content.ReadAsStringAsync();
                    pageNum++;
                    if (!ScanResults(msg, ScanType.Category)) break;
                }
            }

            _newPosts.ForEach(x => _thisScan.Ids.Add(x.Id));

            if (_newPosts.Count > 0)
                File.WriteAllText(_settings.SwapQthCom.CategorySearch.ResultFile, JsonConvert.SerializeObject(_thisScan));

            return BuildResults(_lastCategoryScan);
        }

        private static Post ProcessCategoryPost(string html)
        {
            const string CATEGORY = "<font size=\"2\" face=\"arial\" color=\"#0000FF\">";
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
