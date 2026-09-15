using UnityEngine;
using UnityEngine.UI;
using LookingGlass.Toolkit;

namespace LookingGlass.Demos
{
    public class PageTestHandler : MonoBehaviour {
        [SerializeField] private Button addCalibrationBt, clearTestImgBtn, doneButton;
        [SerializeField] private Toggle dontShowAgainToggle;
        [SerializeField] private Texture testQuilt;
        [SerializeField] private QuiltSettings testQuiltSetting;

        private DemoIOSUIController controller;
        private bool isEnabled = false;

        private void Start() {
            dontShowAgainToggle.onValueChanged.AddListener(onValueChanged);
            addCalibrationBt.onClick.AddListener(OnAddCalibration);
            clearTestImgBtn.onClick.AddListener(OnClearTestImage);
            doneButton.onClick.AddListener(OnDoneClicked);

            controller = GetComponentInParent<DemoIOSUIController>();
        }

        private void OnEnable() {
            // show test image
            if (HologramCamera.Instance.RenderStack[0].RenderType != RenderStep.Type.Quilt) {
                RenderStep step = new RenderStep(RenderStep.Type.Quilt);

                HologramCamera.Instance.RenderStack.Clear();
                HologramCamera.Instance.RenderStack.Add(step);
                HologramCamera.Instance.RenderStack.Add(new RenderStep(RenderStep.Type.CurrentHologramCamera));

                HologramCamera.Instance.RenderStack[0].QuiltSettings = testQuiltSetting;
            }

            HologramCamera.Instance.RenderStack[0].Texture = testQuilt;

            ToggleTestImage(true);

            dontShowAgainToggle.isOn = PageCalibrateHandler.DontShowAgain;
        }

        private void OnDisable()
        {
            OnClearTestImage();
        }

        private void onValueChanged(bool value)
        {
            PageCalibrateHandler.ToggleDontShowAgain(value);
        }

        private void OnAddCalibration()
        {
            controller.PickCalibrationFile(false);
        }

        private void OnClearTestImage()
        {
            ToggleTestImage(false);
        }

        private void OnShowTestImage()
        {
            ToggleTestImage(true);
        }

        private void ToggleTestImage(bool isEnabled)
        {
            if (isEnabled == this.isEnabled)
                return;

            this.isEnabled = isEnabled;

            if (HologramCamera.Instance != null)
            {
                // clear test image
                HologramCamera.Instance.RenderStack[0].IsEnabled = isEnabled;
                HologramCamera.Instance.RenderStack[1].IsEnabled = !isEnabled;               
            }

            if (!isEnabled)
            {
                clearTestImgBtn.onClick.RemoveListener(OnClearTestImage);
                clearTestImgBtn.GetComponentInChildren<Text>().text = "Show test image";
                clearTestImgBtn.onClick.AddListener(OnShowTestImage);
                HologramCamera.Instance.UseAutomaticQuiltSettings();
            }
            else
            {
                clearTestImgBtn.onClick.RemoveListener(OnShowTestImage);
                clearTestImgBtn.GetComponentInChildren<Text>().text = "Clear test image";
                clearTestImgBtn.onClick.AddListener(OnClearTestImage);
                HologramCamera.Instance.UseCustomQuiltSettings(HologramCamera.Instance.RenderStack[0].QuiltSettings);

            }
        }

        private void OnDoneClicked()
        {
            ToggleTestImage(false);
            controller.GoToLastPage();
        }
    }
}
