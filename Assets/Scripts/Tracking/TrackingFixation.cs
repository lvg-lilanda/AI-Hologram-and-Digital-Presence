using UnityEngine;
using Leap.Unity;
using Leap.Unity.Interaction;

/// <summary>
/// The Main Class for handling where the Haptic Fixation is.
/// Includes various modes/hand positions for positioning haptics.
/// </summary>
/// 

namespace HapE.Unity
{

    public class TrackingFixation : MonoBehaviour
    {
        public LeapProvider handManager;
        public Transform fixationTransform;
        public Transform defaultTransform;

        [Tooltip("An optional HapE Device, used in Multi-array scenarios, allows Tracking offset to take Track-Array offset into account")]
        public HapEDevice activeHapEDevice;

        /// <summary>
        /// Fixation - 
        /// 0-'TrackPalm' follows hand plane, position and rotation,  
        /// 1-'Fixed' is a static, x-y-z pos. 
        /// 2-'TrackHeight' tracks hand height only.
        /// 3-'Forcefield' tracks hand-side+ height, but keeps depth fixed (a la Forcefield)
        /// 4-'TrackIndexFinger' tracks the index intermediate bone joint.
        /// 5-'TrackMiddleFinger' tracks the middle intermediate bone joint.
        /// 6-'PinchCentroid' tracks an approximation of the ring centroid, formed by a pinch.
        /// 7-'PinchPosition' tracks the position of the pinch (index/middle+thumb)
        /// 8-'Wrist' tracks the position of the wrist
        /// 9-'ScreenIndexFinger', like Forcefield, but tracks the index finger, not the palm.
        /// 10-'Knuckles' - the middle knuckle location
        /// </summary>
        public enum Fixation
        {
            TrackPalm,
            Fixed,
            TrackHeight,
            Forcefield,
            TrackIndexFinger,
            TrackMiddleFinger,
            PinchCentroid,
            PinchPosition,
            WristPosition,
            ScreenIndexFinger,
            Knuckle
        }

        private void Awake()
        {
            if (handManager == null)
            {
                //handManager = FindObjectOfType<HandInteractionManager>();
                //if (handManager == null)
                //{
                    Debug.LogWarning("Unable to find a HandInteractionManager. Please check InteractionManager exists and is in the scene.");
                //}
            }
        }

        // A full X-Z-Y offset, designed for non-handtrack
        public Vector3 fixedOffsetMeters;
        public Vector3 fixedOffsetRotationEulerAngles;
        public Fixation fixation;

        public void SetDefaultFixedOrigin()
        {
            if (activeHapEDevice != null)
            {
                fixationTransform.transform.SetPositionAndRotation(defaultTransform.position + activeHapEDevice.deviceTransform.localPosition, Quaternion.identity);
            }
            else
            {
                fixationTransform.transform.SetPositionAndRotation(defaultTransform.position, Quaternion.identity);
            }
        }

        /// <summary>
        /// Sets the fixation by enum index. Useful for dropdown menus.
        /// </summary>
        /// <param name="fixation"></param>
        public void SetFixation(int fixationInt)
        {
            fixation = (Fixation)fixationInt;
        }

        /// <summary>
        /// Returns an approximate location for the pinch centroid, 
        /// between knuckle, and intermediate bones of index+thumb
        /// </summary>
        /// <returns></returns>
        public Vector3 GetPinchCentroid(bool useIndex = true)
        {
            Vector3 centroid = Vector3.zero;

            if (handManager.CurrentFrame.Hands.Count > 0)
            {
                Leap.Finger pinchFinger = handManager.CurrentFrame.Hands[0].GetIndex();
                if (!useIndex)
                {
                    pinchFinger = handManager.CurrentFrame.Hands[0].GetMiddle();
                }
                Leap.Finger thumb = handManager.CurrentFrame.Hands[0].GetThumb();
                Vector3 a = pinchFinger.Bone(Leap.Bone.BoneType.TYPE_METACARPAL).Center;
                Vector3 b = pinchFinger.Bone(Leap.Bone.BoneType.TYPE_INTERMEDIATE).Center;
                Vector3 c = thumb.Bone(Leap.Bone.BoneType.TYPE_INTERMEDIATE).Center;
                centroid = (a + b + c) / 3f;
            }
            return centroid;
        }

