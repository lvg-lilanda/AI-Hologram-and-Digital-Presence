using System.Collections.Generic;
using UnityEngine;
using System;
using System.IO;

/// <summary>
/// Hap-E API usage examples
/// PainterNodes represent the position of pattern.
/// EnvelopeNodes represent the intensity envelope of the sensation.
/// See also HapticLibraryPlayer.cs, which plays Sensations serialised out to JSON (from Hap-e Designer for instance)
/// </summary>
namespace HapE.Unity
{
    using Ultraleap.Haptics;

    public class HapETest : MonoBehaviour
    {
        public HapEDeviceManager deviceManager;
        public bool useMockIfRequired = true;

        public bool playing = false;
        private float[] trackerTransform = new float[16];
        public float x = 0.0f;
        public float y = 0.0f;
        public float z = 0.2f;

        public List<Vector3> dummyPath = new List<Vector3>();

        [Header("Haptic Params")]
        public float bigA = 0.01f;
        public float bigB = 0.01f;
        public float a = 2f;
        public float b = 2f;
        public float drawFrequency = 40f;

        // Start is called before the first frame update
        void Start()
        {
            //deviceManager.ConnectToHapEDevice();
            dummyPath.Add(new Vector3(0.029f, 0.028f, 0f));
            dummyPath.Add(new Vector3(-0.032f, 0f, 0.333f));
            dummyPath.Add(new Vector3(0.036f, -0.022f, 0.667f));
            dummyPath.Add(new Vector3(0.03f, -0.050f, 1f));
        }

        public void SetHapticParamsFromBrushDefault(HapEBrushDefault brush)
        {
            deviceManager.ResetHapEState();
            foreach (HapEBrushDefault.HapEPrimitiveParameter primParam in brush.primitiveParams)
            {
                deviceManager.hapticDevice.HapE.V3PrimitiveSetParameter(primParam.parameter, primParam.value);
            }

            if (brush.animatorTransforms.Count > 0)
            {
                foreach (HapEBrushDefault.HapETransform t in brush.animatorTransforms)
                {
                    deviceManager.hapticDevice.HapE.V3AnimatorSetTransform(t.animatorTransform, t.transformArray);
                }
            }
            if (brush.hapECommands.Count > 0)
            {
                foreach (HapEBrushDefault.HapECommand cmd in brush.hapECommands)
                {
                    deviceManager.hapticDevice.HapE.V3SendCommand(cmd.command, cmd.value);
                }
            }

            uint painterNodeCount = (uint)brush.painterNodes.Count;
            uint painterNodeID = 0;
            if (brush.painterNodes.Count > 0)
            {
                foreach (HapEBrushDefault.HapEPainterNode node in brush.painterNodes)
                {
                    deviceManager.hapticDevice.HapE.V3PainterUpdateNode(painterNodeID, node.x, node.y, node.zRotation, node.scaleX, node.scaleY, node.time);
                    painterNodeID += 1;
                }
                deviceManager.hapticDevice.HapE.V3PainterSetLength(painterNodeCount);
                Debug.Log("brush.painterMode:" + brush.painterMode);
                deviceManager.hapticDevice.HapE.V3PainterSetMode(brush.painterMode);
                deviceManager.hapticDevice.HapE.V3PainterSetRepeatCount(brush.painterRepeatCount);
            }

            uint envelopeNodeCount = (uint)brush.envelopeNodes.Count;
            uint envelopeNodeID = 0;
            if (brush.envelopeNodes.Count > 0)
            {
                foreach (HapEBrushDefault.HapEEnvelopeNode node in brush.envelopeNodes)
                {
                    deviceManager.hapticDevice.HapE.V3EnvelopeUpdateNode(envelopeNodeID, node.intensity, node.time);
                    painterNodeID += 1;
                }
                deviceManager.hapticDevice.HapE.V3EnvelopeSetLength(envelopeNodeCount);
                deviceManager.hapticDevice.HapE.V3EnvelopeSetMode(brush.envelopeMode);
                deviceManager.hapticDevice.HapE.V3PainterSetRepeatCount(brush.envelopeRepeatCount);
            }
            PlayAtFixedLocation();
        }

