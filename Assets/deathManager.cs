using System.Collections;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.Experimental.AI;
using UnityEngine.SceneManagement;

public class deathManager : MonoBehaviour
{
    int score = scoreManager.playerScore;
    public TextMeshProUGUI scoresText;
    public TextMeshProUGUI savedText;
    public TMP_InputField playerInputField;
    void Start()
    {
        // Update the HUD with players score
        scoresText.text += "You Scored " + score + " Points!";
        // Auto select the input field so player does not have to use mouse
        playerInputField.Select();
    }

    public void SubmitInput()
    {
        // When the player enters their name do these checks:
        // Name is not empty
        // Name does not contain the deliminator '.'
        // Name is fewer than 11 characters in length
        // These parameters ensure scoreboard will not break/look odd
        string name = playerInputField.text;
        if (!string.IsNullOrEmpty(name) && !name.Contains('.') && name.Length <= 10)
        {
            Debug.Log(name + " was entered");
            using (StreamWriter sw = File.AppendText(Path.Combine(Application.persistentDataPath, "HoloHighscores.txt")))
            {
                sw.WriteLine(name + "." + score);
            }
            // Disable the box after submission so player cannot save their score
            // multiple times
            savedText.text = "Score successfully saved!";
            playerInputField.enabled = false;
        }
        // Let the player know what is wrong with their name if it could not be saved
        else if (name.Contains('.'))
        {
            savedText.text = "Name contains Invalid character";
            playerInputField.Select();
        }
        else if (name.Length > 10)
        {
            savedText.text = "Name contains too many characters (>10)";
            playerInputField.Select();
        }
    }
}
