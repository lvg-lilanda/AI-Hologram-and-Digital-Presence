using System.Collections;
using UnityEngine;
using TMPro;
using System.IO;
using System.Collections.Generic;
using UnityEngine.UI;
using System.Linq;
using UnityEngine.SceneManagement;

public class targetMover : MonoBehaviour
{
    [Header("Spawning")]
    public Collider despawnCol;
    public float speed = 5f;
    public float spawnRate = 2f;
    public Transform[] spawnLocations;
    public GameObject targetPrefab;
    public Collider bulletCol;

    [Header("Game Settings")]
    public int maxHealth = 5;
    public int pointsPerClick = 10;
    public int speedIncreaseScore = 150;
    public float speedIncreaseAmount = 1f;

    [Header("HUD")]
    public TextMeshProUGUI scoreText;
    public TextMeshProUGUI healthText;

    private int health;
    private int score = 0;
    private int nextSpeedIncrease;


    void Start()
    {
        // Set relevant variables
        health = maxHealth;
        nextSpeedIncrease = speedIncreaseScore;

        // Display Health and Points on the HUD
        UpdateHUD();

        // Begins target spawning routine
        StartCoroutine(SpawnRoutine());
    }

    IEnumerator SpawnRoutine()
    {
        while (true)
        {
            // Gets a random spawnpos within the bounds
            Vector3 spawnPos = GetRandomSpawnPosition();

            // Creates a target
            GameObject obj = Instantiate(
                targetPrefab,
                spawnPos,
                Quaternion.identity
            );

            // Adds a script component to the target to make it move
            obj.AddComponent<MovingObject>().Init(
                speed,
                despawnCol,
                bulletCol,
                this
            );

            // Wait for spawnRate seconds before spawning another target
            yield return new WaitForSeconds(spawnRate);
        }
    }

    // For getting a random position within the bounds.
    Vector3 GetRandomSpawnPosition()
    {
        // Find the minimum and maximum X Y, Z bounds.
        float minX = spawnLocations[0].position.x;
        float maxX = spawnLocations[0].position.x;

        float minY = spawnLocations[0].position.y;
        float maxY = spawnLocations[0].position.y;

        float minZ = spawnLocations[0].position.z;
        float maxZ = spawnLocations[0].position.z;

        foreach (Transform point in spawnLocations)
        {
            Vector3 pos = point.position;

            if (pos.x < minX) minX = pos.x;
            if (pos.x > maxX) maxX = pos.x;

            if (pos.y < minY) minY = pos.y;
            if (pos.y > maxY) maxY = pos.y;

            if (pos.z < minZ) minZ = pos.z;
            if (pos.z > maxZ) maxZ = pos.z;
        }

        // Return a vector3 with a random coordinate within the min and max
        return new Vector3(
            Random.Range(minX, maxX),
            Random.Range(minY, maxY),
            Random.Range(minZ, maxZ)
        );
    }

    // For when the player successfully shoots a target
    public void AddScore()
    {
        // Updates the score variable and the HUD whenever player scores
        score += pointsPerClick;
        UpdateHUD();

        // If the score reaches the point threshould, the targets speed up
        if (score >= nextSpeedIncrease)
        {
            speed += speedIncreaseAmount;
            nextSpeedIncrease += speedIncreaseScore;
        }
    }

    // For when player misses target
    public void TargetMissed()
    {
        // Updates the health variable and the HUD whenever the player misses
        health--;
        UpdateHUD();

        // If the player runs out of health
        if (health <= 0)
        {
            // Save the score for use in death scene and load death scene
            scoreManager.playerScore = score;
            SceneManager.LoadScene(2);
        }
    }

    // Updates the health and score elements of the HUD
    void UpdateHUD()
    {
        scoreText.text = "Score: " + score;
        healthText.text = "Health: " + health;
    }

    // Script added to targets when instantiated
    // Contains information regarding movement and collision
    private class MovingObject : MonoBehaviour
    {
        private float speed;
        private Collider despawnCol;
        private targetMover manager;
        private Collider bulletCol;

        public void Init(float s, Collider d, Collider b, targetMover m)
        {
            speed = s;
            despawnCol = d;
            bulletCol = b;
            manager = m;
        }

        void Update()
        {
            // Move the target toward the player at specified speed
            transform.Translate(Vector3.forward * (-1 * speed) * Time.deltaTime);
        }

        // For desktop mode: Clicking the target add points rather than shooting
        void OnMouseDown()
        {
            manager.AddScore();
            Destroy(gameObject);
        }

        // Collision logic
        void OnTriggerEnter(Collider other)
        {
            // If the target hits the death plane
            if (other.gameObject == despawnCol.gameObject)
            {
                manager.TargetMissed();
                Destroy(gameObject);
            }
            // If the target is hit by a bullet
            if (other.gameObject.tag == "bullet")
            {
                manager.AddScore();
                Destroy(gameObject);
            }
        }
    }
}