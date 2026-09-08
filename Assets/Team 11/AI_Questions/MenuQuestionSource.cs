using System;
using UnityEngine;

namespace Team11.AI
{
    /// Sprint 1's seam B. Deliberately dumb: it shows whatever options it is handed
    /// and knows nothing about conversation state. Sprint 3 replaces this whole class
    /// with a microphone and nothing on the answer side moves.
    ///
    /// Wire OnOptionChosen to the pinch/click handler on each option button.
    public sealed class MenuQuestionSource : MonoBehaviour, IQuestionSource
    {
        public event Action<string> QuestionAsked;

        [Tooltip("Raised with the ids to display. The UI subscribes and draws them.")]
        public event Action<string[]> OptionsChanged;

        [Tooltip("Set by AvatarController so the UI can show \"Who are you?\" " +
                 "instead of the id \"who\". Falls back to the id if unset.")]
        public Func<string, string> LabelFor;

        public string Label(string id) => LabelFor != null ? LabelFor(id) : id;

        private bool _live;

        public void Begin(string[] options)
        {
            _live = true;
            OptionsChanged?.Invoke(options ?? Array.Empty<string>());
        }

        public void End()
        {
            _live = false;
            OptionsChanged?.Invoke(Array.Empty<string>());
        }

        /// Call from the button for option `id`.
        public void OnOptionChosen(string id)
        {
            if (!_live) return;
            End();                       // stop double-taps landing twice
            QuestionAsked?.Invoke(id);
        }
    }
}
