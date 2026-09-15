using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;


public class scoreboardUpdater : MonoBehaviour
{
    public TextMeshProUGUI scoresText;
    private string fileName = "HoloHighscores.txt";

    void Start()
    {
        // Search for highscores file in persistant data
        // If it does not exist, it creates one
        string destPath = Path.Combine(Application.persistentDataPath, fileName);
        if (!File.Exists(destPath))
        {
            string sourcePath = Path.Combine(Application.streamingAssetsPath, fileName);
            File.Copy(sourcePath, destPath);
        }
        string fileContent = null;
        if (File.Exists(destPath))
        {
            fileContent = File.ReadAllText(destPath);
        }
        List<string> scores = new List<string>();

        if (fileContent != null)
        {
            // Scans through the file and adds all lines to a list
            using (StringReader reader = new StringReader(fileContent))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    scores.Add(line);
                }
            }

            // Splits the lines by the specified deliminator '.'
            // Orders them from highest to lowest to be displayed
            scores = scores.OrderByDescending(item =>
                int.Parse(item.Split('.')[1].Trim())
            ).ToList();
            
            // If there are more than 5 scores saved, only displays the top 5
            if (scores.Count <= 5)
            {
                for (int i = 0; i < scores.Count; i++)
                {
                    scoresText.text += (i + 1) + ": " + scores[i].Split('.')[0] + " - " + scores[i].Split('.')[1];
                    scoresText.text += "\n";
                }
            }
            // Otherwise print however many there is
            else
            {
                for (int i = 0; i < 5; i++)
                {
                    scoresText.text += (i + 1) + ": " + scores[i].Split('.')[0] + " - " + scores[i].Split('.')[1];
                    scoresText.text += "\n";
                }
            }
        }
        // Editor only: displays in log if it could not find the file for reading
        else Debug.Log("Couldnt find file for highscores");
    }
}