        public void SetCircleParams()
        {
            Debug.Log("Setting 2cm Circle 50Hz");

            deviceManager.hapticDevice.HapE.V3PrimitiveSetParameter(HapE.V3PrimitiveParameter.BigA, 0.02f);
            deviceManager.hapticDevice.HapE.V3PrimitiveSetParameter(HapE.V3PrimitiveParameter.BigB, 0.02f);
            deviceManager.hapticDevice.HapE.V3PrimitiveSetParameter(HapE.V3PrimitiveParameter.DrawFrequency, drawFrequency);
            deviceManager.hapticDevice.HapE.V3SendCommand(HapE.V3Command.EnableAnimator, 0);
            PlayAtFixedLocation();
        }

        public void SetLineParams()
        {
            Debug.Log("Setting Fat Line");
            deviceManager.ResetHapEState();
            deviceManager.hapticDevice.HapE.V3PrimitiveSetParameter(HapE.V3PrimitiveParameter.BigA, 0.005f);
            deviceManager.hapticDevice.HapE.V3PrimitiveSetParameter(HapE.V3PrimitiveParameter.BigB, 0.05f);
            deviceManager.hapticDevice.HapE.V3PrimitiveSetParameter(HapE.V3PrimitiveParameter.DrawFrequency, 50);
            PlayAtFixedLocation();
        }

        public void SetDialParams()
        {
            deviceManager.ResetHapEState();
            deviceManager.hapticDevice.HapE.V3SendCommand(HapE.V3Command.EnableAnimator, 1);
            Debug.Log("Setting Dial Params");
            deviceManager.hapticDevice.HapE.V3PrimitiveSetParameter(HapE.V3PrimitiveParameter.BigA, 0.01f);
            deviceManager.hapticDevice.HapE.V3PrimitiveSetParameter(HapE.V3PrimitiveParameter.BigB, 0.01f);
            deviceManager.hapticDevice.HapE.V3PrimitiveSetParameter(HapE.V3PrimitiveParameter.DrawFrequency, 64);

            float[] T1 = MathHelpers.Identity3x3();
            T1[2] = 0.02f;
            deviceManager.hapticDevice.HapE.V3AnimatorSetTransform(HapE.V3AnimatorTransform.T1, T1);

            float[] T2a1 = MathHelpers.Identity3x3();
            T2a1[0] = 0.992546f;
            T2a1[1] = -0.121869f;
            T2a1[3] = 0.121869f;
            T2a1[4] = 0.992546f;
            deviceManager.hapticDevice.HapE.V3AnimatorSetTransform(HapE.V3AnimatorTransform.T2A1, T2a1);
            PlayAtFixedLocation();
        }

        public void SetDialParamTime(float time)
        {
            float radius = 0.03f;
            deviceManager.ResetHapEState();
            deviceManager.hapticDevice.HapE.V3SendCommand(HapE.V3Command.EnablePainter, 1);
            deviceManager.hapticDevice.HapE.V3SendCommand(HapE.V3Command.EnableAnimator, 1);
            Debug.Log("Setting Dial Params for a radius of:" + time.ToString());
            deviceManager.hapticDevice.HapE.V3PrimitiveSetParameter(HapE.V3PrimitiveParameter.BigA, 0.01f);
            deviceManager.hapticDevice.HapE.V3PrimitiveSetParameter(HapE.V3PrimitiveParameter.BigB, 0.01f);
            deviceManager.hapticDevice.HapE.V3PrimitiveSetParameter(HapE.V3PrimitiveParameter.DrawFrequency, 127);

            float[] T1 = MathHelpers.Identity3x3();
            T1[2] = radius;
            deviceManager.hapticDevice.HapE.V3AnimatorSetTransform(HapE.V3AnimatorTransform.T1, T1);
            deviceManager.hapticDevice.HapE.V3PainterUpdateNode(0, 0, 0, 0, 1.0f, 1.0f, time);
            deviceManager.hapticDevice.HapE.V3PainterUpdateNode(1, 0, 0, (float)Math.PI * 2, 1.0f, 1.0f, 0.0f);
            deviceManager.hapticDevice.HapE.V3PainterSetLength(2);
            deviceManager.hapticDevice.HapE.V3PainterSetRepeatCount(0);
            PlayAtFixedLocation();
        }

