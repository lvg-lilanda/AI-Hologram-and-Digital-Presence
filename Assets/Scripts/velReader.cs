using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class velReader : MonoBehaviour
{
    //private Material material;
    //private Vector3 prev_pos;
    Rigidbody rb;
    Renderer rend;

    // Start is called before the first frame update
    void Start()
    {
        //material = GetComponent<Renderer>().material;
        //prev_pos = transform.position;
        rend = GetComponent<Renderer>();
        rb = GetComponent<Rigidbody>();

    }
    // Update is called once per frame
    void Update()
    {
        //Vector3 vel = (transform.position - prev_pos) / Time.deltaTime;

        //material.SetVector("vel", vel);
        //Debug.Log(rb.velocity);
        //prev_pos = transform.position;
        rend.material.SetVector("_veloc", rb.velocity);
    }
}
