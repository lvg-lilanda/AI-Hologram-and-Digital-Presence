using System.Collections;
using System.Collections.Generic;
using System.Xml.Schema;
using UnityEngine;
using UnityEngine.Events;

public class randSpawn : MonoBehaviour
{
    [Header("Prefab")]
    public GameObject prefab;

    [Header("Spawn Area(local space")]
    public Vector3 areaSize = new Vector3(10, 10, 10);

    [Header("Random Scale")]
    public Vector2 scaleRange = new Vector2(0.5f, 2f);

    [Header("Random Rotation")]
    public bool randomRotation = true;

    //[Header("Parent Object")]
    //public Transform parent = transform;

    //[Header("Events")]
    //public UnityEvent onSpawn;

    public int spawnCount = 0;

    public void Spawn()
    {
        if (prefab == null) return;

        for (int i = 0 ; i < spawnCount; i++) 
        {
            //Random pos in area
            Vector3 randomPos = new Vector3(
                Random.Range(-areaSize.x / 2f, areaSize.x / 2f),
                Random.Range(-areaSize.y / 2f, areaSize.y / 2f),
                Random.Range(-areaSize.z / 2f, areaSize.z / 2f)
            );

            Vector3 worldPos = transform.TransformPoint(randomPos);

            // Random rotation
            Quaternion rotation = Quaternion.identity;
            if (randomRotation) rotation = Random.rotation;

            //Random Scale
            float s = Random.Range(scaleRange.x, scaleRange.y);
            Vector3 scale = new Vector3(s, s, s);

            //Instantiate
            GameObject obj = Instantiate(prefab, worldPos, rotation);
            obj.transform.localScale = scale;

            //onSpawn? Invoke()
        }
    }
}