using Leap;
using Leap.Unity;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GestureShoot : MonoBehaviour
{
    public LeapProvider leapProvider;
    public GunShoot gunShoot;

    [Header("Gesture Settings")]
    public bool useRightHandToShoot = true;
    public float pinchThreshold = 0.8f;
    public float shootCooldown = 0.5f;

    private float lastShootTime;
    private bool wasPinching = false;

    private void Update()
    {
        // If editor objects not set properly return null
        if (leapProvider == null || gunShoot == null)
        {
            return;
        }

        Frame frame = leapProvider.CurrentFrame;

        // Set the correct hand to detect the gesture
        foreach (Hand hand in frame.Hands)
        {
            bool correctHand = useRightHandToShoot ? hand.IsRight : hand.IsLeft;

            if (!correctHand)
            {
                continue;
            }

            // Check if target hand is pinching
            bool isPinching = hand.PinchStrength >= pinchThreshold;

            // If player pinches, shoot a bullet and start a cooldown timer
            if (isPinching && !wasPinching && Time.time >= lastShootTime + shootCooldown)
            {
                gunShoot.Shoot();
                lastShootTime = Time.time;
                Debug.Log("Gesture shoot triggered");
            }

            wasPinching = isPinching;
        }
    }
}
