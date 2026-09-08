using System.Threading;
using System.Threading.Tasks;

namespace Team11.AI
{
    /// Seam A. Sprint 1 implements CannedAnswerSource, Sprint 2 adds LiveAnswerSource
    /// and wraps both in FallbackAnswerSource. Nothing downstream of this changes.
    public interface IAnswerSource
    {
        Task<Answer> AskAsync(string question, CancellationToken ct);
    }
}
