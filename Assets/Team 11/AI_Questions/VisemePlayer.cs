using UnityEngine;

namespace Team11.AI
{
    /// Drives mouth blend shapes from the track Azure generated alongside the audio,
    /// sampled against the AudioSource's own playback position so picture and sound
    /// cannot drift. Falls back to doing nothing when a clip has no track, which is
    /// when uLipSync should take over.
    [RequireComponent(typeof(AudioSource))]
    public sealed class VisemePlayer : MonoBehaviour
    {
        [Tooltip("The face mesh carrying the blend shapes.")]
        public SkinnedMeshRenderer Face;

        [Tooltip("Blend shape index on Face for each of Azure's 55 positions, in order. " +
                 "Leave an entry at -1 to ignore that position. Fill this from the mapping " +
                 "task once the avatar rig exists.")]
        public int[] ShapeMap;

        [Range(0f, 1f)] public float Smoothing = 0.35f;
        public bool HasTrack => _track != null;

        private AudioSource _audio;
        private VisemeTrack _track;
        private float[] _frame, _current;

        private void Awake() => _audio = GetComponent<AudioSource>();

        public void Play(AudioClip clip, string visemeJson)
        {
            _track = VisemeTrack.Parse(visemeJson);
            if (_track != null)
            {
                _frame   = new float[_track.shapeCount];
                _current = new float[_track.shapeCount];
                if (ShapeMap == null || ShapeMap.Length < _track.shapeCount)
                    Debug.LogWarning($"[VisemePlayer] ShapeMap has " +
                        $"{(ShapeMap == null ? 0 : ShapeMap.Length)} entries, track needs " +
                        $"{_track.shapeCount}. Unmapped positions are ignored.");
            }
            else
            {
                Debug.Log("[VisemePlayer] no viseme track for this clip, mouth not driven.");
            }

            _audio.clip = clip;
            _audio.Play();
        }

        private void LateUpdate()
        {
            if (_track == null || Face == null || !_audio.isPlaying) return;
            if (!_track.TryGetFrame(_audio.time, _frame)) return;

            float k = 1f - Mathf.Pow(Smoothing, Time.deltaTime * 60f);
            int n = ShapeMap == null ? 0 : Mathf.Min(ShapeMap.Length, _track.shapeCount);

            for (int i = 0; i < n; i++)
            {
                int target = ShapeMap[i];
                if (target < 0) continue;
                _current[i] = Mathf.Lerp(_current[i], _frame[i], k);
                Face.SetBlendShapeWeight(target, _current[i] * 100f);
            }
        }
    }
}
