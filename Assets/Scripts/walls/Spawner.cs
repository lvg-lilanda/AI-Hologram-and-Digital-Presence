using System.Collections;
using UnityEngine;

public class Spawner : MonoBehaviour
{
    public Transform spawnPos1;
    public Transform spawnPos2;

    public GameObject prefab1;
    public GameObject prefab2;
    public GameObject prefab3;

    public Collider despawnCol;

    public float speed = 5f;
    public float spawnRate = 2f;

    private GameObject[] prefabs;

    void Start()
    {
        // Set all possible wall types and start spawning
        prefabs = new GameObject[] { prefab1, prefab2, prefab3 };
        StartCoroutine(SpawnRoutine());
    }

    IEnumerator SpawnRoutine()
    {
        // Every spawnRate seconds spawn a wall at Positions 1 and 2
        while (true)
        {
            SpawnAt(spawnPos1);
            SpawnAt(spawnPos2);

            yield return new WaitForSeconds(spawnRate);
        }
    }

    void SpawnAt(Transform spawnPoint)
    {
        // Select a random wall from the prefabs and spawn it at the set location
        GameObject prefab = prefabs[Random.Range(0, prefabs.Length)];
        GameObject obj = Instantiate(prefab, spawnPoint.position, spawnPoint.rotation);

        // Add a Script component to it to make it move
        obj.AddComponent<MovingObject>().Init(speed, despawnCol);
    }

    private class MovingObject : MonoBehaviour
    {
        private float speed;
        private Collider despawnCol;

        public void Init(float s, Collider d)
        {
            speed = s;
            despawnCol = d;
        }

        void Update()
        {
            // Move the wall in the direction of the player
            transform.Translate(Vector3.forward * ( -1* speed ) * Time.deltaTime);
        }

        void OnTriggerEnter(Collider other)
        {
            // If the wall hits the despawn collider, delete it for scene performance
            if (other == despawnCol)
            {
                Destroy(gameObject);
            }
        }
    }
}