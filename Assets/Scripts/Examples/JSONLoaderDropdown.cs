using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.IO;
using HapE.Unity;
public class JSONLoaderDropdown : MonoBehaviour
{
    public TMP_Dropdown dropdown;
    public TMP_InputField jsonRootInput;
    public Button refreshButton;
    public Button playButton;
    public bool isPlaying = false;

    public Sprite playSprite;
    public Sprite pauseSprite;

    [SerializeField]
    public string hapticRootPath = "";
    public string currentJSONPath = "";
    public HapEDeviceManager hapticDevice;
    public List<string> jsonFiles = new();

    // Start is called before the first frame update
    void Start()
    {
        hapticRootPath = Path.Combine(Application.streamingAssetsPath, "haptics");
        Debug.Log("hapticRootPath: " + hapticRootPath);
        jsonRootInput.text = hapticRootPath;
        LoadJSONFilesFromRootPath();
        jsonRootInput.onEndEdit.AddListener(RootInputFieldChanged);
        dropdown.onValueChanged.AddListener(DropdownChanged);
        dropdown.RefreshShownValue();
    }

    public void RootInputFieldChanged(string value)
    {
        hapticRootPath = value;
    }

    public void DropdownChanged(int value)
    {
        currentJSONPath = jsonFiles[value];
    }

    public void LoadJSONFilesFromRootPath()
    {
        jsonFiles.Clear();
        dropdown.ClearOptions();
        Debug.Log("hapticRootPath:" + hapticRootPath);
        DirectoryInfo dir = new DirectoryInfo(hapticRootPath);
        FileInfo[] info = dir.GetFiles("*.json");

        foreach (FileInfo f in info)
        {
            // For each Row, instantiate a new HaptiRowPrefb and store its data
            dropdown.options.Add(new TMP_Dropdown.OptionData() { text = f.Name});
            jsonFiles.Add(f.FullName);
        }

        dropdown.RefreshShownValue();
    }

    public void PlayButtonPressed()
    {
        if (isPlaying)
        {
            hapticDevice.StopHaptics();
            playButton.image.sprite = playSprite;
        }
        else
        {
            currentJSONPath = jsonFiles[dropdown.value];
            Debug.Log("Playing :" + currentJSONPath);
            hapticDevice.PlayHapEJSON(currentJSONPath);
            playButton.image.sprite = pauseSprite;
        }
        isPlaying = !isPlaying;
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyUp(KeyCode.Escape))
        {
            // TODO: raise dialog - are you sure you want to quit?
            Application.Quit();
        }
    }
}
