using System;
using System.Collections.Generic;
using UnityEngine;
using LookingGlass;

#if UNITY_EDITOR
using UnityEditor;
#endif

//TODO: Possibly use a custom namespace for this custom code?
namespace UnityEngine.Rendering.PostProcessing {
    /// <summary>
    /// Contains extensions to <see cref="HologramCamera"/> to support post-processing, by implementing callbacks during onto OnEnable and OnDisable.
    /// </summary>
#if UNITY_EDITOR
    [InitializeOnLoad]
#endif
    internal static class HologramCameraPostProcessSetup {
#if UNITY_EDITOR
        static HologramCameraPostProcessSetup() {
            RegisterCallbacks();
        }
#endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterCallbacks() {
            HologramCamera.UsePostProcessing = DetermineIfShouldUsePostProcessing;
        }

        private static bool DetermineIfShouldUsePostProcessing(HologramCamera hologramCamera) {
            if (hologramCamera.TryGetComponent(out PostProcessLayer layer) && layer.enabled) {
#if UNITY_POST_PROCESSING_STACK_V2
                return true;
#else
                Debug.LogWarning("WARNING: Looking Glass' fork of Unity post-processing is in the project, but UNITY_POST_PROCESSING_STACK_V2 is not defined in the project settings / script compilation symbols, so no post-processing will take effect.");
#endif
            }
            return false;
        }
    }
}
