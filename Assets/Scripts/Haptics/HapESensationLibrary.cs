using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Loads HapE JSON from StreamingAssets and stores them in an accessible list for HapticLibraryPlayer
/// </summary>
namespace HapE.Unity {
    public class HapESensationLibrary : MonoBehaviour
    {
        // Start is called before the first frame update
        [Tooltip("Handles loading/writing of HapE JSON.")]
        public HapESerializer serializer;

        [Tooltip("A list of StreamingAssets folder paths, inside which Hap-e JSON files live.")]
        public List<string> streamingAssetSearchPaths = new();

        [Tooltip("The HapESensation objects, loaded from JSON.")]
        public List<HapESensation> sensations = new();

        public UnityEvent OnSensationLibraryUpdated;

        public bool refreshOnAwake = true;

        [Tooltip("A list of the names of Sensations found in the library. HapticLibraryPlayer.cs can playback haptics via PlaySensationWithName('HapticName')")]
        public List<string> hapticNames = new();
        void Awake()
        {
            if (serializer == null)
            {
                serializer = gameObject.AddComponent(typeof(HapESerializer)) as HapESerializer;
            }

            // Include default paths if none set...
            if (streamingAssetSearchPaths.Count == 0)
            {
                streamingAssetSearchPaths.Add("haptics");
                streamingAssetSearchPaths.Add("hap-e_sensations");
            }

            if (refreshOnAwake)
            {
                RefreshSensations();
            }
        }

        /// <summary>
        /// Returns true if the Current Library contains a sensation with the given name.
        /// </summary>
        /// <param name="sensationName"></param>
        /// <returns></returns>
        public bool ContainsSensationWithName(string sensationName)
        {
            foreach (HapESensation sensation in sensations)
            {
                // We cannot guarantee uniqueness of naming.. We just find the first
                // possible sensation with a matching name...
                if (sensation.HapticName == sensationName)
                {
                    return true;
                }
            }
            return false;
        }

        public void RefreshSensations(List<string> overrideSearchPaths = null)
        {
            hapticNames.Clear();
            sensations.Clear();
            if (overrideSearchPaths != null)
            {
                streamingAssetSearchPaths = overrideSearchPaths;
            }
            
            foreach (string subPath in streamingAssetSearchPaths)
            {
                //Debug.Log("HapESensationLibrary, RefreshSensations, looking for haptics in sub-path: " + subPath);
                LoadHapticsFromStreamingAssetsSubPath(subPath);
            }
            GetSensationNameList();
            OnSensationLibraryUpdated.Invoke();
        }

        /// <summary>
        /// Returns the latest list of HapEData Sensations available
        /// </summary>
        /// <returns></returns>
        public List<string> GetSensationNameList()
        {
            hapticNames.Clear();
            foreach (HapESensation sensation in sensations)
            {
                hapticNames.Add(sensation.HapticName);
            }
            return hapticNames;
        }

        /// <summary>
        /// Returns the first Sensation with a given hapticName;
        /// Note: This does not guarantee a unique Haptic, if identical named haptics exist in the library.
        /// </summary>
        /// <param name="hapticName"></param>
        /// <returns></returns>
        public HapESensation GetSensationByHapticName(string n)
        {
            foreach (HapESensation sensation in sensations)
            {
                if (sensation.HapticName == n)
                {
                    return sensation;
                }
            }
            return null;
        }

        /// <summary>
        /// Returns the first HapEData with a given hapticName;
        /// Note: This does not guarantee a unique Haptic, if identical named haptics exist in the library.
        /// </summary>
        /// <param name="hapticName"></param>
        /// <returns></returns>
        public HapEData GetHapEDataByHapticName(string n)
        {
            HapEData newData = new();
            foreach (HapESensation sensation in sensations)
            {
                if (sensation.HapticName == n)
                {
                    newData = sensation.hapeData;
                    return newData;
                }
            }
            return newData;
        }

        public void AddSensationFromJSONPath(string jsonPath)
        {
            // For ecah file, instantiate a new HapESensation Scriptable Object
            HapESensation sensation = ScriptableObject.CreateInstance<HapESensation>();
            sensation.hapeData = serializer.LoadHapEDataFromJSONPath(jsonPath.ToString());
            sensation.name = sensation.HapticName;
            if (!sensations.Contains(sensation))
            {
                sensations.Add(sensation);
            }
        }

        public void AddSensationToLibray(HapESensation sensation)
        {
            if (!sensations.Contains(sensation))
            {
                sensations.Add(sensation);
            }
        }

        public HapESensation GetSensationFromJSONPath(string jsonPath)
        {
            // For ecah file, instantiate a new HapESensation Scriptable Object
            HapESensation sensation = ScriptableObject.CreateInstance<HapESensation>();
            sensation.hapeData = serializer.LoadHapEDataFromJSONPath(jsonPath.ToString());
            sensation.name = sensation.HapticName;
            return sensation;
        }

        // Returns a list of named haptics for valid JSON found in StreamingAssets
        public void LoadHapticsFromStreamingAssetsSubPath(string subPath)
        {
            string hapticRootPath = Path.Combine(Application.streamingAssetsPath, subPath);
            if (!Directory.Exists(hapticRootPath))
            {
                return;
            }
            DirectoryInfo dir = new(hapticRootPath);
            FileInfo[] info = dir.GetFiles("*.json");

            //sensations.Clear();
            foreach (FileInfo f in info)
            {
                AddSensationFromJSONPath(f.ToString());
            }
        }
    }
}