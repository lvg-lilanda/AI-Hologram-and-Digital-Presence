using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace HapE.Unity
{
    public static class GitConvertExtension
    {
        /// <summary>
        /// Converts 10 Digit GitHash to 8-digit Hexidecimal for display
        /// </summary>
        public static string HashAsHex(this uint gitHash)
        {
            return gitHash.ToString("X2");
        }
    }
}