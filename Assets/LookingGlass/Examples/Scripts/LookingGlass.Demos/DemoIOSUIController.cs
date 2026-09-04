using System;
using System.IO;
using UnityEngine;
using LookingGlass.Mobile;

namespace LookingGlass.Demos {
    public class DemoIOSUIController : MonoBehaviour {
        public static event Action onLKGDisplaySetup;

        [SerializeField] private GameObject[] pages;
        [SerializeField] private GameObject popUpCalibrate, popUpTestImage;
        [SerializeField] private GameObject templateScene;

        public enum PageType
        {
            None = -1,
            GetStarted = 0,
            Connect = 1,
            LoadCalibration = 2,
            Done = 3
        };

        public PageType CurrentPageType => (PageType)(currentPage);

        public static bool isCalibrationFileExist =>
            File.Exists(MobileDMAController.VisualJsonFilePath);

        private MobileDMAController dMAController;
        private int currentPage = -1;
        private bool firstSession = false;

        private void OnValidate()
        {
#if UNITY_EDITOR
            if (LKGDisplaySystem.LKGDisplayCount > 0)
            {
                GetComponent<Canvas>().targetDisplay = 1;
                Debug.LogWarning("Please view the UI on Display 2 in Editor");
            }
            else
                GetComponent<Canvas>().targetDisplay = 0;
#endif
        }

        private void OnEnable()
        {
            Display.onDisplaysUpdated += AutoPageUpdate;
        }

        private void OnDisable()
        {
            Display.onDisplaysUpdated -= AutoPageUpdate;
        }

        private void Awake()
        {
            firstSession = !isCalibrationFileExist;
#if !UNITY_EDITOR
            GetComponent<Canvas>().targetDisplay = 0;
#endif
        }

        void Start() {
            dMAController = FindObjectOfType<MobileDMAController>();
            if (dMAController == null) {
                Debug.LogError("MobileDMAController is missing.");
                this.enabled = false;
            }

            templateScene.SetActive(false);

            int i = 0;
            foreach (GameObject page in pages) {
                if (page == null)
                {
                    Debug.LogWarning("Page is missing");
                    continue;
                }
                PageHandler pageHandler = page.GetComponent<PageHandler>();
                if (pageHandler == null)
                {
                    Debug.LogError("PageHandler is missing on Page " + i);
                    continue;
                }
                page.GetComponent<PageHandler>().SetPageIndex(i);
                i++;
                page.SetActive(false);
            }
            HideCalibratePage();
            HideTestPage();

            if (firstSession)
                SwitchToPage(PageType.GetStarted);
            else
                SwitchToPage(PageType.Connect);

        }

        private void AutoPageUpdate()
        {
            bool hasTwoDisplays = Display.displays.Length == 2;
            if (hasTwoDisplays)
            {
                bool notUpdateForFirstSession = firstSession && CurrentPageType == PageType.GetStarted;

                if (notUpdateForFirstSession
                    || CurrentPageType > PageType.Connect)
                    return;

                OnLoadCalibration();
            }
            else
            {
                // Debug.Log("display disconnected at page " + CurrentPageType);
                HideCalibratePage();
                HideTestPage();
                templateScene.SetActive(false);

                if (CurrentPageType <= PageType.Connect)
                    return;

                SwitchToPage(PageType.Connect);
            }
        }

        public void OnLoadCalibration()
        {
            if (!isCalibrationFileExist)
            {
                // Debug.Log("No calibration file exists. Go to Load Calibration File Page");
                // go to next pageh
                SwitchToPage(PageType.LoadCalibration);
            }
            else
            {
                // check player prefs
                if (!PageCalibrateHandler.DontShowAgain)
                {
                    ShowCalibratePage();
                    // Debug.Log("Calibration file exists. And dont-show-again is not on. Show Calibration Page");
                }
                else
                {
                    GoToLastPage();
                    // Debug.Log("Detect calibration file exists. And dont-sho- again is on. Go to template scene");
                }
            }
        }

        public void GoToNextPage() =>
            InternalSwitchToPage(currentPage + 1);
        public void GoToPreviousPage() =>
            InternalSwitchToPage(currentPage - 1);
        public void GoToLastPage() {
            InternalSwitchToPage(pages.Length - 1);
            HideCalibratePage();
            HideTestPage();
            templateScene.SetActive(true);
        }
        public void ShowCalibratePage() => ToggleCalibratePage(true);
        public void HideCalibratePage() => ToggleCalibratePage(false);
        public void ShowTestPage() => ToggleTestImagePage(true);
        public void HideTestPage() => ToggleTestImagePage(false);

        private void ToggleCalibratePage(bool isOn) {
            popUpCalibrate.SetActive(isOn);
        }

        private void ToggleTestImagePage(bool isOn) {
            popUpTestImage.SetActive(isOn);
        }

        public void SwitchToPage(PageType pageType) => InternalSwitchToPage((int)pageType);
        public void SwitchToPage(int pageIndex) => InternalSwitchToPage(pageIndex);

        private void InternalSwitchToPage(int pageIndex)
        {
            if (currentPage == pageIndex)
                return;

            if (currentPage >= 0 && currentPage < pages.Length && pages[currentPage] != null)
                pages[currentPage].gameObject.SetActive(false);

            currentPage = pageIndex;

            if (pageIndex >= 0 && pageIndex < pages.Length && pages[pageIndex] != null)
                pages[pageIndex].gameObject.SetActive(true);
        }

        public void HandleLoadVisualJson()
        {
            // On iOS pick a visual.json file
            // On Mac / Editor, just refresh the displays which includes reloading the calibration visual.json        
#if UNITY_IOS
            PickCalibrationFile(true);
#else
            dMAController.RefreshDisplays();
            GoToNextPage();
#endif
        }

        public void PickCalibrationFile(bool goNextWhenDone)
        {
#if UNITY_IOS
            // Don't attempt to import/export files if the file picker is already open
            //if( NativeFilePicker.IsFilePickerBusy() )
            //	return;
            // Use UTIs on iOS
            string fileType = NativeFilePicker.ConvertExtensionToFileType("txt");
            Debug.Log("txt's MIME/UTI is: " + fileType);
            string[] fileTypes = new string[] { fileType, "public.text", "public.plain-text" };
            NativeFilePicker.Permission permission = NativeFilePicker.PickFile((path) =>
            {
                if (path == null)
                    Debug.Log("File Picker Operation cancelled");
                else
                {
                    Debug.Log("Picked file: " + path);
                    dMAController.LoadedVisualJsonPath = path;

                    onLKGDisplaySetup?.Invoke();

                    if (goNextWhenDone)
                        GoToLastPage(); 
                }
            }, fileTypes);
            Debug.Log("Permission result: " + permission);
#else
            Debug.LogWarning("Calibration file picking is only supported on iOS.");
#endif
        }
    }
}
