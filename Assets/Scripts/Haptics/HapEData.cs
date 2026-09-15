using System;
using System.Collections.Generic;
using UnityEngine;

namespace HapE.Unity
{
    /// <summary>
    /// Object which can be used to store the Hap-e Definition
    /// </summary>
    /// 
    using Ultraleap.Haptics;
    [Serializable]
    public class HapEData : ICloneable
    {
        public int version = 2;
        public int format_version = 1;

        [SerializeField]
        public PrimitiveProperties primitive;

        [SerializeField]
        public AnimatorProperties animator;

        [SerializeField]
        public EnvelopeProperties envelope;

        [SerializeField]
        public PainterProperties painter;

        // Hap-e Designer Specifica Fields (ignored by SDK)
        [Header("Designer-Specific")]
        // HapE-Unity Conventions for default Tracking Fixation
        // (See TrackingFixation.cs for more info
        // 0 - TrackPalm
        // 1 - Fixed
        // 2 - TrackHeight
        // 3 - Forcefield
        // 4 - TrackIndexFinger
        // 5 - TrackMiddleFinger
        // 6 - PinchCentroid
        // 7 - PinchPosition
        // 8 - WristPosition
        public int fixation_mode = 0;
        public Vector3 fixation_offset = new(0, 0, 0);
        public string hapticName;
        public string brushName;
        public string audioFilename;
        public string presetName;

        public TrackingFixation.Fixation GetFixation()
        {
            return (TrackingFixation.Fixation)fixation_mode;
        }
        public void SetFixation(TrackingFixation.Fixation fixation)
        {
            fixation_mode = (int)fixation;
        }

        public object Clone()
        {
            HapEData data = (HapEData)this.MemberwiseClone();
            return data;
        }
    }
    [Serializable]
    public class PrimitiveProperties
    {
        public float draw_frequency;
        public float A;
        public float B;
        public float? a;
        public float? b;
        public float? max_t;

        // These controls are never exposed in the UI
        public float? d;
        public float? k;

        public PrimitiveProperties() { }
        public PrimitiveProperties(float draw_frequency, float A, float B, float a, float b)
        {
            this.draw_frequency = draw_frequency;
            this.A = A;
            this.B = B;
            this.a = a;
            this.b = b;
        }

        public PrimitiveProperties(float draw_frequency, float A, float B)
        {
            this.draw_frequency = draw_frequency;
            this.A = A;
            this.B = B;
        }

        /// <summary>
        /// This is a hack to avoid writing NaN values into the JSON!
        /// </summary>
        public void SanitizeData()
        {
            if (float.IsNaN(this.d.Value))
            {
                this.d = null;
            }
            if (float.IsNaN(this.max_t.Value))
            {
                this.max_t = null;
            }
            if (float.IsNaN(this.k.Value))
            {
                this.k = null;
            }
        }
    }

    [Serializable]
    public class AnimatorProperties
    {
        public bool enabled = false;
        public float[] T1;
        public float[] T1a1;
        public float[] T1a2;
        public float[] T2;
        public float[] T2a1;
        public float[] T2a2;
        public int? T1a_reset_count;
        public int? T2a_reset_count;
        public int T1a1_switch_count;
        public int T1a2_switch_count;
        public int? T2a1_switch_count;
        public int? T2a2_switch_count;

        public void SetAnimatorTransform(HapE.V3AnimatorTransform hapETransform, float[] animatorTransform)
        {
            if (hapETransform == HapE.V3AnimatorTransform.T1)
            {
                this.T1 = animatorTransform;
            }
            else if (hapETransform == HapE.V3AnimatorTransform.T2)
            {
                this.T2 = animatorTransform;
            }
            else if (hapETransform == HapE.V3AnimatorTransform.T1A1)
            {
                this.T1a1 = animatorTransform;
            }
            else if (hapETransform == HapE.V3AnimatorTransform.T1A2)
            {
                this.T1a2 = animatorTransform;
            }
            else if (hapETransform == HapE.V3AnimatorTransform.T2A1)
            {
                this.T2a1 = animatorTransform;
            }
            else if (hapETransform == HapE.V3AnimatorTransform.T2A2)
            {
                this.T2a2 = animatorTransform;
            }
            else
            {
                Debug.LogWarning("Unable to set value for:" + hapETransform);
            }
        }

