using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Mail;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace QTHmon
{
    public class HostedService : IHostedService
    {
        private readonly ILogger _logger;
        private readonly IHostApplicationLifetime _appLifeTime;
        private readonly AppSettings _settings;
        private readonly IQthHandler _qthHandler;
        private readonly IEhamHandler _ehamHandler;
        private readonly CookieContainer _cookies;

        private HttpClientHandler _httpClientHandler;
        private HttpClient _httpClient;

        private Task _task;
        private CancellationTokenSource _cts;

        public HostedService(ILogger<HostedService> logger, IHostApplicationLifetime appLifeTime, IOptions<AppSettings> settings, IQthHandler qthHandler, IEhamHandler ehamHandler)
        {
            _logger = logger;
            _appLifeTime = appLifeTime;
            _settings = settings.Value;
            _qthHandler = qthHandler;
            _ehamHandler = ehamHandler;
            _cookies = new CookieContainer();

            if (string.IsNullOrEmpty(_settings.ResourceFolder)) _settings.ResourceFolder = ".";
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting");

            _httpClientHandler = new HttpClientHandler {UseCookies = false, CookieContainer = _cookies};
            _httpClient = new HttpClient(_httpClientHandler);

            // Create a linked token so we can trigger cancellation outside of this token's cancellation
            _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            _task = MonitorAsync(_cts.Token);

            // If the task is completed then return it, otherwise it's running
            return _task.IsCompleted ? _task : Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Stopping");
            _httpClient.Dispose();
            _httpClientHandler.Dispose();
            return Task.CompletedTask;
        }

        private async Task MonitorAsync(CancellationToken token)
        {
            var results = new List<ScanResult>();

            var keyRes = await _qthHandler.ProcessKeywordsAsync(_httpClient, null, token);
            if (keyRes != null)
                results.Add(keyRes);

            var catRes = await _qthHandler.ProcessCategoriesAsync(_httpClient, null, token);
            if (catRes != null)
                results.AddRange(catRes.Where(x => x != null));

            keyRes = await _ehamHandler.ProcessKeywordsAsync(_httpClient, _cookies, token);
            if (keyRes != null)
                results.Add(keyRes);

            catRes = await _ehamHandler.ProcessCategoriesAsync(_httpClient, _cookies, token);
            if (catRes != null)
                results.AddRange(catRes.Where(x => x != null));

            SendResults(results);
            _appLifeTime.StopApplication();
        }

        private void SendResults(IEnumerable<ScanResult> results)
        {
            var msg = new MailMessage(_settings.EmailFrom, _settings.EmailTo)
            {
                // ReSharper disable PossibleMultipleEnumeration
                Subject = results.Any() ? string.Format(_settings.EmailSubjectResultsFormat, results.Sum(x => x.Items), results.Min(x => x.LastScan)) : _settings.EmailSubjectEmptyFormat,
                // ReSharper restore PossibleMultipleEnumeration
                SubjectEncoding = Encoding.UTF8,
                BodyEncoding = Encoding.UTF8,
                IsBodyHtml = true
            };

            var sb = new StringBuilder(@"<!DOCTYPE html>
<html lang='en'>
<head>
<meta charset='UTF-8'>
  <title>QTH search results</title>
  <style>
    * {box-sizing: border-box}
    html, body {margin:0; padding:0}

    .ext-link {text-align: right; width: 100%;}
    .ext-link a {color: #aaa;}
    .source {width: 100%; font-size: 2rem; font-weight: bold; text-align: center; color: cadetblue; }
    table {border: 1px solid #aaa; margin-bottom: 5px; width: 100%}
    tr, td {border: none; padding: 0; margin: 0}
    td.thumb {vertical-align: top; max-width: 300px}
    td.thumb img {width: 300px}
    td.thumb img.qth {max-width: 300px}
    td.thumb img.eham {max-width: 300px}
    td.title {height: 1.5rem; padding: 2px 5px; font: 1.2rem bold; font-family: helvetica; color: azure; background-color: cornflowerblue; width: 100%}
    td.title a.link {color: azure; text-decoration: none}
    td.title a.cat {float: right; font-size: 1rem; font-style: italic; color: oldlace}
    tr.content {height: 100%}
    tr.content td {padding: 10px 5px 0 5px; height: 100%; font-family: trebuchet ms; vertical-align: top}
    tr.content td .price {color: crimson}
    td.info {height: 1rem; padding: 10px 5px 0 5px; font-family: monospace; font-size: 0.8rem; vertical-align: bottom}
    td.info a.call {color: black;}
    td.info .modified {color: crimson}
</style>
</head>
<body>
");

            if (!string.IsNullOrEmpty(_settings.BodyFileName) && !string.IsNullOrEmpty(_settings.ResourceUrl))
                sb.AppendLine($"<div class='ext-link'><a href='{_settings.ResourceUrl}/{_settings.BodyFileName}' target='_blank'>View this email in a separate browser window</a></div>");

            // ReSharper disable once PossibleMultipleEnumeration
            foreach(var res in results)
            {
                sb.AppendLine($"<div class='source'>{res.Title}</div>\n<div>");
                sb.AppendLine(res.Html);
                sb.AppendLine("</div>");
            }

            sb.AppendLine("</body>\n</html>");

            msg.Body = sb.ToString();

            if (string.IsNullOrEmpty(_settings.BodyFileName)) return;

            _logger.LogDebug("Saving file");
            File.WriteAllText($"{_settings.ResourceFolder}/{_settings.BodyFileName}", msg.Body);

#if !DEBUG
            _logger.LogDebug($"Sending email to {_settings.EmailTo}");
            var client = new SmtpClient(_settings.SmtpServer);

            if (!string.IsNullOrWhiteSpace(_settings.User))
                client.Credentials = new NetworkCredential(_settings.User, _settings.Password);

            if (_settings.AttachFile && !string.IsNullOrEmpty(_settings.BodyFileName))
                msg.Attachments.Add(new Attachment(_settings.BodyFileName));

            client.Send(msg);
#endif
        }
    }
}
