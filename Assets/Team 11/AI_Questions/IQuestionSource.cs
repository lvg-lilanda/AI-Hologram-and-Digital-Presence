using System;

namespace Team11.AI
{
    /// Seam B. Sprint 1 is a pinch menu, Sprint 3 is a microphone.
    /// The source is deliberately dumb: it raises a question and knows nothing
    /// about conversation state, so swapping it can never break the answer side.
    public interface IQuestionSource
    {
        event Action<string> QuestionAsked;

        /// Show the given options and start listening. Called after every answer,
        /// with that answer's FollowUps, which is what makes memory visible.
        void Begin(string[] options);

        void End();
    }
}
