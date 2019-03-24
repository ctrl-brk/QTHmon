using System.Threading;
using System.Threading.Tasks;

namespace QTHmon
{
    public interface IQthSwapHandler
    {
        Task<ScanResult> ProcessKeywordsAsync(CancellationToken token);
        Task<ScanResult> ProcessCategoriesAsync(CancellationToken token);
    }
}
