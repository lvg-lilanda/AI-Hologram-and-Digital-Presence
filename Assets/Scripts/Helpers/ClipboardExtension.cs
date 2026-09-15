using UnityEngine;

namespace HapE.Unity
{
    public static class ClipboardExtension
    {
        /// <summary>
        /// Puts the string into the Clipboard.
        /// </summary>
        public static void CopyToClipboard(this string str)
        {
            GUIUtility.systemCopyBuffer = str;
            Debug.Log($"Copied to clipboard:\n{str}");
        }
    }
}