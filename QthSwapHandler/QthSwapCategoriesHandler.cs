using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace QTHmon
{
    public partial class QthSwapHandler
    {
        public async Task<ScanResult> ProcessCategoriesAsync(CancellationToken token)
        {
            _thisScan = new ScanInfo { Ids = new List<int>() };
            _newPosts = new List<Post>();

#if DEBUG
            File.Delete(_settings.SwapQthCom.CategorySearch.ResultFile);
#endif
            if (_settings.SwapQthCom.CategorySearch.MaxPages <= 0) return null;

            int startIndex = 0, pageNum = 0;

            if (File.Exists(_settings.SwapQthCom.CategorySearch.ResultFile))
            {
                _lastCategoryScan = JsonConvert.DeserializeObject<ScanInfo>(File.ReadAllText(_settings.SwapQthCom.CategorySearch.ResultFile));
                _lastCategoryScan.OtherIds = new List<int>();
            }

            _lastCategoryScan.OtherIds.AddRange(_lastKeywordScan.Ids);

            using (var hc = new HttpClient(_httpHandler))
            {
                while (pageNum < _settings.SwapQthCom.CategorySearch.MaxPages)
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

                    _logger.LogDebug($"Fetching page {pageNum + 1} of maximum {_settings.SwapQthCom.KeywordSearch.MaxPages}");
                    var res = await hc.PostAsync(uri, content, token);
                    if (token.IsCancellationRequested) break;
                    var msg = await res.Content.ReadAsStringAsync();
                    startIndex += 10;
                    pageNum++;
                    if (!ScanResults(msg)) break;
                }
            }

            _newPosts.ForEach(x => _thisScan.Ids.Add(x.Id));

            if (_newPosts.Count > 0)
                File.WriteAllText(_settings.SwapQthCom.KeywordSearch.ResultFile, JsonConvert.SerializeObject(_thisScan));

            return BuildResults();
        }

        private bool ScanResults(string msg)
        {
            // ReSharper disable InconsistentNaming
            const string NO_ADS = "There were no ads that matched your search";
            const string START = "Displaying ads ";
            const string TO = " to ";
            const string OF = " of ";
            const string END = " ads ";
            const string AD_START = "<DT><IMG SRC=\"https://swap.qth.com/mdoc";
            const string AD_END = "<br /><br />";
            // ReSharper restore InconsistentNaming

            if (msg.IndexOf(NO_ADS, StringComparison.Ordinal) > 0) return false;

            var from = msg.IndexOf(START, StringComparison.Ordinal);
            if (from < 0) throw new ApplicationException("Invalid response format");

            var to = msg.IndexOf(TO, from + START.Length, StringComparison.Ordinal);
            if (to < 0 || to - (from + START.Length) > 6) throw new ApplicationException("Invalid response format");

            var of = msg.IndexOf(OF, to + TO.Length, StringComparison.Ordinal);
            if (of < 0 || of - (to + TO.Length) > 6) throw new ApplicationException("Invalid response format");

            var ads = msg.IndexOf(END, of + OF.Length, StringComparison.Ordinal);
            if (ads < 0 || ads - (of + OF.Length) > 6) throw new ApplicationException("Invalid response format");

            var arr = msg.Substring(from + START.Length, ads - (from + START.Length)).Split(' ');

            var startAd = int.Parse(arr[0]);
            //var endAd = int.Parse(arr[2]);
            var totalAds = int.Parse(arr[4]);

            if (startAd > totalAds) return false;

            var adStartIndex = 0;
            var cnt = 0;

            do
            {
                adStartIndex = msg.IndexOf(AD_START, adStartIndex, StringComparison.Ordinal);

                if (adStartIndex < 0)
                {
                    if (cnt == 0) throw new ApplicationException("Invalid response format");
                    break;
                }

                cnt++;

                adStartIndex += AD_START.Length;

                var adEndIndex = msg.IndexOf(AD_END, adStartIndex, StringComparison.Ordinal);
                if (adEndIndex < 0) throw new ApplicationException("Invalid response format");

                var post = ProcessPost(msg.Substring(adStartIndex, adEndIndex - adStartIndex));

                if (post.ActivityDate < _lastScan.Date) return false;
                if (post.ActivityDate == _lastScan.Date && _lastScan.Ids.IndexOf(post.Id) >= 0) return false;

                _newPosts.Add(post);

                if (_thisScan.Date == DateTime.MinValue)
                    _thisScan.Date = post.ActivityDate;

            } while (adStartIndex > 0);

            return true;
        }

        private static Post ProcessPost(string html)
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

        private static string GetValue(string src, string startToken, string endToken, ref int index, bool req = true)
        {
            var start = src.IndexOf(startToken, index, StringComparison.Ordinal);
            if (start < 0)
            {
                if (!req) return null;
                throw new ApplicationException("Invalid response format");
            }
            var end = src.IndexOf(endToken, start + startToken.Length, StringComparison.Ordinal);
            if (end < 0) throw new ApplicationException("Invalid response format");

            index = end + endToken.Length;
            return src.Substring(start + startToken.Length, end - (start + startToken.Length));
        }

        private static string GetCallSign(string src, ref int index)
        {
            var call = GetValue(src, "Callsign <a", "</a>", ref index, false);
            if (call == null) return null;
            var ind = call.IndexOf('>');
            return call.Substring(ind + 1);
        }

        private static string GetPrice(Post post)
        {
            var value = "";
            var cnt = 0;

            var ind = post.Description.IndexOf('$');
            if (ind < 0) return null;
            ind++;
            while (cnt < 15 && ind < post.Description.Length && (char.IsNumber(post.Description[ind]) || new[] { ' ', ',', '.' }.Contains(post.Description[ind])))
            {
                value += post.Description[ind++];
                cnt++;
            }

            return value.Length > 0 ? value : null;
        }

        private static string HighlightPrices(string text)
        {
            var prevInd = 0;
            var ind = text.IndexOf('$');

            if (ind < 0) return text;

            var result = new StringBuilder();

            while (ind >= 0)
            {
                result.Append(text.Substring(prevInd, ind - prevInd));
                result.Append("<span class='price'>$");
                ind++;
                var cnt = 0;
                for (; cnt < 15 && ind < text.Length && (char.IsNumber(text[ind]) || new[] { ' ', ',', '.' }.Contains(text[ind])); cnt++)
                {
                    result.Append(text[ind++]);
                }
                result.Append("</span>");

                prevInd = ind;

                if (ind + cnt >= text.Length) break;

                ind = text.IndexOf('$', ind + cnt);
            }

            if (ind < text.Length)
                result.Append(text.Substring(prevInd));

            return result.ToString();
        }

        private ScanResult BuildResults()
        {
            if (_newPosts.Count == 0)
            {
                _logger.LogDebug("No new posts found");
                return null;
            }

            var sb = new StringBuilder();

            foreach (var post in _newPosts)
            {
                sb.AppendLine("<table>");
                sb.AppendLine("  <tr>");
                sb.AppendLine("    <td rowspan='3' class='thumb'>");
                if (post.HasImage) sb.AppendLine($"    <a href='https://swap.qth.com/view_ad.php?counter={post.Id}' target='_blank'><img src='https://swap.qth.com/segamida/thumb_{post.Id}.jpg'></a>");
                sb.AppendLine("    </td>");
                sb.AppendLine("    <td class='title'>");

                sb.Append($"      <a class='link' href='https://swap.qth.com/view_ad.php?counter={post.Id}' target='_blank'>{post.Title}</a>");
                if (post.Price != null) sb.Append($"&nbsp;&nbsp;&nbsp;${post.Price}");
                sb.AppendLine($"<a class='cat' href='https://swap.qth.com/c_{post.Category}.php' target='_blank'>{post.Category}</a>");

                sb.AppendLine("    </td>");
                sb.AppendLine("  </tr>");
                sb.AppendLine("  <tr class='content'>");
                sb.AppendLine("    <td>");
                sb.AppendLine($"      {post.Description}");
                sb.AppendLine("    </td>");
                sb.AppendLine("  </tr>");
                sb.AppendLine("  <tr>");
                sb.AppendLine("    <td class='info'>");

                sb.Append("      Submitted ");
                if (post.CallSign != null)
                    sb.Append($"by <a class='call' href='https://www.qrz.com/lookup?tquery={post.CallSign}&mode=callsign' target='_blank'>{post.CallSign}</a> ");
                sb.Append($"on {post.SubmittedOn:d}");
                if (post.ModifiedOn.HasValue) sb.Append($" Modified: <span class='modified'>{post.ModifiedOn:d}</span>");
                sb.AppendLine("");

                sb.AppendLine("    </td>");
                sb.AppendLine("  </tr>");
                sb.AppendLine("</table>\n");
            }

            //File.WriteAllText("msg.html", sb.ToString());
            //return;

            return new ScanResult { Title = _settings.SwapQthCom.Title, Items = _newPosts.Count, LastScan = _lastScan.Date, Html = sb.ToString() };
        }
    }
}
