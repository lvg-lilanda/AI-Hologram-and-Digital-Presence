using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NewBehaviourScript : MonoBehaviour
{
    Vector3 startpos;
    Vector3 currpos;
    //Quaternion startorient;
    Rigidbody body;
    float maxvel;

    public enum CenterLocation {initLocation, trueCenter};
    public CenterLocation center = CenterLocation.initLocation;

    public float angularSlowCoefficient = 0.1f;
    public float slowingCoefficient = 0.95f;
    public float centerSpeedLimit = 10.0f;
    //public Quaternion currorient;
    public float velMultiplier = 0.1f;
    public enum fmode {vel, accel, impulse }
    public fmode mode = fmode.vel;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if (center == CenterLocation.initLocation) { startpos = transform.position; }
        else { startpos = new Vector3(0f, 5f, 0f); }
            //startorient = transform.rotation;
        body = GetComponent<Rigidbody>();
        body.AddTorque(new Vector3(0.01f, 0.01f, 0.01f));
    }

    // Update is called once per frame
    void Update()
    {
        //take curr pos and orientation, define current max vel
        currpos = transform.position;
        maxvel = centerSpeedLimit * currpos.magnitude;
        //currorient = transform.rotation;
        if (currpos!= startpos || body.angularVelocity.magnitude > 1f)
        {
            Vector3 dist = startpos - transform.position;
            if (body.velocity.magnitude > maxvel)
            {
                dist.x = -slowingCoefficient * body.velocity.x;
                dist.y = -slowingCoefficient * body.velocity.y;
                dist.z = -slowingCoefficient * body.velocity.z;
            }
            switch (mode)
            {
                case fmode.vel:
                    body.AddForce(velMultiplier * dist , ForceMode.Force);
                    break;
                case fmode.accel:
                    body.AddForce(velMultiplier * dist, ForceMode.Acceleration);
                    break;
                case fmode.impulse:
                    body.AddForce(velMultiplier * dist, ForceMode.Impulse);
                    break;
            }
            body.AddTorque(-angularSlowCoefficient * body.angularVelocity);
        }
    }
}