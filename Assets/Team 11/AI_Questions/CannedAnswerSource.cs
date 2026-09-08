using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Team11.AI
{
    /// Sprint 1's answer source, and the permanent degraded mode afterwards.
    /// Expects, under any Resources folder:
    ///     answers.json          the book
    ///     clips/<id>.wav        the audio
    ///     visemes/<id>.json     the blend shapes
    public sealed class CannedAnswerSource : IAnswerSource
    {
        private readonly AnswerBook _book;

        public CannedAnswerSource(string resourceName = "answers")
        {
            var json = Resources.Load<TextAsset>(resourceName);
            if (json == null)
                throw new InvalidOperationException(
                    $"Resources/{resourceName}.json not found. It must sit under a folder " +
                    "literally named Resources for Unity to see it.");

            _book = JsonUtility.FromJson<AnswerBook>(json.text);
            if (_book?.entries == null || _book.entries.Length == 0)
                throw new InvalidOperationException($"{resourceName}.json parsed but held no entries.");
        }

        public string[] RootOptions => _book.root;

        public string PromptFor(string id) =>
            _book.entries.FirstOrDefault(e => e.id == id)?.prompt ?? id;

        public Task<Answer> AskAsync(string question, CancellationToken ct)
        {
            var entry = _book.entries.FirstOrDefault(e =>
                            string.Equals(e.id, question, StringComparison.OrdinalIgnoreCase))
                     ?? _book.entries.FirstOrDefault(e =>
                            string.Equals(e.prompt, question, StringComparison.OrdinalIgnoreCase));

            if (entry == null)
            {
                Debug.LogWarning($"[CannedAnswerSource] no entry for \"{question}\".");
                return Task.FromResult<Answer>(null);
            }

            var clip = Resources.Load<AudioClip>($"clips/{entry.id}");
            if (clip == null)
                Debug.LogWarning($"[CannedAnswerSource] Resources/clips/{entry.id} missing. " +
                                 "Run generate_clips.py and copy the output in.");

            var visemes = Resources.Load<TextAsset>($"visemes/{entry.id}");

            return Task.FromResult(new Answer
            {
                Id         = entry.id,
                Text       = entry.text,
                Audio      = clip,
                VisemeJson = visemes != null ? visemes.text : null,
                FollowUps  = entry.followUps,
                IsFallback = false,
            });
        }
    }
}
