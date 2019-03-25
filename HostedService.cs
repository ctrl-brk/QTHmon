using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
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
        private readonly IApplicationLifetime _appLifeTime;
        private readonly AppSettings _settings;
        private readonly IQthSwapHandler _qthSwapHandler;

        private Task _task;
        private CancellationTokenSource _cts;

        public HostedService(ILogger<HostedService> logger, IApplicationLifetime appLifeTime, IOptions<AppSettings> settings, IQthSwapHandler qthSwapHandler)
        {
            _logger = logger;
            _appLifeTime = appLifeTime;
            _settings = settings.Value;
            _qthSwapHandler = qthSwapHandler;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting");

            // Create a linked token so we can trigger cancellation outside of this token's cancellation
            _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            _task = MonitorAsync(_cts.Token);

            // If the task is completed then return it, otherwise it's running
            return _task.IsCompleted ? _task : Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Stopping");
            return Task.CompletedTask;
        }

        private async Task MonitorAsync(CancellationToken token)
        {
            var results = new List<ScanResult>();

            var keyRes = await _qthSwapHandler.ProcessKeywordsAsync(token);
            if (keyRes != null) results.Add(keyRes);

            results.AddRange((await _qthSwapHandler.ProcessCategoriesAsync(token)).Where(catRes => catRes != null));

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

    .source {width: 100%; font-size: 2rem; font-weight: bold; text-align: center; color: cadetblue; }
    table {border: 1px solid #aaa; margin-bottom: 5px; width: 100%}
    tr, td {border: none; padding: 0; margin: 0}
    td.thumb {vertical-align: top; max-width: 300px}
    td.thumb img {width: 300px}
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

            // ReSharper disable once PossibleMultipleEnumeration
            foreach(var res in results)
            {
                sb.AppendLine($"<div class='source'>{res.Title}</div>\n<div>");
                sb.AppendLine(res.Html);
                sb.AppendLine("</div>");
            }

            sb.AppendLine("</body>\n</html>");

            msg.Body = sb.ToString();

            //File.WriteAllText("msg.html", msg.Body);
            //return;

            var client = new SmtpClient(_settings.SmtpServer);

            if (!string.IsNullOrWhiteSpace(_settings.User))
                client.Credentials = new NetworkCredential(_settings.User, _settings.Password);

            _logger.LogDebug("Sending email");
            client.Send(msg);
        }
    }
}
