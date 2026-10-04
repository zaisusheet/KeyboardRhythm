using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Replace the contents of the existing script; retain its .meta and scene reference.
// Diagnostic UI only. This does not judge TOUCH, DOUBLE, LONG or FLOOR notes.
[DisallowMultipleComponent]
public class KeyboardInputCheck : MonoBehaviour
{
    private const int LaneCount = 10;
    private const int RegularKeyCount = 30;
    private const int FloorIndex = 30;
    private const int GameKeyCount = 31;

    // Three rows from SPEC.md, left-to-right. Index % 10 gives the lane.
    // U01 prototype: '+' uses the JIS ;/+ key (Semicolon), '?' uses /? (Slash).
    // Shift is optional and is not included among the 31 game keys.
    private static readonly Key[] GameKeys =
    {
        Key.Q, Key.W, Key.E, Key.R, Key.T, Key.Y, Key.U, Key.I, Key.O, Key.P,
        Key.A, Key.S, Key.D, Key.F, Key.G, Key.H, Key.J, Key.K, Key.L, Key.Semicolon,
        Key.Z, Key.X, Key.C, Key.V, Key.B, Key.N, Key.M, Key.Comma, Key.Period, Key.Slash,
        Key.Space
    };

    private static readonly string[] KeyLabels =
    {
        "Q", "W", "E", "R", "T", "Y", "U", "I", "O", "P",
        "A", "S", "D", "F", "G", "H", "J", "K", "L", "+",
        "Z", "X", "C", "V", "B", "N", "M", ",", ".", "?",
        "SPACE / FLOOR"
    };

    private readonly Color readyColor = new Color(0.22f, 0.25f, 0.30f, 1f);
    private readonly Color heldColor = new Color(0.08f, 0.65f, 0.35f, 1f);
    private readonly Color checkedColor = new Color(0.12f, 0.36f, 0.66f, 1f);
    private readonly Color laneIdleColor = new Color(0.10f, 0.13f, 0.19f, 1f);
    private readonly Color laneHeldColor = new Color(0.04f, 0.30f, 0.21f, 1f);

    // Physical-key snapshots are retained independently of lane summaries.
    private readonly bool[] heldKeys = new bool[GameKeyCount];
    private readonly bool[] pressedKeys = new bool[GameKeyCount];
    private readonly bool[] releasedKeys = new bool[GameKeyCount];
    private readonly bool[] checkedKeys = new bool[GameKeyCount];
    private readonly int[] laneHeldCounts = new int[LaneCount];
    private readonly int[] laneDownTotals = new int[LaneCount];
    private readonly int[] laneUpTotals = new int[LaneCount];
    private readonly Image[] keyImages = new Image[GameKeyCount];
    private readonly Text[] keyStates = new Text[GameKeyCount];
    private readonly Image[] laneImages = new Image[LaneCount];
    private readonly Text[] laneHeldTexts = new Text[LaneCount];
    private readonly Text[] laneEventTexts = new Text[LaneCount];

    private GameObject generatedCanvas;
    private Font labelFont;
    private Text progressText;
    private Text lastInputText;
    private Text shiftText;
    private int checkedCount;
    private int floorDownTotal;
    private int floorUpTotal;

    private void Start()
    {
        CreateDisplay();
        Debug.Log("10レーン入力チェック開始。Game画面をクリックしてください。", this);
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        CapturePhysicalKeys(keyboard);
        UpdateLaneState();
        UpdateDisplay(keyboard);
        LogPhysicalEvents(keyboard);
    }

    private void CapturePhysicalKeys(Keyboard keyboard)
    {
        for (int i = 0; i < GameKeyCount; i++)
        {
            heldKeys[i] = keyboard != null && keyboard[GameKeys[i]].isPressed;
            pressedKeys[i] = keyboard != null && keyboard[GameKeys[i]].wasPressedThisFrame;
            releasedKeys[i] = keyboard != null && keyboard[GameKeys[i]].wasReleasedThisFrame;

            if (pressedKeys[i] && !checkedKeys[i])
            {
                checkedKeys[i] = true;
                checkedCount++;
            }
        }
    }

