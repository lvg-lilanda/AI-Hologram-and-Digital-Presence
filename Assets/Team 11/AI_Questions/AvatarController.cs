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

        [Header("Sprint 2 - live AI service")]
        [Tooltip("Off: the Sprint 1 recorded answers only. On: ask the local AI " +
                 "service, falling back to the recordings when it cannot answer.")]
        public bool UseLiveService = false;

        [Tooltip("Where the local service listens. It binds to localhost, so this is " +
                 "only ever 127.0.0.1 unless the service moves off the VX PC.")]
        public string ServiceUrl = "http://127.0.0.1:8765";

        [Tooltip("Give up on a single answer after this long and use a recording " +
                 "instead. Measured on an M3 Mac, a real answer takes 5-11 seconds.")]
        public int RequestTimeoutSeconds = 30;

        /// Raised with the line being spoken. The UI captions it.
        public event Action<string> Spoke;

        private IAnswerSource        _answers;
        private CannedAnswerSource   _canned;
        private string               _sourceTag = "canned";
        private CancellationTokenSource _cts;
        private LiveAnswerSource _live;
        private string _sessionId;
        private bool _busy;

        private void Start()
        {
            try
            {
                _canned  = new CannedAnswerSource();
                _answers = _canned;

                if (UseLiveService)
                {
                    // One session id per run of the scene, so the service treats this
                    // as one continuous conversation and its memory works. The operator
                    // gets a fresh visitor by restarting, or by calling ResetSession().
                    _sessionId = Guid.NewGuid().ToString("N").Substring(0, 12);
                    _live = new LiveAnswerSource(ServiceUrl, _sessionId, RequestTimeoutSeconds);

                    // The menu raises entry IDS; the service needs the human question.
                    // PromptFor is the translation, and it is the easiest thing in this
                    // wiring to forget - see FallbackAnswerSource.
                    _answers = new FallbackAnswerSource(_live, _canned, _canned.PromptFor);
                    _sourceTag = "live";
                    Debug.Log($"[AvatarController] live service at {ServiceUrl}, " +
                              $"session {_sessionId}");
                }
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

        /// Forget this visitor. Wire to the operator's between-visitors reset so the
        /// next person does not inherit the last one's conversation.
        public async void ResetSession()
        {
            if (!UseLiveService || string.IsNullOrEmpty(_sessionId)) return;
            using (var request = UnityEngine.Networking.UnityWebRequest.PostWwwForm(
                       $"{ServiceUrl.TrimEnd('/')}/session/{_sessionId}/reset", ""))
            {
                request.timeout = 10;
                var op = request.SendWebRequest();
                while (!op.isDone) await Task.Yield();
            }
            Debug.Log("[AvatarController] conversation reset");
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

                // An answer with no audio still has the words, so caption it rather
                // than dropping it. The Sprint 2 service returns a null audio URL
                // whenever speech is not configured - during bring-up, or if Azure
                // is down mid-demo - and an avatar that silently does nothing looks
                // broken, where one that shows the line it cannot speak looks like a
                // degraded mode. Follow-ups still advance, so the conversation is not
                // thrown back to the root options over a missing clip.
                if (!string.IsNullOrWhiteSpace(answer?.Text))
                {
                    Spoke?.Invoke(answer.Text);
                }

                TranscriptLog.Write(_sourceTag, id, clock.ElapsedMilliseconds, 0f, outcome);
                Debug.LogWarning($"[AvatarController] nothing to play for \"{id}\" ({outcome}).");

                var fallbackOptions = answer?.FollowUps != null && answer.FollowUps.Length > 0
                                    ? answer.FollowUps
                                    : _canned.RootOptions;
                Menu.Begin(fallbackOptions);
                _busy = false;
                return;
            }

            Player.Play(answer.Audio, answer.VisemeJson);
            Spoke?.Invoke(answer.Text);

            TranscriptLog.Write(
                answer.IsFallback ? "fallbk" : _sourceTag,
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
