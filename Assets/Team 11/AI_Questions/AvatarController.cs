using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Team11.AI
{
    /// The one place the pieces meet. It owns neither seam, so swapping either
    /// implementation leaves this file untouched.
    ///
    ///   question in  ->  answer out  ->  audio + mouth  ->  log  ->  next options
    public sealed class AvatarController : MonoBehaviour
    {
        public VisemePlayer       Player;
        public MenuQuestionSource Menu;

        [Tooltip("Optional. A short clip played the instant a question arrives, so the " +
                 "avatar acknowledges before the answer is ready. Buys back most of the " +
                 "Sprint 2 latency budget and costs one recording.")]
        public AudioClip Thinking;

        [Tooltip("Play Thinking only when the answer takes longer than this, in seconds. " +
                 "Sprint 1 answers come off disk and should never trip it.")]
        public float ThinkingAfterSeconds = 0.35f;

        /// Raised with the line being spoken. The UI captions it.
        public event Action<string> Spoke;

        private IAnswerSource        _answers;
        private CannedAnswerSource   _canned;
        private CancellationTokenSource _cts;
        private bool _busy;

        private void Start()
        {
            try
            {
                _canned  = new CannedAnswerSource();
                _answers = _canned;                  // Sprint 2 wraps this in FallbackAnswerSource
            }
            catch (Exception e)
            {
                Debug.LogError($"[AvatarController] {e.Message}");
                enabled = false;
                return;
            }

            if (Menu == null || Player == null)
            {
                Debug.LogError("[AvatarController] Menu and Player must be assigned.");
                enabled = false;
                return;
            }

            Menu.LabelFor = _canned.PromptFor;   // ids in the data, prompts on the buttons
            Menu.QuestionAsked += OnQuestion;
            Menu.Begin(_canned.RootOptions);
        }

        private void OnDestroy()
        {
            if (Menu != null) Menu.QuestionAsked -= OnQuestion;
            _cts?.Cancel();
            _cts?.Dispose();
        }

        private async void OnQuestion(string id)
        {
            if (_busy) return;
            _busy = true;

            _cts?.Cancel();
            _cts?.Dispose();
            _cts = new CancellationTokenSource();

            var clock = Stopwatch.StartNew();
            Answer answer = null;
            string outcome = "ok";

            try
            {
                var pending = _answers.AskAsync(id, _cts.Token);

                if (Thinking != null &&
                    await Task.WhenAny(pending, Task.Delay(
                        TimeSpan.FromSeconds(ThinkingAfterSeconds), _cts.Token)) != pending)
                {
                    Player.Play(Thinking, null);
                }

                answer = await pending;
            }
            catch (OperationCanceledException) { outcome = "cancelled"; }
            catch (Exception e)                { outcome = $"error: {e.Message}"; }

            if (answer?.Audio == null)
            {
                if (outcome == "ok") outcome = "no audio";
                TranscriptLog.Write("canned", id, clock.ElapsedMilliseconds, 0f, outcome);
                Debug.LogWarning($"[AvatarController] nothing to play for \"{id}\" ({outcome}).");
                Menu.Begin(_canned.RootOptions);
                _busy = false;
                return;
            }

            Player.Play(answer.Audio, answer.VisemeJson);
            Spoke?.Invoke(answer.Text);

            TranscriptLog.Write(
                answer.IsFallback ? "fallbk" : "canned",
                id, clock.ElapsedMilliseconds, answer.Audio.length,
                Player.HasTrack ? outcome : outcome + " (no visemes)");

            await Task.Delay(TimeSpan.FromSeconds(answer.Audio.length));

            // The second pass shows follow-ups, not the same four options.
            // That is what makes the conversation visible before any memory exists.
            var next = answer.FollowUps != null && answer.FollowUps.Length > 0
                     ? answer.FollowUps
                     : _canned.RootOptions;

            Menu.Begin(next);
            _busy = false;
        }
    }
}
