using UnityEngine;

namespace HapE.Unity
{
    public class HapESensation : ScriptableObject
    {
        public HapEData hapeData;
        [SerializeField]
        private string _hapticName;
        public string HapticName
        {
            set { _hapticName = value; hapeData.hapticName = _hapticName; }
            get { return hapeData.hapticName; }
        }

        private TrackingFixation.Fixation _fixation = TrackingFixation.Fixation.TrackPalm;
        public TrackingFixation.Fixation Fixation
        {
            set { _fixation = value; hapeData.SetFixation(_fixation); }
            get { return hapeData.GetFixation(); }
        }

        private Vector3 _offsetMeters;
        public Vector3 offsetMeters
        {
            set { _offsetMeters = value; hapeData.fixation_offset = _offsetMeters; }
            get { return hapeData.fixation_offset; }
        }
        public bool IsLoopingSensation
        {
            get { return (hapeData.envelope.repeat_count == 0); }
        }
    }
}