        public void SetAnimatorCommand(HapE.V3Command command, uint value)
        {
            if (command == HapE.V3Command.T1A1SwitchCount)
            {
                this.T1a1_switch_count = (int)value;
            }
            else if (command == HapE.V3Command.T2A1SwitchCount)
            {
                this.T2a1_switch_count = (int)value;
            }
            else if (command == HapE.V3Command.T1A2SwitchCount)
            {
                this.T1a2_switch_count = (int)value;
            }
            else if (command == HapE.V3Command.T2A2SwitchCount)
            {
                this.T2a2_switch_count = (int)value;
            }
        }
    }

    /// <summary>
    /// Object representing a Node in the Hap-e Intensity envelope 
    /// </summary>
    public struct EnvelopeNode
    {
        public float intensity;
        public float t;
        public int? transition_mode; // (0 = Linear (default), 1 = Cosine);

        public EnvelopeNode(float t, float intensity)
        {
            this.intensity = intensity;
            this.t = t;
            this.transition_mode = null;
        }

        public EnvelopeNode(float t, float intensity, int transition_mode)
        {
            this.intensity = intensity;
            this.t = t;
            this.transition_mode = transition_mode;
        }
    }

    [Serializable]
    public class EnvelopeProperties
    {
        public bool enabled = true;
        public List<EnvelopeNode> nodes;
        public List<Tuple<int, int>> links;
        public int mode = 0; // Envelope Mode (0 = Forward (default), 1 = Backward, 2 = PingPong, 3 = Random  
        public int? start_node;
        public int? end_node;
        public int? length; // the number of nodes in the envelope
        public int repeat_count = 0; // RepeatCount - defines number of loops.

        public HapE.V3EnvelopeMode EnvelopeMode()
        {
            switch (mode)
            {
                case 0:
                    return HapE.V3EnvelopeMode.Forward;
                case 1:
                    return HapE.V3EnvelopeMode.Backward;
                case 2:
                    return HapE.V3EnvelopeMode.PingPong;
                case 3:
                    return HapE.V3EnvelopeMode.Random;
                default:
                    return HapE.V3EnvelopeMode.Forward;
            }
        }
    }

    /// <summary>
    /// Object representing a positional node in the Hap-e Painter
    /// </summary>

    [Serializable]
    public struct PainterNode
    {
        public float x;
        public float y;
        public float z_rotation;
        public float x_scale;
        public float y_scale;
        public float t;
        public int? transition_mode; // (0 = Linear (default), 1 = Cosine);

        public PainterNode(float x, float y, float z_rotation, float scaleX, float scaleY, float t)
        {
            this.x = x;
            this.y = y;
            this.z_rotation = z_rotation;
            this.x_scale = scaleX;
            this.y_scale = scaleY;
            this.t = t;
            this.transition_mode = null;
        }

        public PainterNode(float x, float y, float z_rotation, float scaleX, float scaleY, float t, int transition_mode)
        {
            this.x = x;
            this.y = y;
            this.z_rotation = z_rotation;
            this.x_scale = scaleX;
            this.y_scale = scaleY;
            this.t = t;
            this.transition_mode = transition_mode;
        }
    }


    [Serializable]
    public class PainterProperties
    {
        public bool enabled = true;

        [SerializeField]
        [NonReorderable]
        public List<PainterNode> nodes;
        public List<Tuple<int, int>> links;
        public int mode = 0; // Playback Mode (0 = Forward (default), 1 = Backward, 2 = PingPong, 3 = Random 
        public int? start_node;
        public int? end_node;
        public int? length; // the number of nodes in the painter path
        public int repeat_count = 0; // RepeatCount - defines number of loops.

        public HapE.V3PainterMode PainterMode()
        {
            switch (mode)
            {
                case 0:
                    return HapE.V3PainterMode.Forward;
                case 1:
                    return HapE.V3PainterMode.Backward;
                case 2:
                    return HapE.V3PainterMode.PingPong;
                case 3:
                    return HapE.V3PainterMode.Random;
                default:
                    Debug.Log("DEFULAT CASE, FORWARD!");
                    return HapE.V3PainterMode.Forward;
            }
        }
    }

}