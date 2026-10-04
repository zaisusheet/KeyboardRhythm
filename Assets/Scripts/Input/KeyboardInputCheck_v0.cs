using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Earlier input-check version. Keep the class name distinct from the current version.
[DisallowMultipleComponent]
public class KeyboardInputCheck_v0 : MonoBehaviour
{
    // These are test keys. Change this array to change the displayed keys.
    private readonly Key[] displayKeys = { Key.D, Key.F, Key.J, Key.K };
    private readonly Color idleColor = new Color(0.22f, 0.25f, 0.30f, 1f);
    private readonly Color pressedColor = new Color(0.08f, 0.65f, 0.35f, 1f);

    private GameObject generatedCanvas;
    private Image[] keyImages;
    private Font labelFont;
    private Text statusText;
    private string inputHint;

    private void Start()
    {
        CreateDisplay();
        Debug.Log("入力チェック開始：Game画面をクリックしてキーを押してください。", this);
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;

        // Each key is checked independently, including simultaneous presses.
        for (int i = 0; i < displayKeys.Length; i++)
        {
            bool isPressed = keyboard != null && keyboard[displayKeys[i]].isPressed;
            keyImages[i].color = isPressed ? pressedColor : idleColor;
        }

        statusText.text = keyboard == null
            ? "Keyboard not detected"
            : inputHint;

        if (keyboard == null)
        {
            return;
        }

        // Preserve the original check: log press/release for every key.
        foreach (var key in keyboard.allKeys)
        {
            if (key.wasPressedThisFrame)
            {
                Debug.Log($"押した: {key.keyCode}", this);
            }

            if (key.wasReleasedThisFrame)
            {
                Debug.Log($"離した: {key.keyCode}", this);
            }
        }
    }

    private void CreateDisplay()
    {
#if UNITY_2022_2_OR_NEWER
        labelFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
#else
        labelFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
#endif

        generatedCanvas = new GameObject(
            "RuntimeInputCanvas",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler));

        Canvas canvas = generatedCanvas.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = generatedCanvas.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;

        // A display-only UI needs no EventSystem or GraphicRaycaster.
        Transform root = generatedCanvas.transform;
        inputHint = "Click the Game view, then press "
            + string.Join(" / ", displayKeys);
        RectTransform background = CreateRect(
            "Background", root, Vector2.zero, Vector2.zero);
        background.anchorMin = Vector2.zero;
        background.anchorMax = Vector2.one;
        background.offsetMin = Vector2.zero;
        background.offsetMax = Vector2.zero;
        Image backgroundImage = background.gameObject.AddComponent<Image>();
        backgroundImage.color = new Color(0.06f, 0.08f, 0.12f, 1f);
        backgroundImage.raycastTarget = false;

        CreateText("Title", root, "KEY INPUT TEST",
            new Vector2(0f, 150f), new Vector2(1000f, 60f), 40);

        statusText = CreateText("Status", root,
            inputHint,
            new Vector2(0f, -120f), new Vector2(1100f, 50f), 24);

        keyImages = new Image[displayKeys.Length];
        for (int i = 0; i < displayKeys.Length; i++)
        {
            float x = (i - (displayKeys.Length - 1) * 0.5f) * 150f;
            RectTransform box = CreateRect(
                $"Key_{displayKeys[i]}", root,
                new Vector2(x, 0f), new Vector2(120f, 120f));

            Image image = box.gameObject.AddComponent<Image>();
            image.color = idleColor;
            image.raycastTarget = false;
            keyImages[i] = image;

            CreateText("Label", box, displayKeys[i].ToString(),
                Vector2.zero, new Vector2(120f, 120f), 48);
        }
    }

    private static RectTransform CreateRect(
        string objectName, Transform parent, Vector2 position, Vector2 size)
    {
        GameObject obj = new GameObject(objectName, typeof(RectTransform));
        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        return rect;
    }

    private Text CreateText(
        string objectName, Transform parent, string value,
        Vector2 position, Vector2 size, int fontSize)
    {
        RectTransform rect = CreateRect(objectName, parent, position, size);
        Text text = rect.gameObject.AddComponent<Text>();
        text.font = labelFont;
        text.text = value;
        text.fontSize = fontSize;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.raycastTarget = false;
        return text;
    }

    private void OnDestroy()
    {
        if (generatedCanvas != null)
        {
            Destroy(generatedCanvas);
        }
    }
}