        /// <summary>
        /// Returns location for the pinch point, 
        /// between tips of index/middle + thumb
        /// </summary>
        /// <returns></returns>
        public Vector3 GetPinchPosition(bool useIndex = true)
        {
            Vector3 centroid = Vector3.zero;
            if (handManager.CurrentFrame.Hands.Count > 0)
            {
                Leap.Finger pinchFinger = handManager.CurrentFrame.Hands[0].GetIndex();
                if (!useIndex)
                {
                    pinchFinger = handManager.CurrentFrame.Hands[0].GetMiddle();
                }

                Leap.Finger thumb = handManager.CurrentFrame.Hands[0].GetThumb();
                Vector3 a = pinchFinger.TipPosition;
                Vector3 b = thumb.TipPosition;
                centroid = (a + b) / 2f;
            }
            return centroid;
        }


        // Update is called once per frame
        void Update()
        {
            float zArrayOffset = 0f;

            // If we have a multi-device scenario, and using one Leap camera to serve multiple arrays,
            // we need to adjust the fixed offset, based on the Array's local position
            // (we assume the Tracker/HandTracking Origin is a child of the activeHapEDevice.
            if (activeHapEDevice != null)
            {
                zArrayOffset = activeHapEDevice.deviceTransform.localPosition.z;
            }

            // Case where we have a hand tracked haptic, but there's no hand - default to 20cm above array.
            if ((fixation != Fixation.Fixed) && handManager.CurrentFrame.Hands.Count == 0)
            {
                SetDefaultFixedOrigin();
            }
            else if ((fixation == Fixation.Fixed))
            {
                fixationTransform.transform.position = fixedOffsetMeters;
                fixationTransform.transform.rotation = Quaternion.identity;
            }

            else if ((fixation == Fixation.TrackPalm))
            {
                if (handManager.CurrentFrame.Hands.Count > 0)
                {
                    Vector3 newPos = handManager.CurrentFrame.Hands[0].PalmPosition + fixedOffsetMeters;
                    fixationTransform.SetPositionAndRotation(newPos, handManager.CurrentFrame.Hands[0].Rotation);
                }
            }

            else if ((fixation == Fixation.TrackHeight) && handManager.CurrentFrame.Hands.Count > 0)
            {
                Vector3 handPos = handManager.CurrentFrame.Hands[0].PalmPosition;
                fixationTransform.SetPositionAndRotation(new Vector3(fixedOffsetMeters.x, handPos.y, fixedOffsetMeters.z + zArrayOffset), Quaternion.identity);
            }
            else if ((fixation == Fixation.Forcefield) && handManager.CurrentFrame.Hands.Count > 0)
            {
                Vector3 handPos = handManager.CurrentFrame.Hands[0].PalmPosition;
                fixationTransform.SetPositionAndRotation(new Vector3(handPos.x, handPos.y, fixedOffsetMeters.z + zArrayOffset), Quaternion.identity);
            }
            else if ((fixation == Fixation.ScreenIndexFinger) && handManager.CurrentFrame.Hands.Count > 0)
            {
                Vector3 fingerPos = handManager.CurrentFrame.Hands[0].GetIndex().TipPosition;
                fixationTransform.SetPositionAndRotation(new Vector3(fingerPos.x, fingerPos.y, fixedOffsetMeters.z + zArrayOffset), Quaternion.identity);
            }

            else if ((fixation == Fixation.TrackIndexFinger) && handManager.CurrentFrame.Hands.Count > 0)
            {
                Leap.Finger indexFinger = handManager.CurrentFrame.Hands[0].GetIndex();
                Vector3 boneDirection = indexFinger.Bone(Leap.Bone.BoneType.TYPE_INTERMEDIATE).Direction;
                if (boneDirection == Vector3.zero)
                {
                    boneDirection = Vector3.one;
                }
                Vector3 distalBonePos = indexFinger.Bone(Leap.Bone.BoneType.TYPE_INTERMEDIATE).Center;
                Quaternion boneRotation = Quaternion.LookRotation(boneDirection);
                Vector3 fingerPos = distalBonePos + fixedOffsetMeters;
                fixationTransform.SetPositionAndRotation(fingerPos, boneRotation);
            }
            else if ((fixation == Fixation.TrackMiddleFinger) && handManager.CurrentFrame.Hands.Count > 0)
            {
                Leap.Finger middleFinger = handManager.CurrentFrame.Hands[0].GetMiddle();
                Vector3 boneDirection = middleFinger.Bone(Leap.Bone.BoneType.TYPE_INTERMEDIATE).Direction;
                if (boneDirection == Vector3.zero)
                {
                    boneDirection = Vector3.one;
                }
                Vector3 distalBonePos = middleFinger.Bone(Leap.Bone.BoneType.TYPE_INTERMEDIATE).Center;
                Quaternion boneRotation = Quaternion.LookRotation(boneDirection);
                Vector3 fingerPos = distalBonePos + fixedOffsetMeters;
                fixationTransform.SetPositionAndRotation(fingerPos, boneRotation);
            }
            else if ((fixation == Fixation.PinchCentroid) && handManager.CurrentFrame.Hands.Count > 0)
            {
                Vector3 pinchCentroid = GetPinchCentroid();
                Quaternion palmRotation = handManager.CurrentFrame.Hands[0].Rotation;
                // Adds a 90 degrees Z rotation to the Palm rotation - this oddly gives a stable approximation of the pinch plane!
                palmRotation *= Quaternion.Euler(0, 0, 90);
                Vector3 trackingPos = pinchCentroid + fixedOffsetMeters;
                fixationTransform.transform.SetPositionAndRotation(trackingPos, palmRotation);
            }
            else if ((fixation == Fixation.PinchPosition) && handManager.CurrentFrame.Hands.Count > 0)
            {
                Vector3 pinchCentroid = GetPinchPosition();
                Quaternion palmRotation = handManager.CurrentFrame.Hands[0].Rotation;
                // Adds a 90 degrees Z rotation to the Palm rotation - this oddly gives a stable approximation of the pinch plane!
                //palmRotation *= Quaternion.Euler(0, 0, 90);
                Vector3 trackingPos = pinchCentroid + fixedOffsetMeters;
                fixationTransform.transform.SetPositionAndRotation(trackingPos, palmRotation);
            }
            else if ((fixation == Fixation.WristPosition) && handManager.CurrentFrame.Hands.Count > 0)
            {
                Vector3 pinchCentroid = handManager.CurrentFrame.Hands[0].WristPosition;
                Quaternion palmRotation = handManager.CurrentFrame.Hands[0].Rotation;
                Vector3 trackingPos = pinchCentroid + fixedOffsetMeters;
                fixationTransform.transform.SetPositionAndRotation(trackingPos, palmRotation);
            }
            else if ((fixation == Fixation.Knuckle) && handManager.CurrentFrame.Hands.Count > 0)
            {
                Leap.Finger middleFinger = handManager.CurrentFrame.Hands[0].GetMiddle();
                Vector3 boneDirection = middleFinger.Bone(Leap.Bone.BoneType.TYPE_PROXIMAL).Direction;
                if (boneDirection == Vector3.zero)
                {
                    boneDirection = Vector3.one;
                }
                Vector3 distalBonePos = middleFinger.Bone(Leap.Bone.BoneType.TYPE_PROXIMAL).Center;
                Quaternion boneRotation = Quaternion.LookRotation(boneDirection);
                Vector3 fingerPos = distalBonePos + fixedOffsetMeters;
                fixationTransform.SetPositionAndRotation(fingerPos, boneRotation);
            }
        }
    }
}