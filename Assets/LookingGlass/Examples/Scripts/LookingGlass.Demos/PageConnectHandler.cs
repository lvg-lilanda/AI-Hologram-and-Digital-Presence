using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace LookingGlass.Demos {
    public class PageConnectHandler : PageHandler {
        protected override void OnNextClicked() {
            controller.OnLoadCalibration();
        }

        private void OnEnable()
        {
#if UNITY_IOS && !UNITY_EDITOR
            bool hasTwoDisplays = Display.displays.Length == 2;

            if (hasTwoDisplays)
                OnNextClicked();
#else
            if (LKGDisplaySystem.LKGDisplayCount > 0)
                controller.GoToLastPage();
#endif
        }
    }
}
