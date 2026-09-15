using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class cubeSpawingCollision : MonoBehaviour
{
    public int maxSpawns = 1;
    public randSpawn spawner;
    int spawns = 0;
    private void OnTriggerEnter(Collider other)
    {
        if (spawns <  maxSpawns)
        spawner.Spawn();
        spawns++;
    }
}
