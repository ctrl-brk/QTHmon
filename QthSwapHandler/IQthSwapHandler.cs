using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

// ReSharper disable once CheckNamespace
namespace QTHmon
{
    public interface IQthSwapHandler
    {
        Task<ScanResult> ProcessKeywordsAsync(CancellationToken token);
        Task<IEnumerable<ScanResult>> ProcessCategoriesAsync(CancellationToken token);
    }
}