        public void SetLineScanParams()
        {
            Debug.Log("Setting Line Scan Params");
            deviceManager.ResetHapEState();
            deviceManager.hapticDevice.HapE.V3SendCommand(HapE.V3Command.EnableAnimator, 1);
            deviceManager.hapticDevice.HapE.V3PrimitiveSetParameter(HapE.V3PrimitiveParameter.BigA, 0.04f);
            deviceManager.hapticDevice.HapE.V3PrimitiveSetParameter(HapE.V3PrimitiveParameter.BigB, 0.001f);
            deviceManager.hapticDevice.HapE.V3PrimitiveSetParameter(HapE.V3PrimitiveParameter.DrawFrequency, 50);

            // Scan Up
            float lineLength = 0.008f;
            float[] T1a1 = MathHelpers.Identity3x3();
            T1a1[5] = lineLength / 2;
            deviceManager.hapticDevice.HapE.V3SendCommand(HapE.V3Command.T1A1SwitchCount, 13);
            deviceManager.hapticDevice.HapE.V3AnimatorSetTransform(HapE.V3AnimatorTransform.T1A1, T1a1);

            // Scan Down
            float[] T1a2 = MathHelpers.Identity3x3();
            T1a2[5] = -lineLength / 2;
            deviceManager.hapticDevice.HapE.V3SendCommand(HapE.V3Command.T1A2SwitchCount, 13);
            deviceManager.hapticDevice.HapE.V3AnimatorSetTransform(HapE.V3AnimatorTransform.T1A2, T1a2);
            PlayAtFixedLocation();
        }

        public void SetPainterScanParams()
        {
            Debug.Log("Setting Painter Scan Params");
            deviceManager.ResetHapEState();
            deviceManager.hapticDevice.HapE.V3SendCommand(HapE.V3Command.EnableAnimator, 0);
            deviceManager.hapticDevice.HapE.V3SendCommand(HapE.V3Command.EnableEnvelope, 1);
            deviceManager.hapticDevice.HapE.V3SendCommand(HapE.V3Command.EnablePainter, 1);

            deviceManager.hapticDevice.HapE.V3PrimitiveSetParameter(HapE.V3PrimitiveParameter.BigA, 0.06f);
            deviceManager.hapticDevice.HapE.V3PrimitiveSetParameter(HapE.V3PrimitiveParameter.BigB, 0.001f);
            deviceManager.hapticDevice.HapE.V3PrimitiveSetParameter(HapE.V3PrimitiveParameter.DrawFrequency, 127);

            deviceManager.hapticDevice.HapE.V3PainterUpdateNode(0, 0, -0.05f, 0, 1f, 1f, 3f);
            deviceManager.hapticDevice.HapE.V3PainterUpdateNode(1, 0, 0.05f, 0, 1f, 1f, 0f);
            deviceManager.hapticDevice.HapE.V3EnvelopeUpdateNode(0, 0f, 3f);
            deviceManager.hapticDevice.HapE.V3EnvelopeUpdateNode(1, 1f, 0f);

            deviceManager.hapticDevice.HapE.V3PainterSetLength(2);
            deviceManager.hapticDevice.HapE.V3EnvelopeSetLength(2);
            deviceManager.hapticDevice.HapE.V3PainterSetRepeatCount(0);
            deviceManager.hapticDevice.HapE.V3EnvelopeSetRepeatCount(0);
            PlayAtFixedLocation();
        }

        public void SetTriangleParams()
        {
            Debug.Log("Setting Triangle Params");
            deviceManager.ResetHapEState();
            deviceManager.hapticDevice.HapE.V3SendCommand(HapE.V3Command.EnableAnimator, 1);
            deviceManager.hapticDevice.HapE.V3SendCommand(HapE.V3Command.EnablePainter, 1);
            deviceManager.hapticDevice.HapE.V3PrimitiveSetParameter(HapE.V3PrimitiveParameter.BigA, 0.01f);
            deviceManager.hapticDevice.HapE.V3PrimitiveSetParameter(HapE.V3PrimitiveParameter.BigB, 0.01f);
            deviceManager.hapticDevice.HapE.V3PrimitiveSetParameter(HapE.V3PrimitiveParameter.DrawFrequency, 80);
            deviceManager.hapticDevice.HapE.V3PainterSetRepeatCount(0);
            deviceManager.hapticDevice.HapE.V3PainterSetLength(3);
            deviceManager.hapticDevice.HapE.V3PainterSetMode(HapE.V3PainterMode.Forward);
            deviceManager.hapticDevice.HapE.V3PainterUpdateNode(0, 0.04f, 0, 0, 1, 1, 0.33f);
            deviceManager.hapticDevice.HapE.V3PainterUpdateNode(1, 0, 0.04f, 0, 1, 1, 0.33f);
            deviceManager.hapticDevice.HapE.V3PainterUpdateNode(2, -0.04f, 0, 0, 1, 1, 0.33f);
            PlayAtFixedLocation();
        }

