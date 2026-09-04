//using System.Collections;
//using System.Collections.Generic;
//using UnityEngine;
//using UnityEngine.SceneManagement;

//public class GunShoot : MonoBehaviour
//{
//    public GameObject bulletPrefab;
//    public Transform firePoint;
//    public float bulletSpeed = 20f;
//    public float bulletLifeTime = 5f;

//    public void Shoot()
//    {
//        // If the prefab or firelocation are not set properly, return null
//        if (bulletPrefab == null || firePoint == null)
//        {
//            Debug.LogError("Set objects in editor");
//            return;
//        }

//        // Create a bullet
//        GameObject bullet = Instantiate(
//            bulletPrefab,
//            firePoint.position,
//            firePoint.rotation
//        );

//        Rigidbody rb = bullet.GetComponent<Rigidbody>();
//        // If it does not contain a rigidbody, return null
//        if (rb == null)
//        {
//            Debug.LogError("Bullet prefab needs rigidbody");
//            return;
//        }

//        // Move the bullet with force towards direction fired
//        // Destroys the bullet after bulletLifeTime seconds for scene performance
//        rb.velocity = firePoint.right * bulletSpeed;
//        Destroy(bullet, bulletLifeTime);
//    }
//}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GunShoot : MonoBehaviour
{
    public GameObject bulletPrefab;
    public Transform firePoint;
    public float bulletSpeed = 20f;
    public float bulletLifeTime = 5f;

    //Audio clips assigned in the inspector
    [Header("Audio")]
    [SerializeField] AudioClip shootSound;

    public void Shoot()
    {
        // If the prefab or firelocation are not set properly, return null
        if (bulletPrefab == null || firePoint == null)
        {
            Debug.LogError("Set objects in editor");
            return;
        }

        // Create a bullet
        GameObject bullet = Instantiate(
            bulletPrefab,
            firePoint.position,
            firePoint.rotation
        );

        Rigidbody rb = bullet.GetComponent<Rigidbody>();
        // If it does not contain a rigidbody, return null
        if (rb == null)
        {
            Debug.LogError("Bullet prefab needs rigidbody");
            return;
        }

        // Move the bullet with force towards direction fired
        // Destroys the bullet after bulletLifeTime seconds for scene performance
        rb.velocity = firePoint.right * bulletSpeed;
        Destroy(bullet, bulletLifeTime);

        //Play shooting sound
        AudioSource.PlayClipAtPoint(shootSound, firePoint.position );
    }
}