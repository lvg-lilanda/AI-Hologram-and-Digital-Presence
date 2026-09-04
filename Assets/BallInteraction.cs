using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using HapE.Unity;
//using Leap;
//using Leap.Unity;

public class BallInteraction : MonoBehaviour
{
    public HapEDeviceManager hapticsDevice = null;
    //public LeapProvider leapProvider;
    private bool isHapticPlaying = false;
    private string hapticsPath = "C:/Dev/Startup Pack/UnityBaseScene/HoloTV_BaseScene/Assets/StreamingAssets/haptics/00.Presence.json";

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
    
    }

    void OnCollisionEnter(Collision collision)
    {
        //using String 'Contact' to check if hand.
        
        if (collision.collider.name.Contains("Contact"))
        {            
            hapticsDevice.PlayHapEJSON(hapticsPath);
        }
    }
}
