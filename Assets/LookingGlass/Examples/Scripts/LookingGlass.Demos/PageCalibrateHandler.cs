using UnityEngine;
using UnityEngine.UI;
using LookingGlass.Mobile;
using LookingGlass.Toolkit;

namespace LookingGlass.Demos
{
    public class PageCalibrateHandler : MonoBehaviour
    {
        private const string DONTSHOWAGAINKEY = "DONTSHOWAGAINCALIBRATE";
        public static bool DontShowAgain => PlayerPrefs.GetInt(DONTSHOWAGAINKEY, 0) == 1;
        public static void ToggleDontShowAgain(bool value) => PlayerPrefs.SetInt(DONTSHOWAGAINKEY, value ? 1 : 0);

        [SerializeField] private Button addCalibrationBt, showTestImgBtn, doneButton;
        [SerializeField] private Toggle dontShowAgainToggle;
        [SerializeField] private Text titleText;

        private DemoIOSUIController controller;

        private void Awake()
        {
            MobileDMAController.onCalibrationLoaded += SetTitle;
        }

        private void OnDestroy()
        {
            MobileDMAController.onCalibrationLoaded -= SetTitle;
        }

        private void Start()
        {
            dontShowAgainToggle.onValueChanged.AddListener(ToggleDontShowAgain);
            addCalibrationBt.onClick.AddListener(OnAddCalibration);
            showTestImgBtn.onClick.AddListener(OnShowTestImage);
            doneButton.onClick.AddListener(OnDoneClicked);

            controller = GetComponentInParent<DemoIOSUIController>();
        }

        private void OnEnable()
        {
            dontShowAgainToggle.isOn = DontShowAgain;
        }

        public void SetTitle(Calibration calibration)
        {
            titleText.text = "Using calibration for " + calibration.serial;
        }

        private void OnAddCalibration() => controller.PickCalibrationFile(false);
        private void OnShowTestImage() => controller.ShowTestPage();
        private void OnDoneClicked() => controller.GoToLastPage();
    }
}
