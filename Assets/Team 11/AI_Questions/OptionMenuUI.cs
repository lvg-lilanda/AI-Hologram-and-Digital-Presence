using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Team11.AI
{
    /// Builds the option buttons and the caption entirely in code, so there is no
    /// prefab to make and no TextMeshPro dependency. Deliberately plain: this is a
    /// working placeholder for Thursday, to be replaced by Hiba's design.
    ///
    /// Add it to any GameObject, assign Menu and Controller, press Play.
    public sealed class OptionMenuUI : MonoBehaviour
    {
        public MenuQuestionSource Menu;
        public AvatarController   Controller;

        public enum MenuSpace
        {
            /// Lives in the 3D scene, so the Hologram Camera renders it into every
            /// quilt view and it reads as part of the hologram. Also gives the buttons
            /// a real position, which is what Ultraleap needs to hit them.
            World,
            /// Drawn after all camera rendering, flat over the top. Fine on a monitor,
            /// wrong on a light field display. Kept so you can compare the two.
            ScreenOverlay,
        }

        [Header("Where the menu lives")]
        public MenuSpace Space = MenuSpace.World;

        [Tooltip("World mode: what the menu is positioned relative to. Usually the avatar. " +
                 "Falls back to this object.")]
        public Transform Anchor;

        [Tooltip("World mode: metres from the anchor. Default puts it to the avatar's right, " +
                 "at roughly chest height, matching Hiba's wireframe.")]
        public Vector3 WorldOffset = new Vector3(-0.75f, 1.35f, 0f);

        [Tooltip("World mode: canvas units to metres. 900 units at 0.0015 is about 1.35m wide.")]
        public float WorldScale = 0.0015f;

        [Tooltip("World mode: the camera the buttons raycast against. Leave empty to use " +
                 "the Hologram Camera or Camera.main.")]
        public Camera EventCamera;

        [Header("Layout")]
        public Vector2 PanelSize   = new Vector2(420f, 70f);
        public float   PanelLeft   = 60f;
        public float   PanelTop    = -160f;
        public float   Spacing     = 14f;
        public int     FontSize    = 22;

        private RectTransform _list;
        private Text          _caption;

        private void Awake()
        {
            EnsureEventSystem();
            var canvas = BuildCanvas();
            _list      = BuildListRoot(canvas);
            _caption   = BuildCaption(canvas);
        }

        private void OnEnable()
        {
            if (Menu != null)       Menu.OptionsChanged += Render;
            if (Controller != null) Controller.Spoke    += ShowCaption;
        }

        private void OnDisable()
        {
            if (Menu != null)       Menu.OptionsChanged -= Render;
            if (Controller != null) Controller.Spoke    -= ShowCaption;
        }

        private void ShowCaption(string line)
        {
            if (_caption != null) _caption.text = line;
        }

        private void Render(string[] ids)
        {
            for (int i = _list.childCount - 1; i >= 0; i--)
                Destroy(_list.GetChild(i).gameObject);

            if (ids == null) return;

            for (int i = 0; i < ids.Length; i++)
            {
                string id = ids[i];                       // captured per iteration
                var go = new GameObject($"Option_{id}", typeof(RectTransform),
                                        typeof(Image), typeof(Button));
                var rt = (RectTransform)go.transform;
                rt.SetParent(_list, false);
                rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot     = new Vector2(0f, 1f);
                rt.sizeDelta = PanelSize;
                rt.anchoredPosition = new Vector2(0f, -i * (PanelSize.y + Spacing));

                go.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.92f);
                go.GetComponent<Button>().onClick.AddListener(() => Menu.OnOptionChosen(id));

                var label = NewText(go.transform, Menu.Label(id), FontSize,
                                    TextAnchor.MiddleLeft, Color.black);
                var lrt = label.rectTransform;
                lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
                lrt.offsetMin = new Vector2(20f, 0f); lrt.offsetMax = new Vector2(-20f, 0f);
            }
        }

        // ---- construction -------------------------------------------------

        private static void EnsureEventSystem()
        {
            // Without one, buttons look fine and simply never fire.
            if (FindObjectOfType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem", typeof(EventSystem),
                                    typeof(StandaloneInputModule));
            go.hideFlags = HideFlags.DontSave;
        }

        private Canvas BuildCanvas()
        {
            var go = new GameObject("AI Menu Canvas", typeof(Canvas),
                                    typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.transform.SetParent(transform, false);
            var canvas = go.GetComponent<Canvas>();
            var scaler = go.GetComponent<CanvasScaler>();

            if (Space == MenuSpace.ScreenOverlay)
            {
                canvas.renderMode          = RenderMode.ScreenSpaceOverlay;
                scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                return canvas;
            }

            var cam = EventCamera != null ? EventCamera : Camera.main;
            canvas.renderMode  = RenderMode.WorldSpace;
            canvas.worldCamera = cam;                 // raycasting needs this in world space

            var rt = (RectTransform)go.transform;
            rt.sizeDelta  = new Vector2(900f, 700f);
            rt.localScale = new Vector3(WorldScale, WorldScale, WorldScale);
            rt.position   = (Anchor != null ? Anchor.position : transform.position) + WorldOffset;

            // Face the viewer. The Looking Glass camera does not move, so once is enough.
            if (cam != null) rt.forward = (rt.position - cam.transform.position).normalized;
            else Debug.LogWarning("[OptionMenuUI] No camera found for the world-space menu. " +
                                  "Assign Event Camera to the Hologram Camera.");
            return canvas;
        }

        private RectTransform BuildListRoot(Canvas canvas)
        {
            var go = new GameObject("Options", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(canvas.transform, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot     = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(PanelLeft, PanelTop);
            rt.sizeDelta = Vector2.zero;
            return rt;
        }

        private Text BuildCaption(Canvas canvas)
        {
            var t = NewText(canvas.transform, "", 26, TextAnchor.UpperLeft, Color.white);
            var rt = t.rectTransform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot     = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(PanelLeft, -60f);
            rt.sizeDelta = new Vector2(900f, 80f);
            return t;
        }

        private static Text NewText(Transform parent, string content, int size,
                                    TextAnchor anchor, Color colour)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<Text>();
            t.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
                       ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
            t.fontSize  = size;
            t.alignment = anchor;
            t.color     = colour;
            t.text      = content;
            return t;
        }
    }
}