        // Allows the Hap-E Designer to set points
        public void SetTracingPath(List<Vector3> points)
        {
            Debug.Log("Setting Triangle Params");
            deviceManager.ResetHapEState();
            deviceManager.hapticDevice.HapE.V3SendCommand(HapE.V3Command.EnableAnimator, 1);
            deviceManager.hapticDevice.HapE.V3SendCommand(HapE.V3Command.EnablePainter, 1);
            deviceManager.hapticDevice.HapE.V3PrimitiveSetParameter(HapE.V3PrimitiveParameter.BigA, 0.01f);
            deviceManager.hapticDevice.HapE.V3PrimitiveSetParameter(HapE.V3PrimitiveParameter.BigB, 0.01f);
            deviceManager.hapticDevice.HapE.V3PrimitiveSetParameter(HapE.V3PrimitiveParameter.DrawFrequency, 80);
            deviceManager.hapticDevice.HapE.V3PainterSetRepeatCount(0);

            uint numPoints = (uint)points.Count;
            deviceManager.hapticDevice.HapE.V3PainterSetLength(numPoints);
            deviceManager.hapticDevice.HapE.V3PainterSetMode(HapE.V3PainterMode.PingPong);

            uint ix = 0;
            foreach (Vector3 pt in points)
            {
                // Hard-code time to be 1 second for the tracing for now
                deviceManager.hapticDevice.HapE.V3PainterUpdateNode(ix, pt.x, pt.y, 0, 1, 1, 1f);
                ix++;
            }
            PlayAtFixedLocation();
        }

        string GetAssetPath(string assetFilename)
        {
            string hapticRootPath = Path.Combine(Application.streamingAssetsPath, "haptics");
            DirectoryInfo dir = new DirectoryInfo(hapticRootPath);
            FileInfo[] info = dir.GetFiles(assetFilename);
            foreach (FileInfo f in info)
            {
                return f.ToString(); // only return first one
            }

            throw new Exception("File not found");
        }
        public void LoadAndPlayRotorSalutTest()
        {
            deviceManager.PlayHapEJSON(GetAssetPath("Rotor Salut.json"));
        }

        public void LoadAndPlayButtonClickTest()
        {
            deviceManager.PlayHapEJSON(GetAssetPath("08.Button Click.json"));
        }

        public void LoadAndPlayButtonDoubleClickTest()
        {
            deviceManager.PlayHapEJSON(GetAssetPath("09.Button Double Tap.json"));
        }

        public void DoubleTapCircleTest()
        {
            SetDoubleTap();
        }
        // A double-tap Circle haptic
        public void SetDoubleTap(float preDelay = 0f,
            float onTime = 0.33f,
            float offTime = 0.15f,
            float postDelay = 0f)
        {
            Debug.Log("Setting 2cm Circle 50Hz");
            deviceManager.ResetHapEState();
            deviceManager.hapticDevice.HapE.V3PrimitiveSetParameter(HapE.V3PrimitiveParameter.BigA, 0.01f);
            deviceManager.hapticDevice.HapE.V3PrimitiveSetParameter(HapE.V3PrimitiveParameter.BigB, 0.01f);
            deviceManager.hapticDevice.HapE.V3PrimitiveSetParameter(HapE.V3PrimitiveParameter.DrawFrequency, 80f);
            deviceManager.hapticDevice.HapE.V3SendCommand(HapE.V3Command.EnableEnvelope, 1);
            deviceManager.hapticDevice.HapE.V3EnvelopeSetMode(HapE.V3EnvelopeMode.Forward);

            // This sets the number of times the envelope repeats.
            deviceManager.hapticDevice.HapE.V3EnvelopeSetRepeatCount(1);

            // This sets the number of nodes in the envelope
            deviceManager.hapticDevice.HapE.V3EnvelopeSetLength(9);

            // Set keyframes for: WARMUP-ON-OFF-ON-OFF-COOLDOWN
            deviceManager.hapticDevice.HapE.V3EnvelopeUpdateNode(0, 0, preDelay);
            deviceManager.hapticDevice.HapE.V3EnvelopeUpdateNode(1, 1, 0);
            deviceManager.hapticDevice.HapE.V3EnvelopeUpdateNode(2, 1, onTime);
            deviceManager.hapticDevice.HapE.V3EnvelopeUpdateNode(3, 0, 0);
            deviceManager.hapticDevice.HapE.V3EnvelopeUpdateNode(4, 0, offTime);
            deviceManager.hapticDevice.HapE.V3EnvelopeUpdateNode(5, 1, 0);
            deviceManager.hapticDevice.HapE.V3EnvelopeUpdateNode(6, 1, onTime);
            deviceManager.hapticDevice.HapE.V3EnvelopeUpdateNode(7, 0, 0);
            deviceManager.hapticDevice.HapE.V3EnvelopeUpdateNode(8, 0, postDelay);

            PlayAtFixedLocation();
        }

