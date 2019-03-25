using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;

// ReSharper disable once CheckNamespace
namespace QTHmon
{
    public partial class QthSwapHandler : IQthSwapHandler
    {
        private enum ScanType
        {
            Keyword,
            Category
        }

        private class ScanInfo
        {
            public DateTime Date { get; set; }
            public List<int> Ids { get; set; }
            [NonSerialized]
            public List<int> OtherIds;
        }

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
            _httpHandler = new HttpClientHandler();
        }

        private bool ScanResults(string msg, ScanType scanType)
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

            var lastScan = scanType == ScanType.Keyword ? _lastKeywordScan : _lastCategoryScan;

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

                var post = ProcessPost(msg.Substring(adStartIndex, adEndIndex - adStartIndex), scanType);

                if (post.ActivityDate < lastScan.Date) return false;
                if (post.ActivityDate == lastScan.Date && lastScan.Ids.IndexOf(post.Id) >= 0) return false;

                if (lastScan.OtherIds != null && lastScan.OtherIds.IndexOf(post.Id) < 0)  //ignore if listed in other searches before
                {
                    _newPosts.Add(post);

                    if (_thisScan.Date == DateTime.MinValue)
                        _thisScan.Date = post.ActivityDate;
                }

            } while (adStartIndex > 0);

            return true;
        }

        private static Post ProcessPost(string html, ScanType scanType)
        {
            const string CATEGORY = "<font size=2 face=arial color=0000FF>";
            var TITLE = scanType == ScanType.Keyword ? "<font size=2 face=arial> - " : "<font size=2 face=arial COLOR=\"#0000FF\">";
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
                Category = GetValue(html, CATEGORY, END, ref index, false),
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
            if (!string.IsNullOrEmpty(post.Category)) post.Category = post.Category.ToLower();

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

        private ScanResult BuildResults(ScanInfo lastScan)
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
                if (post.Category != null) sb.AppendLine($"<a class='cat' href='https://swap.qth.com/c_{post.Category}.php' target='_blank'>{post.Category}</a>");

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

            return new ScanResult { Title = _settings.SwapQthCom.Title, Items = _newPosts.Count, LastScan = lastScan.Date, Html = sb.ToString() };
        }
    }
}
