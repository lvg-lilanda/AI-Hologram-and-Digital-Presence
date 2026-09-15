using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using LookingGlass;
public class holoCameraInit : MonoBehaviour
{
    private HologramCamera holoCam;
    // Start is called before the first frame update
    void Start()
    {
        holoCam = GetComponent<HologramCamera>();
        StartCoroutine(Reinitialize());
        
    }

    IEnumerator Reinitialize()
    {
        yield return null;
        holoCam.enabled = false;
        yield return null;
        holoCam.enabled = true;
    }
}
