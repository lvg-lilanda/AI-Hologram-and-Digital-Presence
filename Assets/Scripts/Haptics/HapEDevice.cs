using UnityEngine;
using Ultraleap.Haptics;
/// <summary>
/// Object Representing the Hap-e Haptics Device.
/// Used mainly in Multi-Device scenarios, to determine where haptics should positioned
/// and which array should fire.
/// </summary>
/// 
namespace HapE.Unity
{
    public class HapEDevice : MonoBehaviour
    {
        public Device device;
        public Transform deviceTransform;
        public string nickname = "MainArray";

        // The Object representing the Leap Motion Camera for this device;
        public Transform trackingOrigin;
        public string deviceId = "DEVICE_ID";

        // The Object representing the Leap Motion Camera for this device;
        public Transform particleRendererTransform;

        // Start is called before the first frame update
        void Start()
        {
            if (deviceTransform == null)
            {
                deviceTransform = gameObject.transform;
            }
        }

        public Vector3 GetTrackerOffsetTranslation()
        {
            Vector3 offset = trackingOrigin.position - deviceTransform.position;
            return offset;
        }

        public void SetParticleRendererTransform()
        {
            //Debug.Log("SetParticleRendererTransform");
            Vector3 offsetPosition = deviceTransform.localPosition;// - trackingOrigin.localPosition;
            Quaternion offsetAngle = Quaternion.Euler(deviceTransform.localEulerAngles - trackingOrigin.localEulerAngles);
            particleRendererTransform.transform.SetLocalPositionAndRotation(offsetPosition, offsetAngle);
        }
    }
}
