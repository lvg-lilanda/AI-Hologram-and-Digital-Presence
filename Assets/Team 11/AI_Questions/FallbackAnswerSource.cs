using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Team11.AI
{
    /// Tries the live service, drops to the recorded clips when it cannot answer.
    /// Answer.IsFallback already existed in the Sprint 1 data contract for exactly
    /// this, so nothing downstream needs to change.
    ///
    /// The id/question translation lives here and is easy to miss: MenuQuestionSource
    /// raises ENTRY IDS ("murud"), because that is what CannedAnswerSource looks up.
    /// The live service does retrieval over natural language, and "murud" retrieves
    /// almost nothing. So the id goes to the canned source and the human-readable
    /// prompt goes to the service. Send the id to the service and it will quietly
    /// answer worse, with no error anywhere.
    public sealed class FallbackAnswerSource : IAnswerSource
    {
        private readonly LiveAnswerSource _live;
        private readonly CannedAnswerSource _canned;
        private readonly Func<string, string> _toQuestionText;

        public FallbackAnswerSource(LiveAnswerSource live, CannedAnswerSource canned,
                                    Func<string, string> toQuestionText)
        {
            _live = live;
            _canned = canned;
            _toQuestionText = toQuestionText ?? (id => id);
        }

        public async Task<Answer> AskAsync(string question, CancellationToken ct)
        {
            try
            {
                var answer = await _live.AskAsync(_toQuestionText(question), ct);

                // A live answer counts if it has words. Audio may legitimately be
                // absent while speech is unconfigured, and falling back to a canned
                // clip in that case would replace a real answer with a scripted one -
                // worse, not better.
                if (!string.IsNullOrWhiteSpace(answer?.Text))
                {
                    return answer;
                }

                Debug.LogWarning("[FallbackAnswerSource] live source gave nothing; " +
                                 $"using the recorded answer. {_live.LastError}");
            }
            catch (OperationCanceledException)
            {
                throw;   // the person moved on - not a failure to paper over
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[FallbackAnswerSource] live source threw: {e.Message}");
            }

            var canned = await _canned.AskAsync(question, ct);
            if (canned != null) canned.IsFallback = true;
            return canned;
        }
    }
}