    private void UpdateLaneState()
    {
        // Recompute holds from all three physical keys. Releasing one key must
        // not turn the lane off while another key in that lane remains held.
        for (int lane = 0; lane < LaneCount; lane++)
        {
            laneHeldCounts[lane] = 0;
        }

        for (int i = 0; i < RegularKeyCount; i++)
        {
            int lane = i % LaneCount;
            if (heldKeys[i])
            {
                laneHeldCounts[lane]++;
            }
            if (pressedKeys[i])
            {
                laneDownTotals[lane]++;
            }
            if (releasedKeys[i])
            {
                laneUpTotals[lane]++;
            }
        }

        // Space is independent: it contributes to neither lane holds nor
        // the ten lane event counters.
        if (pressedKeys[FloorIndex])
        {
            floorDownTotal++;
        }
        if (releasedKeys[FloorIndex])
        {
            floorUpTotal++;
        }
    }

    private void UpdateDisplay(Keyboard keyboard)
    {
        int heldCount = 0;
        int activeLanes = 0;
        for (int i = 0; i < GameKeyCount; i++)
        {
            if (heldKeys[i])
            {
                heldCount++;
            }
            keyImages[i].color = heldKeys[i]
                ? heldColor : (checkedKeys[i] ? checkedColor : readyColor);
            if (i != FloorIndex)
            {
                keyStates[i].text = heldKeys[i]
                    ? "HELD" : (checkedKeys[i] ? "CHECKED" : "READY");
            }
        }

        for (int lane = 0; lane < LaneCount; lane++)
        {
            bool active = laneHeldCounts[lane] > 0;
            if (active)
            {
                activeLanes++;
            }
            laneImages[lane].color = active ? laneHeldColor : laneIdleColor;
            laneHeldTexts[lane].text = $"Held: {laneHeldCounts[lane]}";
            // Totals persist through release, so events can be inspected easily.
            laneEventTexts[lane].text = $"Down {laneDownTotals[lane]}\nUp {laneUpTotals[lane]}";
        }

        string floorState = heldKeys[FloorIndex] ? "ON" : "OFF";
        keyStates[FloorIndex].text = $"Held {floorState} | Down {floorDownTotal} | Up {floorUpTotal}";
        progressText.text = keyboard == null
            ? $"Keyboard not detected | Checked: {checkedCount} / 31"
            : $"Game keys held: {heldCount} | Active lanes: {activeLanes} / 10 | Checked: {checkedCount} / 31";

        bool leftShift = keyboard != null && keyboard.leftShiftKey.isPressed;
        bool rightShift = keyboard != null && keyboard.rightShiftKey.isPressed;
        string shiftState = leftShift && rightShift ? "LEFT + RIGHT"
            : (leftShift ? "LEFT" : (rightShift ? "RIGHT" : "OFF"));
        shiftText.text = $"Shift: {shiftState} (diagnostic only) | Down / Up = physical key event totals";
    }

    private void LogPhysicalEvents(Keyboard keyboard)
    {
        if (keyboard == null)
        {
            return;
        }
        foreach (var key in keyboard.allKeys)
        {
            string description;
            if (key.wasPressedThisFrame)
            {
                description = DescribeKey(key.keyCode);
                lastInputText.text = $"Last: DOWN {description} | Unity: {key.keyCode}";
                Debug.Log($"押した: {description} / Unity={key.keyCode} / Layout={key.displayName}", this);
            }
            if (key.wasReleasedThisFrame)
            {
                description = DescribeKey(key.keyCode);
                lastInputText.text = $"Last: UP {description} | Unity: {key.keyCode}";
                Debug.Log($"離した: {description} / Unity={key.keyCode}", this);
            }
        }
    }

