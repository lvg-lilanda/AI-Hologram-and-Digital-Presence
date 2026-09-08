using System;
using UnityEngine;

namespace Team11.AI
{
    /// The normalised shape written by generate_clips.py. Values are flattened
    /// (frame * shapeCount + i) because Unity's JsonUtility cannot parse float[][].
    [Serializable]
    public class VisemeTrack
    {
        public string  id;
        public int     fps = 60;
        public int     shapeCount;
        public int     frameCount;
        public float[] values;
        public int[]   visemeIds;
        public float[] visemeOffsetsMs;

        public static VisemeTrack Parse(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            var t = JsonUtility.FromJson<VisemeTrack>(json);
            if (t == null || t.shapeCount <= 0 || t.values == null || t.values.Length == 0)
                return null;
            return t;
        }

        /// Weights for the frame covering this playback position, or null past the end.
        public bool TryGetFrame(float seconds, float[] into)
        {
            if (into == null || into.Length < shapeCount) return false;
            int f = Mathf.Clamp(Mathf.FloorToInt(seconds * fps), 0, frameCount - 1);
            int b = f * shapeCount;
            if (b < 0 || b + shapeCount > values.Length) return false;
            Array.Copy(values, b, into, 0, shapeCount);
            return true;
        }
    }
}
