using System.IO;
using UnityEngine;
using Newtonsoft.Json;
namespace HapE.Unity
{
    public class HapESerializer : MonoBehaviour
    {
        /// <summary>
        /// Writes out Hap-E Data to the Haptics SDK schema for Hap-e JSON format. 
        /// </summary>
        /// <param name="data"></param>
        public void WriteHapEDataToJSON(HapEData data, string savePath = null)
        {
            if (savePath == null)
            {
                string filename = data.hapticName + ".json";
                string rootPath = Path.Combine(Application.streamingAssetsPath, "haptics");
                savePath = Path.Combine(rootPath, filename);
            }

            // Ensure we don't have any NaN values...
            data.primitive.SanitizeData();

            //Debug.Log("Save Path For HapEFormat JSON:" + savePath);
            string njson = JsonConvert.SerializeObject(data, Formatting.Indented, new JsonSerializerSettings()
            {
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
                NullValueHandling = NullValueHandling.Ignore
            });
            File.WriteAllText(savePath, njson);
        }

        public HapEData LoadHapEDataFromJSONPath(string jsonFilePath)
        {
            HapEData newHapEData = new HapEData();
            if (File.Exists(jsonFilePath))
            {
                // deserialize JSON directly from a file
                using (StreamReader file = File.OpenText(jsonFilePath))
                {
                    JsonSerializer serializer = new JsonSerializer();
                    newHapEData = (HapEData)serializer.Deserialize(file, typeof(HapEData));
                }
            }
            else
            {
                Debug.LogError("Cannot find file:" + jsonFilePath);
            }
            return newHapEData;
        }
    }
}