using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;
/// <summary>
/// Object used when loading JSON for getting the Leap Tracker transform.
/// Can also re-write new Transfrom to: StreamingAssets/config/TrackerOrigin.json
/// </summary>
public class TrackingOriginTransform
{
    public List<float> trackingPosition = new List<float>() { 0.0f, 0.0f, 0.121f};
    public List<float> trackingRotation = new List<float>() { 0.0f, 0.0f, 0.0f };
    public List<float> arrayPosition = new List<float>() { 0.0f, 0.0f, 0.0f};
    public List<float> arrayRotation = new List<float>() { 0.0f, 0.0f, 0.0f };
    public string defaultTrackerOriginJSONFilename = "config/TrackerOrigin.json";

    public void SaveToTrackingOriginJSON(string savePath = null)
    {
        if (savePath == null)
        {
            string filename = defaultTrackerOriginJSONFilename;
            string rootPath = Path.Combine(Application.streamingAssetsPath);
            savePath = Path.Combine(rootPath, filename);
        }

        //Debug.Log("Save Path For HapEFormat JSON:" + savePath);
        string njson = JsonConvert.SerializeObject(this, Formatting.Indented, new JsonSerializerSettings()
        {
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
            NullValueHandling = NullValueHandling.Ignore
        });
        File.WriteAllText(savePath, njson);
    }
}
