//Copyright 2017-2021 Looking Glass Factory Inc.
//All rights reserved.
//Unauthorized copying or distribution of this file, and the source code contained herein, is strictly prohibited.

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using LookingGlass.Toolkit;

namespace LookingGlass {
    [ExecuteAlways]
    //REVIEW: Do we still have this page?
    // [HelpURL("https://docs.lookingglassfactory.com/Unity/Scripts/SimpleDOF/")]
    public class SimpleDOF : MonoBehaviour {
        [SerializeField] private HologramCamera camera;

        [Header("DoF Curve")]
        [SerializeField] private float start = -1.5f;
        [SerializeField] private float dip = -0.5f;
        [SerializeField] private float rise =  0.5f;
        [SerializeField] private float end =  2;

        [Header("Blur")]
        [Range(0, 2)]
        [SerializeField] private float blurSize = 1;
        [SerializeField] private bool horizontalOnly = true;
        [SerializeField] private bool testFocus;

        private Material passdepthMaterial;
        private Material boxBlurMaterial;
        private Material finalpassMaterial;

        private void OnEnable() {
            if (!TryEnsureCameraExists())
                return;
            passdepthMaterial = new Material(Shader.Find("LookingGlass/DOF/Pass Depth"));
            boxBlurMaterial   = new Material(Shader.Find("LookingGlass/DOF/Box Blur"));
            finalpassMaterial = new Material(Shader.Find("LookingGlass/DOF/Final Pass"));
        }

        private void OnDisable() {
            if (passdepthMaterial != null)
                Material.DestroyImmediate(passdepthMaterial);
            if (boxBlurMaterial != null)
                Material.DestroyImmediate(boxBlurMaterial);
            if (finalpassMaterial != null)
                Material.DestroyImmediate(finalpassMaterial);
        }

        private bool TryEnsureCameraExists() {
            if (camera == null) {
                camera = GetComponentInParent<HologramCamera>();
                if (camera == null) {
                    enabled = false;
                    Debug.LogWarning("[LookingGlass] Simple DOF needs to be on a LookingGlass capture's camera");
                    return false;
                }
            }
            return true;
        }

        public void DoDOF(RenderTexture src, RenderTexture srcDepth) {
            if (!TryEnsureCameraExists())
                return;

            // // make sure the LookingGlass is capturing depth
            // camera.cam.depthTextureMode = DepthTextureMode.Depth;

            Vector4 dofParams = new Vector4(start, dip, rise, end) * camera.CameraProperties.Size;
            dofParams = new Vector4(
                1.0f / (dofParams.x - dofParams.y),
                dofParams.y,
                dofParams.z,
                1.0f / (dofParams.w - dofParams.z)
            );
            boxBlurMaterial.SetVector("dofParams", dofParams);
            boxBlurMaterial.SetFloat("focalLength", camera.CameraProperties.FocalPlane);
            finalpassMaterial.SetInt("testFocus", testFocus ? 1 : 0);
            if (horizontalOnly)
                Shader.EnableKeyword("_HORIZONTAL_ONLY");
            else
                Shader.DisableKeyword("_HORIZONTAL_ONLY");

            RenderTexture fullres = RenderTexture.GetTemporary(src.width, src.height, 0);
            RenderTexture blur1 = RenderTexture.GetTemporary(src.width / 2, src.height / 2, 0);
            RenderTexture blur2 = RenderTexture.GetTemporary(src.width / 3, src.height / 3, 0);
            RenderTexture blur3 = RenderTexture.GetTemporary(src.width / 4, src.height / 4, 0);

            Shader.SetGlobalVector("ProjParams", new Vector4(
                1, 
                camera.SingleViewCamera.nearClipPlane, 
                camera.SingleViewCamera.farClipPlane, 
                1
            ));

            QuiltSettings quiltSettings = camera.QuiltSettings;
            Vector4 tile = new Vector4(
                quiltSettings.columns,
                quiltSettings.rows,
                quiltSettings.tileCount,
                quiltSettings.columns * quiltSettings.rows
            );
            Vector4 viewPortion = new Vector4(
                quiltSettings.ViewPortionHorizontal,
                quiltSettings.ViewPortionVertical
            );
            boxBlurMaterial.SetVector("tile", tile);
            boxBlurMaterial.SetVector("viewPortion", viewPortion);
            finalpassMaterial.SetVector("tile", tile);
            finalpassMaterial.SetVector("viewPortion", viewPortion);

            //Passes: Start with depth
            passdepthMaterial.SetTexture("QuiltDepth", srcDepth);
            Graphics.Blit(src, fullres, passdepthMaterial);

            //Blur 1
            boxBlurMaterial.SetInt("blurPassNum", 0);
            boxBlurMaterial.SetFloat("blurSize", blurSize * 2);
            Graphics.Blit(fullres, blur1, boxBlurMaterial);

            //Blur 2
            boxBlurMaterial.SetInt("blurPassNum", 1);
            boxBlurMaterial.SetFloat("blurSize", blurSize * 3);
            Graphics.Blit(fullres, blur2, boxBlurMaterial);

            //Blur 3
            boxBlurMaterial.SetInt("blurPassNum", 2);
            boxBlurMaterial.SetFloat("blurSize", blurSize * 4);
            Graphics.Blit(fullres, blur3, boxBlurMaterial);

            finalpassMaterial.SetTexture("blur1", blur1);
            finalpassMaterial.SetTexture("blur2", blur2);
            finalpassMaterial.SetTexture("blur3", blur3);

            Graphics.Blit(fullres, src, finalpassMaterial);

            // disposing of stuff
            RenderTexture.ReleaseTemporary(fullres);
            RenderTexture.ReleaseTemporary(blur1);
            RenderTexture.ReleaseTemporary(blur2);
            RenderTexture.ReleaseTemporary(blur3);
        }
    }
}
