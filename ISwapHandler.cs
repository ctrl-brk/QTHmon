// ReSharper disable once CheckNamespace
namespace QTHmon;

public interface ISwapHandler
{
    Task<ScanResult> ProcessKeywordsAsync(HttpClient client, CookieContainer cookies, CancellationToken token);
    Task<IEnumerable<ScanResult>> ProcessCategoriesAsync(HttpClient client, CookieContainer cookies, CancellationToken token);
}