    private static string DescribeKey(Key key)
    {
        for (int i = 0; i < GameKeyCount; i++)
        {
            if (GameKeys[i] == key)
            {
                return i == FloorIndex ? "SPACE / FLOOR"
                    : $"{KeyLabels[i]} (Lane {i % LaneCount + 1})";
            }
        }
        return $"{key} (outside the 31 game keys)";
    }

    private void CreateDisplay()
    {
#if UNITY_2022_2_OR_NEWER
        labelFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
#else
        labelFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
#endif
        generatedCanvas = new GameObject("RuntimeInputCanvas",
            typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        Canvas canvas = generatedCanvas.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        CanvasScaler scaler = generatedCanvas.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;

        Transform root = generatedCanvas.transform;
        RectTransform background = CreateRect("Background", root, Vector2.zero, Vector2.zero);
        background.anchorMin = Vector2.zero;
        background.anchorMax = Vector2.one;
        background.offsetMin = Vector2.zero;
        background.offsetMax = Vector2.zero;
        AddImage(background, new Color(0.06f, 0.08f, 0.12f, 1f));

        CreateText("Title", root, "10-LANE INPUT TEST",
            new Vector2(0f, 310f), new Vector2(1200f, 44f), 34);
        CreateText("Hint", root, "Click Game view. Hold Q + A, then release Q. Space = independent FLOOR.",
            new Vector2(0f, 268f), new Vector2(1240f, 32f), 21);

        const float pitch = 104f;
        for (int lane = 0; lane < LaneCount; lane++)
        {
            RectTransform panel = CreateRect($"Lane_{lane + 1}", root,
                new Vector2((lane - 4.5f) * pitch, 75f), new Vector2(98f, 330f));
            laneImages[lane] = AddImage(panel, laneIdleColor);
            CreateText("LaneNumber", panel, $"Lane {lane + 1}",
                new Vector2(0f, 140f), new Vector2(96f, 28f), 21);

            for (int row = 0; row < 3; row++)
            {
                int index = row * LaneCount + lane;
                RectTransform card = CreateRect($"Key_{GameKeys[index]}", panel,
                    new Vector2(0f, 82f - row * 68f), new Vector2(84f, 56f));
                keyImages[index] = AddImage(card, readyColor);
                CreateText("Label", card, KeyLabels[index],
                    new Vector2(0f, 8f), new Vector2(84f, 32f), 28);
                keyStates[index] = CreateText("State", card, "READY",
                    new Vector2(0f, -17f), new Vector2(84f, 18f), 13);
            }

            laneHeldTexts[lane] = CreateText("HeldCount", panel, "Held: 0",
                new Vector2(0f, -100f), new Vector2(96f, 24f), 18);
            laneEventTexts[lane] = CreateText("EventTotals", panel, "Down 0\nUp 0",
                new Vector2(0f, -136f), new Vector2(96f, 42f), 15);
        }

        RectTransform floor = CreateRect("Key_Space_FLOOR", root,
            new Vector2(0f, -145f), new Vector2(600f, 62f));
        keyImages[FloorIndex] = AddImage(floor, readyColor);
        CreateText("Label", floor, KeyLabels[FloorIndex],
            new Vector2(0f, 10f), new Vector2(580f, 32f), 25);
        keyStates[FloorIndex] = CreateText("State", floor, "Held OFF | Down 0 | Up 0",
            new Vector2(0f, -18f), new Vector2(580f, 22f), 16);

        progressText = CreateText("Progress", root, "",
            new Vector2(0f, -207f), new Vector2(1240f, 34f), 22);
        lastInputText = CreateText("LastInput", root, "Last: -",
            new Vector2(0f, -249f), new Vector2(1240f, 32f), 20);
        shiftText = CreateText("Shift", root, "",
            new Vector2(0f, -289f), new Vector2(1240f, 32f), 18);
    }

    private static Image AddImage(RectTransform rect, Color color)
    {
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
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

    private Text CreateText(string objectName, Transform parent, string value,
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
