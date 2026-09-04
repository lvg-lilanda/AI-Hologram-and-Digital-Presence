using UnityEngine;
using Ultraleap.Haptics;
using System;

namespace HapE.Unity
{
    public class HapEParticleRenderer : MonoBehaviour
    {
        public HapEDeviceManager deviceManager;
        public ParticleSystem controlPointParticleSystem;
        public GameObject hapticPreviewObject;
        private ParticleSystem.Particle[] particlePoints_;

        [Tooltip("This transform allows us to override the simulation space for the particles, to handle multi-array")]
        public Transform simulationSpace;

        // TODO: Understand what the best number of control points is for render performance
        private ControlPointAtTime[] cps;
        public Color Color;

        [Range(0.001f, 0.05f)]
        public float Size = 0.01f;

        // Start is called before the first frame update
        void Start()
        {
            int ptCount = 400;
            if (deviceManager != null && deviceManager.hapticDevice != null)
            {
                ptCount = (int)(Time.fixedDeltaTime * deviceManager.hapticDevice.Evaluator.ControlPointUpdateFrequency);
            }

            cps = new ControlPointAtTime[ptCount];
            // Ensure that the particle System is always in a non-looping state - we update the particle positions every frame
            var main = controlPointParticleSystem.main;
            main.loop = false;

            // By default start up the Haptic Renderer in a Local Simulation Space -
            // This assumes that that Tracking Origin is offset relative to the array in Unity World Coords.
            SetLocalSimulationSpace();
        }

        public void SetLocalSimulationSpace()
        {
            // ANT: Honest to god, fix this Unity!
            var main = controlPointParticleSystem.main;
            if (main.simulationSpace != ParticleSystemSimulationSpace.Local)
            {
                main.simulationSpace = ParticleSystemSimulationSpace.Local;
            }
        }

        public void SetWorldSimulationSpace()
        {
            // ANT: Honest to god, fix this Unity!
            var main = controlPointParticleSystem.main;
            if (main.simulationSpace != ParticleSystemSimulationSpace.World)
            {
                main.simulationSpace = ParticleSystemSimulationSpace.World;
            }
        }

        /// <summary>
        /// This Custom space allows us to handle rendering of haptics for different arrays
        /// </summary>
        public void SetCustomSimulationSpace()
        {
            if (simulationSpace != null)
            {
                // ANT: Honest to god, fix this Unity!
                var main = controlPointParticleSystem.main;
                main.simulationSpace = ParticleSystemSimulationSpace.Custom;
                main.customSimulationSpace = simulationSpace;
            }
        }

        private void CreateControlPointParticles(ControlPointAtTime[] focalPoints, Color color, float radius)
        {
            // The number of particles is driven by the History Size of Sensation Emitter
            if (focalPoints == null)
            {
                return;
            }
            int numPoints = focalPoints.Length;
            particlePoints_ = new ParticleSystem.Particle[numPoints];
            int ix = 0;
            foreach (ControlPointAtTime cp in focalPoints)
            {
                float x = float.IsNaN(cp.Point.PosX) ? 0f : cp.Point.PosX;
                float y = float.IsNaN(cp.Point.PosY) ? 0f : cp.Point.PosY;
                float z = float.IsNaN(cp.Point.PosZ) ? 0f : cp.Point.PosZ;
                float intensity = cp.Point.Intensity;
                particlePoints_[ix].position = new Vector3(x, z, y);
                particlePoints_[ix].startSize = radius;
                color.a = intensity;
                particlePoints_[ix].startColor = color;
                ix++;
            }

            controlPointParticleSystem?.SetParticles(particlePoints_, numPoints);
        }


        private void OnEnable()
        {
            controlPointParticleSystem.Clear();
            if (hapticPreviewObject != null)
            {
                hapticPreviewObject.SetActive(true);
            }
        }

        // Clear the history if Component is disabled
        private void OnDisable()
        {
            ClearParticles();
        }

        // Update is called once per frame
        void FixedUpdate()
        {
            if (deviceManager.hapticDevice != null && deviceManager.hapticDevice.HapE.IsEmitting)
            {
                Render();
            }
        }

        public void ClearParticles()
        {
            controlPointParticleSystem.Clear();
            controlPointParticleSystem.Stop();
            deviceManager.hapticDevice?.Evaluator.ClearControlPointHistory();
        }

        private void Render()
        {
            _ = (int)deviceManager.hapticDevice.Evaluator.ConsumeControlPointHistory(cps.AsSpan());
            CreateControlPointParticles(cps, Color, Size);
        }
    }
}