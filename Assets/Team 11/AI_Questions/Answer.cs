using System;
using UnityEngine;

namespace Team11.AI
{
    /// What the avatar needs in order to speak one reply.
    /// Identical whether it came off disk (Sprint 1) or off the wire (Sprint 2).
    public sealed class Answer
    {
        public string    Id;          // entry id, for the log
        public string    Text;        // captions and the transcript log
        public AudioClip Audio;       // what actually plays
        public string    VisemeJson;  // Azure blend shapes, null when unavailable
        public string[]  FollowUps;   // what the option list shows next
        public bool      IsFallback;  // true when the live path failed and a clip answered
    }

    [Serializable]
    public class AnswerEntry
    {
        public string   id;
        public string   prompt;
        public string   text;
        public string[] followUps;
    }

    [Serializable]
    public class AnswerBook
    {
        public int           version;
        public string        voice;
        public string        locale;
        public string[]      root;
        public AnswerEntry[] entries;
    }
}
