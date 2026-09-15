using System.Collections.Generic;
using UnityEngine;
using System;

namespace HapE.Unity
{
    using Ultraleap.Haptics;
    /// <summary>
    /// The Default Storage Object for Hap-e Brush preset, for example Circle/Line/Dial
    /// </summary>
    [CreateAssetMenu(fileName = "HapEBrush", menuName = "Hap-e/New Brush")]
    public class HapEBrushDefault : ScriptableObject
    {
        public string brushName;

        [Serializable]
        public class HapEPrimitiveParameter
        {
            public HapE.V3PrimitiveParameter parameter;
            public float value;
        }
        [Header("Primitive Parameters")]
        [NonReorderable]
        public List<HapEPrimitiveParameter> primitiveParams;

        /// <summary>
        /// Get a PrimitiveProperties object for Serialisation
        /// </summary>
        /// 
        public PrimitiveProperties GetPrimitiveProperties()
        {
            PrimitiveProperties props = new();
            props.A = GetHapEPrimitiveParameterValue(HapE.V3PrimitiveParameter.BigA);
            props.B = GetHapEPrimitiveParameterValue(HapE.V3PrimitiveParameter.BigB);
            props.draw_frequency = GetHapEPrimitiveParameterValue(HapE.V3PrimitiveParameter.DrawFrequency);
            props.a = GetHapEPrimitiveParameterValue(HapE.V3PrimitiveParameter.a);
            props.b = GetHapEPrimitiveParameterValue(HapE.V3PrimitiveParameter.b);
            props.k = GetHapEPrimitiveParameterValue(HapE.V3PrimitiveParameter.k);

            // Special cases, multiples of Pi...
            props.d = GetHapEPrimitiveParameterValue(HapE.V3PrimitiveParameter.d);
            props.max_t = GetHapEPrimitiveParameterValue(HapE.V3PrimitiveParameter.MaxT);
            return props;
        }

        [Serializable]
        public class HapETransform
        {
            public HapE.V3AnimatorTransform animatorTransform;
            // Typically a 3x3 Matrix, in the case of T1, T1a1, T2a1 etc.
            public float[] transformArray;
        }

        [Header("Animator")]
        [NonReorderable]
        public List<HapETransform> animatorTransforms;


        public virtual void SetHapEAnimTransform(HapE.V3AnimatorTransform animTransform, float[] transformArray)
        {
            HapETransform transformToUpdate = new();

            foreach (HapETransform T in animatorTransforms)
            {
                if (animTransform == T.animatorTransform)
                {
                    transformToUpdate = T;
                    break;
                }
            }
            transformToUpdate.transformArray = transformArray;
        }

        public void SetBrushAnimatorTransformsFromHapEData(HapEData hapEData)
        {
            if (hapEData.animator != null && hapEData.animator.enabled)
            {
                SetHapEAnimTransform(HapE.V3AnimatorTransform.T1, hapEData.animator.T1);
                SetHapEAnimTransform(HapE.V3AnimatorTransform.T2, hapEData.animator.T2);
                SetHapEAnimTransform(HapE.V3AnimatorTransform.T1A1, hapEData.animator.T1a1);
                SetHapEAnimTransform(HapE.V3AnimatorTransform.T1A2, hapEData.animator.T1a2);
                SetHapEAnimTransform(HapE.V3AnimatorTransform.T2A1, hapEData.animator.T2a1);
                SetHapEAnimTransform(HapE.V3AnimatorTransform.T2A2, hapEData.animator.T2a2);
            }
        }

        public float[] GetHapEAnimatorTransform(HapE.V3AnimatorTransform animTransform)
        {
            HapETransform hapeTransform = new();
            foreach (HapETransform T in animatorTransforms)
            {
                if (animTransform == T.animatorTransform)
                {
                    hapeTransform = T;
                    break;
                }
            }
            return hapeTransform.transformArray;
        }



        // TODO: It's questionable to me whether a Brush should store the Painter Nodes...
        // This potentially has some issues whereby a Brush can define Painter points, but also the Brush can be
        // used as the objec to trace along a path.
        [Serializable]
        public struct HapEPainterNode
        {
            public float x;
            public float y;
            public float zRotation;
            public float scaleX;
            public float scaleY;
            public float time;
        }

        [Header("Painter")]
        [NonReorderable]
        public List<HapEPainterNode> painterNodes;
        public HapE.V3PainterMode painterMode;
        public uint painterRepeatCount = 0;

        [Serializable]
        public struct HapEEnvelopeNode
        {
            public float time;
            public float intensity;
        }

        [Header("Envelope")]
        [NonReorderable]
        public List<HapEEnvelopeNode> envelopeNodes;
        public HapE.V3EnvelopeMode envelopeMode;
        public uint envelopeRepeatCount = 0;

        public void SetHapEPrimitiveParameter(HapE.V3PrimitiveParameter parameter, float value)
        {
            HapEPrimitiveParameter paramToUpdate = new();

            foreach (HapEPrimitiveParameter param in primitiveParams)
            {
                if (parameter == param.parameter)
                {
                    paramToUpdate = param;
                    break;
                }
            }
            paramToUpdate.value = value;
        }

        /// <summary>
        /// Returns the value for a HapE.V3Primitive Parameter stored in the brush.
        /// </summary>
        /// <param name="parameter"></param>
        /// <returns></returns>
        public float GetHapEPrimitiveParameterValue(HapE.V3PrimitiveParameter parameter)
        {
            HapEPrimitiveParameter paramToUpdate = new();
            paramToUpdate.value = float.NaN;
            foreach (HapEPrimitiveParameter param in primitiveParams)
            {
                if (parameter == param.parameter)
                {
                    paramToUpdate = param;
                    break;
                }
            }
            return paramToUpdate.value;
        }


        [Serializable]
        public struct HapECommand
        {
            public HapE.V3Command command;
            public uint value;
        }

        [Header("Commands")]
        [NonReorderable]
        public List<HapECommand> hapECommands;

        public int GetHapECommandIntValue(HapE.V3Command command)
        {
            HapECommand cmd = new();
            foreach (HapECommand c in hapECommands)
            {
                if (command == c.command)
                {
                    cmd = c;
                    break;
                }
            }
            return (int)cmd.value;
        }

        public void SetHapECommandIntValue(HapE.V3Command command, int value)
        {
            Debug.Log("Brush: SetHapECommandIntValue");
            for (int ix = 0; ix < hapECommands.Count; ix++)
            {
                HapECommand cmd = hapECommands[ix];
                if (command == cmd.command)
                {
                    Debug.Log("Setting cmd.value: " + (uint)value + ", for command: " + command);
                    cmd.value = (uint)value;
                    return;
                }
            }
        }
    }
}