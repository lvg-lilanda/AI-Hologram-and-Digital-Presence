using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Leap;
using Leap.Unity;

public class followHand : MonoBehaviour
{
    private bool followLeft = false;
    [SerializeField] private LeapServiceProvider Provider;

    void Update()
    {
        // If Ultraleap is not active, return null
        if (Provider == null) return;
        Frame frame = Provider.CurrentFrame;
        if (frame == null) return;

        // Set the target hand to be followed depending on the bool set in the editor
        Hand targetHand = null;
        foreach (Hand hand in frame.Hands)
        {
            if ((followLeft && hand.IsLeft) || (!followLeft && hand.IsRight))
            {
                targetHand = hand;
                break;
            }
        }
        // Get the palm position of the target hand and translates objects position to it
        if (targetHand != null)
        {
            Quaternion handRotation = targetHand.Rotation;
            Vector3 palmPos = targetHand.PalmPosition;
            transform.position = palmPos;
            transform.rotation = handRotation;
            transform.Rotate(90, -90, 0);
        }
    }
}
