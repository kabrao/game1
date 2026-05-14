using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BallShooter
{
    /// <summary>
    /// Procedurally built main menu. Shown first; the arena/game only spawns
    /// after the player presses PLAY. Builds its own EventSystem if needed.
    /// </summary>
    public class MainMenu : MonoBehaviour
    {
        const string PREF_PRIMARY = "BallShooter_PrimaryWeapon";

        Canvas canvas;
        GameObject root;
        Camera menuCamera;
        Image smgBtn, assaultBtn, sniperBtn;
        WeaponType chosenPrimary = WeaponType.Assault;

        public static void Show()
        {
            // If a menu instance already exists, don't double-build
            if (Object.FindFirstObjectByType<MainMenu>() != null) return;
            var go = new GameObject("MainMenu");
            go.AddComponent<MainMenu>();
        }

        void Awake()
        {
            BuildMenuCamera();
            BuildEventSystemIfMissing();
            BuildCanvas();
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        void BuildMenuCamera()
        {
            // If no active camera, create a simple one so the screen isn't black.
            // (Bootstrap disables the original SampleScene camera.)
            var existing = Camera.main;
            if (existing != null && existing.gameObject.activeInHierarchy && existing.enabled) return;

            var camGO = new GameObject("MenuCamera");
            camGO.tag = "MainCamera";
            menuCamera = camGO.AddComponent<Camera>();
            menuCamera.clearFlags = CameraClearFlags.SolidColor;
            menuCamera.backgroundColor = new Color(0.08f, 0.09f, 0.12f);
            menuCamera.transform.position = new Vector3(0, 1.6f, -8);
            camGO.AddComponent<AudioListener>();
        }

        void BuildEventSystemIfMissing()
        {
            if (Object.FindFirstObjectByType<EventSystem>() != null) return;
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
        }

        void BuildCanvas()
        {
            root = new GameObject("MenuCanvas");
            root.transform.SetParent(transform, false);
            canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            root.AddComponent<GraphicRaycaster>();

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // Background panel (gradient feel via two stacked images)
            var bg = new GameObject("BG").AddComponent<Image>();
            bg.transform.SetParent(root.transform, false);
            bg.color = new Color(0.05f, 0.06f, 0.08f, 1f);
            var bgRT = bg.rectTransform;
            bgRT.anchorMin = Vector2.zero;
            bgRT.anchorMax = Vector2.one;
            bgRT.offsetMin = bgRT.offsetMax = Vector2.zero;
            bg.raycastTarget = false;

            var accent = new GameObject("Accent").AddComponent<Image>();
            accent.transform.SetParent(root.transform, false);
            accent.color = new Color(0.18f, 0.08f, 0.25f, 0.55f);
            var aRT = accent.rectTransform;
            aRT.anchorMin = new Vector2(0, 0);
            aRT.anchorMax = new Vector2(1, 0.55f);
            aRT.offsetMin = aRT.offsetMax = Vector2.zero;
            accent.raycastTarget = false;

            // Title
            var title = CreateText("Title", root.transform, font, 120, TextAnchor.MiddleCenter, "BALL SHOOTER");
            title.rectTransform.anchorMin = new Vector2(0, 0.7f);
            title.rectTransform.anchorMax = new Vector2(1, 0.95f);
            title.rectTransform.offsetMin = title.rectTransform.offsetMax = Vector2.zero;
            title.color = new Color(1f, 0.93f, 0.55f);

            var subtitle = CreateText("Sub", root.transform, font, 28, TextAnchor.MiddleCenter,
                "Survive the rounds. Don't get touched. Pop every ball.");
            subtitle.rectTransform.anchorMin = new Vector2(0, 0.62f);
            subtitle.rectTransform.anchorMax = new Vector2(1, 0.7f);
            subtitle.rectTransform.offsetMin = subtitle.rectTransform.offsetMax = Vector2.zero;
            subtitle.color = new Color(0.8f, 0.85f, 0.95f);

            // Load saved primary
            chosenPrimary = (WeaponType)PlayerPrefs.GetInt(PREF_PRIMARY, (int)WeaponType.Assault);

            // ---- Primary weapon picker (3 buttons) ----
            var pickerLabel = CreateText("PickerLabel", root.transform, font, 22, TextAnchor.MiddleCenter, "PRIMARY WEAPON");
            pickerLabel.rectTransform.anchorMin = pickerLabel.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            pickerLabel.rectTransform.anchoredPosition = new Vector2(0, 175);
            pickerLabel.rectTransform.sizeDelta = new Vector2(500, 30);
            pickerLabel.color = new Color(0.85f, 0.88f, 0.95f);

            smgBtn = CreateButton("SmgBtn", root.transform, font, "SMG",
                new Color(0.25f, 0.3f, 0.45f), new Color(0.4f, 0.55f, 0.85f), Color.white);
            smgBtn.rectTransform.anchorMin = smgBtn.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            smgBtn.rectTransform.anchoredPosition = new Vector2(-200, 110);
            smgBtn.rectTransform.sizeDelta = new Vector2(180, 56);
            smgBtn.GetComponent<Button>().onClick.AddListener(() => PickPrimary(WeaponType.SMG));

            assaultBtn = CreateButton("AssaultBtn", root.transform, font, "ASSAULT",
                new Color(0.4f, 0.3f, 0.18f), new Color(0.75f, 0.55f, 0.25f), Color.white);
            assaultBtn.rectTransform.anchorMin = assaultBtn.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            assaultBtn.rectTransform.anchoredPosition = new Vector2(0, 110);
            assaultBtn.rectTransform.sizeDelta = new Vector2(180, 56);
            assaultBtn.GetComponent<Button>().onClick.AddListener(() => PickPrimary(WeaponType.Assault));

            sniperBtn = CreateButton("SniperBtn", root.transform, font, "SNIPER",
                new Color(0.45f, 0.2f, 0.4f), new Color(0.85f, 0.4f, 0.85f), Color.white);
            sniperBtn.rectTransform.anchorMin = sniperBtn.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            sniperBtn.rectTransform.anchoredPosition = new Vector2(200, 110);
            sniperBtn.rectTransform.sizeDelta = new Vector2(180, 56);
            sniperBtn.GetComponent<Button>().onClick.AddListener(() => PickPrimary(WeaponType.Sniper));

            // Stats blurb for the currently-selected primary
            var pickerHint = CreateText("PickerHint", root.transform, font, 16, TextAnchor.MiddleCenter,
                "Pistol and Knife are always included.");
            pickerHint.rectTransform.anchorMin = pickerHint.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            pickerHint.rectTransform.anchoredPosition = new Vector2(0, 70);
            pickerHint.rectTransform.sizeDelta = new Vector2(500, 22);
            pickerHint.color = new Color(0.7f, 0.75f, 0.85f);

            RefreshPickerHighlight();

            // Play button
            var playBtn = CreateButton("PlayBtn", root.transform, font, "PLAY",
                new Color(0.95f, 0.75f, 0.2f), new Color(1f, 0.85f, 0.35f), Color.black);
            playBtn.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            playBtn.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            playBtn.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            playBtn.rectTransform.anchoredPosition = new Vector2(0, -10);
            playBtn.rectTransform.sizeDelta = new Vector2(380, 90);
            playBtn.GetComponent<Button>().onClick.AddListener(OnPlay);

            // Quit button
            var quitBtn = CreateButton("QuitBtn", root.transform, font, "QUIT",
                new Color(0.3f, 0.3f, 0.35f), new Color(0.5f, 0.5f, 0.55f), Color.white);
            quitBtn.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            quitBtn.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            quitBtn.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            quitBtn.rectTransform.anchoredPosition = new Vector2(0, -130);
            quitBtn.rectTransform.sizeDelta = new Vector2(280, 70);
            quitBtn.GetComponent<Button>().onClick.AddListener(OnQuit);

            // Controls hint (bottom)
            string controls =
                "<b>WASD</b> move   <b>Mouse</b> aim   <b>Space</b> jump\n" +
                "<b>Shift</b> sprint   <b>Ctrl</b> crouch   <b>Shift+Ctrl</b> slide\n" +
                "<b>Left Click</b> shoot   <b>R</b> reload   <b>X</b> pause / settings";
            var hint = CreateText("Controls", root.transform, font, 22, TextAnchor.LowerCenter, controls);
            hint.supportRichText = true;
            hint.rectTransform.anchorMin = new Vector2(0, 0);
            hint.rectTransform.anchorMax = new Vector2(1, 0.18f);
            hint.rectTransform.offsetMin = hint.rectTransform.offsetMax = Vector2.zero;
            hint.color = new Color(0.85f, 0.88f, 0.95f, 0.85f);

            // Tip footer
            var tip = CreateText("Tip", root.transform, font, 18, TextAnchor.LowerCenter,
                "Tip: flying balls dive-bomb you — keep moving.");
            tip.rectTransform.anchorMin = new Vector2(0, 0);
            tip.rectTransform.anchorMax = new Vector2(1, 0.04f);
            tip.rectTransform.offsetMin = tip.rectTransform.offsetMax = Vector2.zero;
            tip.color = new Color(0.6f, 0.65f, 0.75f);
        }

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
            t.fontSize = 42;
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

        void PickPrimary(WeaponType t)
        {
            chosenPrimary = t;
            PlayerPrefs.SetInt(PREF_PRIMARY, (int)t);
            RefreshPickerHighlight();
        }

        void RefreshPickerHighlight()
        {
            // Brighten the chosen button, dim the others by adjusting its image alpha.
            SetButtonHighlight(smgBtn,     chosenPrimary == WeaponType.SMG);
            SetButtonHighlight(assaultBtn, chosenPrimary == WeaponType.Assault);
            SetButtonHighlight(sniperBtn,  chosenPrimary == WeaponType.Sniper);
        }

        static void SetButtonHighlight(Image img, bool selected)
        {
            if (img == null) return;
            var c = img.color;
            c.a = selected ? 1f : 0.55f;
            img.color = c;
            // Outline the selected
            var outline = img.GetComponent<Outline>();
            if (selected && outline == null)
            {
                outline = img.gameObject.AddComponent<Outline>();
                outline.effectColor = new Color(1f, 0.9f, 0.4f, 0.9f);
                outline.effectDistance = new Vector2(2.5f, -2.5f);
            }
            else if (!selected && outline != null)
            {
                Destroy(outline);
            }
        }

        void OnPlay()
        {
            // Tear down the menu + camera, then build the arena
            if (menuCamera != null) Destroy(menuCamera.gameObject);
            Destroy(gameObject);
            GameBootstrap.StartGame();
        }

        void OnQuit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
