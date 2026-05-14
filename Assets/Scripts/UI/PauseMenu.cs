using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BallShooter
{
    /// <summary>
    /// In-game pause menu, toggled with X. Holds the sensitivity slider and
    /// resume / quit-to-main-menu buttons. Saves sensitivity to PlayerPrefs.
    /// </summary>
    public class PauseMenu : MonoBehaviour
    {
        const string PREF_SENS = "BallShooter_Sensitivity";
        const string PREF_FOV = "BallShooter_FOV";
        const string PREF_VOL = "BallShooter_MasterVolume";

        public PlayerLook look;
        public Camera fpCamera;
        public SoundFX sfx;

        Canvas canvas;
        GameObject panel;
        Slider sensSlider;
        Text sensValueText;
        Slider fovSlider;
        Text fovValueText;
        Slider volSlider;
        Text volValueText;
        bool paused;
        float prevTimeScale = 1f;

        public bool IsPaused => paused;

        void Awake()
        {
            BuildEventSystemIfMissing();
            BuildCanvas();
            panel.SetActive(false);
        }

        void Start()
        {
            // Refs are wired by bootstrap AFTER AddComponent, so we apply
            // saved values here instead of in Awake.
            if (look != null)
            {
                float saved = PlayerPrefs.GetFloat(PREF_SENS, look.sensitivity);
                look.sensitivity = saved;
                if (sensSlider != null) sensSlider.SetValueWithoutNotify(saved);
                UpdateSensLabel(saved);
            }
            if (fpCamera != null)
            {
                float saved = PlayerPrefs.GetFloat(PREF_FOV, fpCamera.fieldOfView);
                fpCamera.fieldOfView = saved;
                if (fovSlider != null) fovSlider.SetValueWithoutNotify(saved);
                UpdateFovLabel(saved);
            }
            if (sfx != null)
            {
                float saved = PlayerPrefs.GetFloat(PREF_VOL, sfx.masterVolume);
                sfx.masterVolume = saved;
                if (volSlider != null) volSlider.SetValueWithoutNotify(saved);
                UpdateVolLabel(saved);
            }
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.xKey.wasPressedThisFrame)
            {
                if (paused) Resume();
                else Pause();
            }
        }

        void Pause()
        {
            paused = true;
            prevTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            panel.SetActive(true);
        }

        public void Resume()
        {
            paused = false;
            Time.timeScale = prevTimeScale > 0f ? prevTimeScale : 1f;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            panel.SetActive(false);
        }

        void OnSensChanged(float v)
        {
            if (look != null) look.sensitivity = v;
            PlayerPrefs.SetFloat(PREF_SENS, v);
            UpdateSensLabel(v);
        }
        void UpdateSensLabel(float v)
        {
            if (sensValueText != null) sensValueText.text = $"Sensitivity: {v:0.000}";
        }

        void OnFovChanged(float v)
        {
            if (fpCamera != null) fpCamera.fieldOfView = v;
            // Update the shooter's stored "non-ADS" FOV so ADS-release returns here
            var shooter = fpCamera != null ? fpCamera.GetComponentInParent<PlayerShooter>() : null;
            if (shooter != null) shooter.baseFov = v;
            PlayerPrefs.SetFloat(PREF_FOV, v);
            UpdateFovLabel(v);
        }
        void UpdateFovLabel(float v)
        {
            if (fovValueText != null) fovValueText.text = $"FOV: {v:0}";
        }

        void OnVolChanged(float v)
        {
            if (sfx != null) sfx.masterVolume = v;
            PlayerPrefs.SetFloat(PREF_VOL, v);
            UpdateVolLabel(v);
        }
        void UpdateVolLabel(float v)
        {
            if (volValueText != null) volValueText.text = $"Volume: {Mathf.RoundToInt(v * 100)}%";
        }

        // ---------------- UI ----------------

        void BuildEventSystemIfMissing()
        {
            if (Object.FindFirstObjectByType<EventSystem>() != null) return;
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
        }

        void BuildCanvas()
        {
            var go = new GameObject("PauseCanvas");
            go.transform.SetParent(transform, false);
            canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            panel = new GameObject("PausePanel");
            panel.transform.SetParent(canvas.transform, false);
            var bg = panel.AddComponent<Image>();
            bg.color = new Color(0, 0, 0, 0.78f);
            var bgRT = bg.rectTransform;
            bgRT.anchorMin = Vector2.zero; bgRT.anchorMax = Vector2.one;
            bgRT.offsetMin = bgRT.offsetMax = Vector2.zero;

            // Container box (taller to fit extra sliders + controls list)
            var box = new GameObject("Box").AddComponent<Image>();
            box.transform.SetParent(panel.transform, false);
            box.color = new Color(0.08f, 0.09f, 0.13f, 0.95f);
            var boxRT = box.rectTransform;
            boxRT.anchorMin = boxRT.anchorMax = new Vector2(0.5f, 0.5f);
            boxRT.sizeDelta = new Vector2(820, 780);
            boxRT.anchoredPosition = Vector2.zero;

            // Title
            var title = CreateText("Title", box.transform, font, 64, TextAnchor.UpperCenter, "PAUSED");
            title.rectTransform.anchorMin = new Vector2(0, 1);
            title.rectTransform.anchorMax = new Vector2(1, 1);
            title.rectTransform.pivot = new Vector2(0.5f, 1);
            title.rectTransform.anchoredPosition = new Vector2(0, -30);
            title.rectTransform.sizeDelta = new Vector2(0, 80);
            title.color = new Color(1f, 0.92f, 0.5f);

            // ---- Sensitivity ----
            sensValueText = CreateText("SensValue", box.transform, font, 26, TextAnchor.MiddleCenter, "Sensitivity: 0.400");
            sensValueText.rectTransform.anchorMin = sensValueText.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            sensValueText.rectTransform.anchoredPosition = new Vector2(0, 250);
            sensValueText.rectTransform.sizeDelta = new Vector2(700, 32);

            sensSlider = CreateSlider("SensSlider", box.transform, 0.05f, 3.0f, 0.4f);
            sensSlider.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, 215);
            sensSlider.GetComponent<RectTransform>().sizeDelta = new Vector2(620, 30);
            sensSlider.onValueChanged.AddListener(OnSensChanged);

            var sensHint = CreateText("SensHint", box.transform, font, 14, TextAnchor.MiddleCenter,
                "Valorant scale — most players use 0.2 to 1.0");
            sensHint.rectTransform.anchorMin = sensHint.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            sensHint.rectTransform.anchoredPosition = new Vector2(0, 188);
            sensHint.rectTransform.sizeDelta = new Vector2(620, 20);
            sensHint.color = new Color(0.7f, 0.75f, 0.85f);

            // ---- FOV ----
            fovValueText = CreateText("FovValue", box.transform, font, 26, TextAnchor.MiddleCenter, "FOV: 90");
            fovValueText.rectTransform.anchorMin = fovValueText.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            fovValueText.rectTransform.anchoredPosition = new Vector2(0, 140);
            fovValueText.rectTransform.sizeDelta = new Vector2(700, 32);

            fovSlider = CreateSlider("FovSlider", box.transform, 60f, 120f, 90f);
            fovSlider.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, 105);
            fovSlider.GetComponent<RectTransform>().sizeDelta = new Vector2(620, 30);
            fovSlider.onValueChanged.AddListener(OnFovChanged);

            // ---- Volume ----
            volValueText = CreateText("VolValue", box.transform, font, 26, TextAnchor.MiddleCenter, "Volume: 80%");
            volValueText.rectTransform.anchorMin = volValueText.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            volValueText.rectTransform.anchoredPosition = new Vector2(0, 50);
            volValueText.rectTransform.sizeDelta = new Vector2(700, 32);

            volSlider = CreateSlider("VolSlider", box.transform, 0f, 1f, 0.8f);
            volSlider.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, 15);
            volSlider.GetComponent<RectTransform>().sizeDelta = new Vector2(620, 30);
            volSlider.onValueChanged.AddListener(OnVolChanged);

            // ---- Controls list ----
            string controlsList =
                "<b>WASD</b> move    <b>Space</b> jump (hold = auto-bhop)\n" +
                "<b>Shift</b> sprint   <b>Ctrl</b> crouch   <b>Shift+Ctrl</b> slide\n" +
                "<b>L-Click</b> fire   <b>R-Click</b> ADS   <b>R</b> reload\n" +
                "<b>1/2/3</b> primary / pistol / knife   <b>Mouse Wheel</b> cycle\n" +
                "<b>X</b> pause / settings";
            var controls = CreateText("Controls", box.transform, font, 18, TextAnchor.MiddleCenter, controlsList);
            controls.supportRichText = true;
            controls.rectTransform.anchorMin = controls.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            controls.rectTransform.anchoredPosition = new Vector2(0, -75);
            controls.rectTransform.sizeDelta = new Vector2(740, 130);
            controls.color = new Color(0.85f, 0.88f, 0.95f, 0.95f);

            // ---- Buttons ----
            var resume = CreateButton("Resume", box.transform, font, "RESUME",
                new Color(0.95f, 0.75f, 0.2f), new Color(1f, 0.85f, 0.35f), Color.black);
            resume.rectTransform.anchorMin = resume.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            resume.rectTransform.anchoredPosition = new Vector2(0, -190);
            resume.rectTransform.sizeDelta = new Vector2(360, 64);
            resume.GetComponent<Button>().onClick.AddListener(Resume);

            var quit = CreateButton("Quit", box.transform, font, "MAIN MENU",
                new Color(0.3f, 0.3f, 0.35f), new Color(0.5f, 0.5f, 0.55f), Color.white);
            quit.rectTransform.anchorMin = quit.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            quit.rectTransform.anchoredPosition = new Vector2(0, -270);
            quit.rectTransform.sizeDelta = new Vector2(280, 52);
            quit.GetComponent<Button>().onClick.AddListener(QuitToMenu);

            // Footer hint
            var foot = CreateText("Foot", box.transform, font, 16, TextAnchor.LowerCenter,
                "Press X to close");
            foot.rectTransform.anchorMin = new Vector2(0, 0);
            foot.rectTransform.anchorMax = new Vector2(1, 0);
            foot.rectTransform.pivot = new Vector2(0.5f, 0);
            foot.rectTransform.anchoredPosition = new Vector2(0, 14);
            foot.rectTransform.sizeDelta = new Vector2(0, 22);
            foot.color = new Color(0.7f, 0.75f, 0.85f, 0.85f);
        }

        void QuitToMenu()
        {
            // Unpause then reload scene; bootstrap will show the main menu again
            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        // ---------------- UI helpers ----------------

        static Text CreateText(string name, Transform parent, Font font, int size, TextAnchor anchor, string content)
        {
            var g = new GameObject(name);
            g.transform.SetParent(parent, false);
            var t = g.AddComponent<Text>();
            t.font = font;
            t.fontSize = size;
            t.alignment = anchor;
            t.text = content;
            t.color = Color.white;
            t.raycastTarget = false;
            var outline = g.AddComponent<Outline>();
            outline.effectColor = new Color(0, 0, 0, 0.85f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);
            return t;
        }

        static Image CreateButton(string name, Transform parent, Font font, string label,
                                  Color normal, Color highlighted, Color textColor)
        {
            var g = new GameObject(name);
            g.transform.SetParent(parent, false);
            var img = g.AddComponent<Image>();
            img.color = normal;
            var btn = g.AddComponent<Button>();
            var colors = btn.colors;
            colors.normalColor = normal;
            colors.highlightedColor = highlighted;
            colors.pressedColor = highlighted * 0.85f;
            colors.selectedColor = highlighted;
            colors.fadeDuration = 0.06f;
            btn.colors = colors;

            var labelGO = new GameObject("Label");
            labelGO.transform.SetParent(g.transform, false);
            var t = labelGO.AddComponent<Text>();
            t.font = font;
            t.fontSize = 34;
            t.alignment = TextAnchor.MiddleCenter;
            t.text = label;
            t.color = textColor;
            t.raycastTarget = false;
            var rt = t.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            return img;
        }

        static Slider CreateSlider(string name, Transform parent, float min, float max, float value)
        {
            var g = new GameObject(name);
            g.transform.SetParent(parent, false);
            var rt = g.AddComponent<RectTransform>();

            var slider = g.AddComponent<Slider>();
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = min;
            slider.maxValue = max;

            // Background
            var bg = new GameObject("Background").AddComponent<Image>();
            bg.transform.SetParent(g.transform, false);
            bg.color = new Color(0.2f, 0.21f, 0.25f, 1f);
            var bgRT = bg.rectTransform;
            bgRT.anchorMin = new Vector2(0, 0.25f);
            bgRT.anchorMax = new Vector2(1, 0.75f);
            bgRT.offsetMin = bgRT.offsetMax = Vector2.zero;

            // Fill area + fill
            var fillArea = new GameObject("Fill Area").AddComponent<RectTransform>();
            fillArea.SetParent(g.transform, false);
            fillArea.anchorMin = new Vector2(0, 0.25f);
            fillArea.anchorMax = new Vector2(1, 0.75f);
            fillArea.offsetMin = new Vector2(8, 0);
            fillArea.offsetMax = new Vector2(-8, 0);

            var fill = new GameObject("Fill").AddComponent<Image>();
            fill.transform.SetParent(fillArea, false);
            fill.color = new Color(0.95f, 0.75f, 0.2f);
            var fillRT = fill.rectTransform;
            fillRT.anchorMin = new Vector2(0, 0);
            fillRT.anchorMax = new Vector2(1, 1);
            fillRT.offsetMin = fillRT.offsetMax = Vector2.zero;

            // Handle area + handle
            var handleArea = new GameObject("Handle Slide Area").AddComponent<RectTransform>();
            handleArea.SetParent(g.transform, false);
            handleArea.anchorMin = new Vector2(0, 0);
            handleArea.anchorMax = new Vector2(1, 1);
            handleArea.offsetMin = new Vector2(10, 0);
            handleArea.offsetMax = new Vector2(-10, 0);

            var handle = new GameObject("Handle").AddComponent<Image>();
            handle.transform.SetParent(handleArea, false);
            handle.color = new Color(1f, 0.95f, 0.85f);
            var handleRT = handle.rectTransform;
            handleRT.sizeDelta = new Vector2(22, 0);
            handleRT.anchorMin = new Vector2(0, 0);
            handleRT.anchorMax = new Vector2(0, 1);

            slider.fillRect = fillRT;
            slider.handleRect = handleRT;
            slider.targetGraphic = handle;
            slider.value = value;
            return slider;
        }
    }
}
