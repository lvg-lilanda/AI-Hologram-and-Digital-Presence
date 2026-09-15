using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class menuButtons : MonoBehaviour
{
    // Bools to set type of button in editor
    public bool isStartButton = true;
    public bool isMenuButton = false;

    void OnTriggerEnter(Collider other)
    {
        // When button is hit by a bullet
        if (other.gameObject.tag == "bullet")
        {
            // If its a start button
            if (isStartButton && !isMenuButton)
            {
                // Load main scene
                SceneManager.LoadScene(1);
                Debug.Log("Game Started");
            }
            // if its menu button
            else if (isMenuButton && !isStartButton)
            {
                // Load Menu
                SceneManager.LoadScene(0);
            }
            // If its quit button, Close application
            else Application.Quit();
        }
    }
}
