using System.IO;
using UnityEngine;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace LookingGlass.Editor {
    public class LKGSettingsBuildPostProcessor : IPostprocessBuildWithReport {
        public int callbackOrder => 0;

        public void OnPostprocessBuild(BuildReport report) {
            if (File.Exists(LKGSettingsSystem.FileName)) {
                //NOTE: The BuildReport.summary.outputPath is...
                //  WINDOWS: The .exe file path itself.
                //  MACOS:  The .app file path.
                string folder = report.summary.outputPath;
                folder = Path.GetDirectoryName(folder);
                folder = folder.Replace('\\', '/');

                string outFilePath = Path.Combine(folder, LKGSettingsSystem.FileName).Replace('\\', '/');
                File.Copy(LKGSettingsSystem.FileName, outFilePath, true);

                Debug.Log("Copied " + LKGSettingsSystem.FileName + " alongside your build:\n" + outFilePath);
            }
        }
    }
}