        public void PlayAtFixedLocation()
        {
            UpdateTrackerTransform();
            deviceManager.hapticDevice.HapE.Start();
            playing = true;
        }

        public void SetCircleOpenParams()
        {
            Debug.Log("Setting Circle Open Params");
            deviceManager.ResetHapEState();
            deviceManager.hapticDevice.HapE.V3SendCommand(HapE.V3Command.EnableAnimator, 1);
            deviceManager.hapticDevice.HapE.V3PrimitiveSetParameter(HapE.V3PrimitiveParameter.BigA, 0.02f);
            deviceManager.hapticDevice.HapE.V3PrimitiveSetParameter(HapE.V3PrimitiveParameter.BigB, 0.02f);
            deviceManager.hapticDevice.HapE.V3PrimitiveSetParameter(HapE.V3PrimitiveParameter.DrawFrequency, 50);

            float scaleValue = 0.9f;

            // Scale Down
            float[] T1a1 = new float[9];
            T1a1[0] = 1 / scaleValue;
            T1a1[4] = 1 / scaleValue;
            T1a1[8] = 1f;
            deviceManager.hapticDevice.HapE.V3SendCommand(HapE.V3Command.T1A1SwitchCount, 20);
            deviceManager.hapticDevice.HapE.V3AnimatorSetTransform(HapE.V3AnimatorTransform.T1A1, T1a1);

            // Scale Up
            float[] T1a2 = new float[9];
            T1a2[0] = scaleValue;
            T1a2[4] = scaleValue;
            T1a2[8] = 1f;
            deviceManager.hapticDevice.HapE.V3SendCommand(HapE.V3Command.T1A2SwitchCount, 20);
            deviceManager.hapticDevice.HapE.V3AnimatorSetTransform(HapE.V3AnimatorTransform.T1A2, T1a2);
            UpdateTrackerTransform();
            deviceManager.hapticDevice.HapE.Start();
        }

        // Test path tracing from x-y-t vectors
        public void SetDummyPath()
        {
            SetTracingPath(dummyPath);
        }

        /// <summary>
        /// Update Drawing Params
        /// </summary>
        /// <param name="value"></param>
        /// 
        public void SetBigA(float value)
        {
            bigA = value;
            deviceManager.hapticDevice.HapE.V3PrimitiveSetParameter(HapE.V3PrimitiveParameter.BigA, bigA);
        }
        public void SetBigB(float value)
        {
            bigB = value;
            deviceManager.hapticDevice.HapE.V3PrimitiveSetParameter(HapE.V3PrimitiveParameter.BigB, bigB);
        }
        public void SetA(float value)
        {
            a = value;
            deviceManager.hapticDevice.HapE.V3PrimitiveSetParameter(HapE.V3PrimitiveParameter.a, a);
        }
        public void SetB(float value)
        {
            b = value;
            deviceManager.hapticDevice.HapE.V3PrimitiveSetParameter(HapE.V3PrimitiveParameter.b, b);
        }

        public void SetDrawFrequnecy(float value)
        {
            drawFrequency = value;
            deviceManager.hapticDevice.HapE.V3PrimitiveSetParameter(HapE.V3PrimitiveParameter.DrawFrequency, drawFrequency);
        }

        public void UpdateTrackerTransform()
        {
            trackerTransform[0] = 1f;
            trackerTransform[3] = x;
            trackerTransform[5] = 1f;
            trackerTransform[7] = y;
            trackerTransform[10] = 1f;
            trackerTransform[11] = z;
            trackerTransform[15] = 1f;
            deviceManager.hapticDevice.HapE.V3SetTracker(trackerTransform);
        }

        public void TogglePlayback()
        {
            UpdateTrackerTransform();
            if (playing)
            {
                deviceManager.hapticDevice.HapE.Stop();
            }
            else
            {
                deviceManager.hapticDevice.HapE.Start();
            }
            playing = !playing;
        }


        // Update is called once per frame
        void Update()
        {
            if (Input.GetKeyUp("space"))
            {
                TogglePlayback();
            }


        }
    }
}