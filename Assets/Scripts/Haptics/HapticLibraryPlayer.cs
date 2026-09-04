using System;
using UnityEngine;

/// <summary>
/// Script to playback Sensations found in the HapESensationLibrary.
/// </summary>
namespace HapE.Unity
{
    public class HapticLibraryPlayer : MonoBehaviour
    {
        // Start is called before the first frame update
        [Tooltip("The object to manage loading of HapE JSON objects from StreamingAssets")]
        public HapESensationLibrary library;

        [Tooltip("Manages the HapE Devices and Playback")]
        public HapEDeviceManager hapeDeviceManager;

        [Tooltip("The sensation currently playing back.")]
        public HapESensation currentSensation;

        /// <summary>
        /// Plays haptic at a specific TrackingFixation
        /// </summary>
        public void PlayHapticWithNameAtFixation(string hapticName, TrackingFixation.Fixation fixation, Vector3? fixationOffset = null, bool forceLoadData = false)
        {
            Debug.Log("Play Sensation : " + hapticName + " at fixation:" + fixation);
            hapeDeviceManager.trackTransformObject.fixation = fixation;
            if (fixationOffset != null)
            {
                hapeDeviceManager.trackTransformObject.fixedOffsetMeters = (Vector3)fixationOffset;
            }
            PlaySensationWithName(hapticName, forceLoadData: forceLoadData, ignoreDefaultFixation: true);
        }

        public bool isPlaying()
        {
            return hapeDeviceManager.hapticDevice.HapE.IsEmitting;
        }


        /// <summary>
        /// Method to allow calling from Unity Events with default args.
        /// </summary>
        /// <param name="hapticName"></param>
        public void PlaySensationWithName(string hapticName)
        {
            PlaySensationWithName(hapticName, transformOverride: null, forceLoadData: false, ignoreDefaultFixation: false);
        }

        /// <summary>
        /// Plays haptic at the Fixation specified by the HapESensation
        /// </summary>
        /// <param name="hapticName"></param>
        public void PlaySensationWithName(string hapticName, Transform transformOverride = null, bool forceLoadData = false, bool ignoreDefaultFixation = false)
        {
            HapESensation sensation = library.GetSensationByHapticName(hapticName);
            if (sensation == null)
            {
                Debug.LogWarning("Tried to play sensation named: " + hapticName + ", but could not find it in the Library! Check StreamingAssets haptics dirs...");
                return;
            }

            // For non-looping sensations, we ensure that the Haptic Playback stops before...
            if (!sensation.IsLoopingSensation)
            {
                hapeDeviceManager.StopHaptics();
            }

            HapEData data = sensation.hapeData;
            if (data.hapticName == null || data.hapticName == "")
            {
                Debug.LogWarning("Could not play haptic with name: " + hapticName);
                return;
            }

            if (transformOverride == null)
            {
                if (!ignoreDefaultFixation)
                {
                    hapeDeviceManager.trackTransformObject.fixation = sensation.Fixation;
                    hapeDeviceManager.trackTransformObject.fixedOffsetMeters = sensation.offsetMeters;
                }
            }
            else
            {
                hapeDeviceManager.trackTransformObject.fixation = TrackingFixation.Fixation.Fixed;
                hapeDeviceManager.trackTransformObject.fixedOffsetMeters = transformOverride.position;
                hapeDeviceManager.trackTransformObject.fixedOffsetRotationEulerAngles = transformOverride.eulerAngles;
            }

            if (forceLoadData || ((currentSensation != null) && (currentSensation.HapticName != hapticName)))
            {
                hapeDeviceManager.PlayHapEData(data, skipReset: true);
            }
            else if (!forceLoadData && (currentSensation == null) || (currentSensation.HapticName != hapticName))
            {
                hapeDeviceManager.PlayHapEData(data, skipReset: true);
            }
            else
            {
                hapeDeviceManager.PlayCurrentDeviceHapEData();
            }
            currentSensation = sensation;
        }

        public void ClearCurrentSensation()
        {
            currentSensation = null;
            // The Device Count has changed, we should stop output, before trying to reconnect to something...
            try
            {
                StopHaptics();
            }
            catch (Exception e)
            {
                Debug.LogWarning("ClearCurrentSensation, but unable to stop haptics due to error: " + e);
            }
        }

        /// <summary>
        /// Plays Sensation at a forced Fixation
        /// </summary>
        /// <param name="hapticName"></param>
        public void PlaySensationWithNameWithFixation(string hapticName, TrackingFixation.Fixation fixation, Transform transformOverride = null, bool forceLoadData = true)
        {
            Debug.Log("PlaySensationWithNameWithFixation : " + hapticName + " at Fixation:" + fixation);

            HapESensation sensation = library.GetSensationByHapticName(hapticName);

            // For non-looping sensations, we ensure that the Haptic Playback stops before...
            if (!sensation.IsLoopingSensation)
            {
                hapeDeviceManager.StopHaptics();
            }
            if (sensation == null)
            {
                Debug.LogWarning("Tried to play sensation named: " + hapticName + ", but could not find it in the Library! Check StreamingAssets haptics dirs...");
                return;
            }
            HapEData data = sensation.hapeData;
            if (data.hapticName == null || data.hapticName == "")
            {
                Debug.LogWarning("Could not play haptic with name: " + hapticName);
                return;
            }

            hapeDeviceManager.trackTransformObject.fixation = fixation;

            if (transformOverride != null)
            {
                hapeDeviceManager.trackTransformObject.fixedOffsetMeters = transformOverride.position;
                hapeDeviceManager.trackTransformObject.fixedOffsetRotationEulerAngles = transformOverride.eulerAngles;
            }

            if (forceLoadData || ((currentSensation != null) && (currentSensation.HapticName != hapticName)))
            {
                hapeDeviceManager.PlayHapEData(data, skipReset: true);
            }
            else
            {
                Debug.Log("hapeDeviceManager.PlayCurrentDeviceHapEData();");
                hapeDeviceManager.PlayCurrentDeviceHapEData();
            }
            currentSensation = sensation;
        }

        public void StopHaptics()
        {
            Debug.Log("Stop Haptics");
            hapeDeviceManager.StopHaptics();
        }

        public void PlaySensationWithNameAtFixedLocation(string hapticName, Transform transform)
        {
            Debug.Log("Play Haptic : " + hapticName + " at position:" + transform.position);
            PlaySensationWithName(hapticName, transformOverride: transform, forceLoadData: false);
        }
    }
}
