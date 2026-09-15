using System;
using UnityEngine;

namespace HapE.Unity
{

    public class MathHelpers
    {
        public static float Remap(float input, float oldLow, float oldHigh, float newLow, float newHigh)
        {
            float t = Mathf.InverseLerp(oldLow, oldHigh, input);
            return Mathf.Lerp(newLow, newHigh, t);
        }


        // Return a linearly spaced array of numbers
        public static float[] Linspace(float StartValue, float EndValue, int numberofpoints)
        {

            float[] parameterVals = new float[numberofpoints];
            float increment = Math.Abs(StartValue - EndValue) / (numberofpoints - 1);
            int j = 0; //will keep a track of the numbers 
            float nextValue = StartValue;
            for (int i = 0; i < numberofpoints; i++)
            {
                parameterVals.SetValue(nextValue, j);
                j++;
                if (j > numberofpoints)
                {
                    throw new IndexOutOfRangeException();
                }
                nextValue = nextValue + increment;
            }
            return parameterVals;
        }

        public static float[] Identity3x3()
        {
            float[] T = new float[9];
            T[0] = T[4] = T[8] = 1;
            return T;
        }
        public static float InverseLerp(Vector3 a, Vector3 b, Vector3 value)
        {
            Vector3 AB = b - a;
            Vector3 AV = value - a;
            return Vector3.Dot(AV, AB) / Vector3.Dot(AB, AB);
        }

        /// <summary>
        /// Resampling Float array Method https://stackoverflow.com/questions/28874894/float-array-resampling
        /// </summary>
        /// <param name="source"></param>
        /// <param name="n"></param>
        /// <returns></returns>
        public static float[] Resample(float[] source, int n)
        {
            //n destination length
            int m = source.Length; //source length
            float[] destination = new float[n];
            destination[0] = source[0];
            destination[n - 1] = source[m - 1];

            for (int i = 1; i < n - 1; i++)
            {
                float jd = ((float)i * (float)(m - 1) / (float)(n - 1));
                int j = (int)jd;
                destination[i] = source[j] + (source[j + 1] - source[j]) * (jd - (float)j);
            }
            return destination;
        }
    }
}
