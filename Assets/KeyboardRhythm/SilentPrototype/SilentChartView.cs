using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace KeyboardRhythm.SilentPrototype
{
    // Display only: all positions come from the same chart clock used by judgement.
    public sealed class SilentChartView : IDisposable
    {
        private readonly GameObject canvasObject;
        private readonly RectTransform stage;
        private readonly RectTransform noteLayer;
        private readonly Font font;
        private readonly Image[] lanes = new Image[10];
        private readonly Image floorKeyIndicator;
        private sealed class NoteWidgets
        {
            public RectTransform Root, Head, Tail;
            public Image BodyImage, HeadImage;
            public Text Label;
        }
        private readonly List<NoteWidgets> noteWidgets = new List<NoteWidgets>();
        private GameObject ownedEventSystem;
        private Dropdown chartDropdown;
        private Text timingFeedback;
        private Slider speedSlider;
        private Toggle hitSoundToggle;
        private Text speedLabel;
        // uGUI creates this child while the list is open (including its closing fade).
        public bool IsChartMenuOpen => chartDropdown != null && chartDropdown.transform.Find("Dropdown List") != null;
        private Text title, status, feedback, counts, hint, combo, audioStatus, timingStatus;
        private const float HitY = 128, SpawnY = 674, LaneWidth = 50, BoardLeft = 390, BoardBottom = 106;
        private static readonly Color Background = new Color(0.045f, 0.055f, 0.09f);

        public SilentChartView(Transform owner)
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            canvasObject = new GameObject("GeneratedSilentChartCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(owner, false);
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            stage = Rect("Stage", canvasObject.transform, 0, 0, 1280, 720);
            stage.anchorMin = stage.anchorMax = new Vector2(0.5f, 0.5f);
            stage.pivot = new Vector2(0.5f, 0.5f);
            ImageOf(stage, Background);
            ImageOf(Rect("SongPanel", stage, 20, 20, 300, 680), new Color(0.07f, 0.09f, 0.14f));
            ImageOf(Rect("ResultPanel", stage, 960, 20, 300, 680), new Color(0.07f, 0.09f, 0.14f));
            TextAt("SongHeading", stage, 40, 648, 260, 32, 18, TextAnchor.MiddleLeft).text = "KEYBOARD RHYTHM";
            title = TextAt("Title", stage, 40, 442, 260, 136, 18, TextAnchor.UpperLeft);
            title.resizeTextForBestFit = true;
            title.resizeTextMinSize = 12;
            title.resizeTextMaxSize = 18;
            hint = TextAt("Hint", stage, 40, 188, 260, 246, 15, TextAnchor.UpperLeft);
            audioStatus = TextAt("AudioStatus", stage, 40, 110, 260, 65, 16, TextAnchor.UpperLeft);
            timingStatus = TextAt("TimingStatus", stage, 40, 30, 260, 72, 13, TextAnchor.UpperLeft);

            for (int i = 0; i < 10; i++)
            {
                RectTransform lane = Rect("Lane" + (i + 1), stage, BoardLeft + i * LaneWidth, BoardBottom, LaneWidth - 2, 592);
                lanes[i] = ImageOf(lane, new Color(0.09f, 0.12f, 0.19f));
                TextAt("Number", stage, BoardLeft + i * LaneWidth, 674, LaneWidth - 2, 24, 15).text = (i + 1).ToString();
                TextAt("Keys", stage, BoardLeft + i * LaneWidth, 42, LaneWidth - 2, 60, 13).text =
                    KeyboardLaneInput.Labels[i].Replace(" / ", "\n");
            }
            float floorLeft = BoardLeft + (NoteLayout.FloorLeftCenterLane - 0.5f) * LaneWidth;
            float floorWidth = (NoteLayout.FloorRightCenterLane - NoteLayout.FloorLeftCenterLane) * LaneWidth;
            floorKeyIndicator = ImageOf(Rect("SpaceIndicator", stage, floorLeft, 20, floorWidth, 18),
                new Color(0.20f, 0.12f, 0.30f));
            TextAt("SpaceLabel", stage, floorLeft, 20, floorWidth, 18, 13).text = "SPACE / FLOOR";
            ImageOf(Rect("JudgeLine", stage, BoardLeft, HitY, 10 * LaneWidth, 3), new Color(0.4f, 0.9f, 1f));
            // Keep all moving notes inside the tall central lane area.
            noteLayer = Rect("Notes", stage, BoardLeft, BoardBottom, 10 * LaneWidth, 568);
            noteLayer.gameObject.AddComponent<RectMask2D>();
            // Draw translucent text over the notes at the centre of their travel area.
            combo = TextAt("Combo", stage, BoardLeft, (HitY + SpawnY) * 0.5f - 72, 10 * LaneWidth, 144, 36, TextAnchor.MiddleCenter);
            combo.color = new Color(0.88f, 0.94f, 1f, 0.5f);
            combo.gameObject.SetActive(false);
            var feedbackRoot = Rect("JudgementFeedback", canvasObject.transform, 0, 0, 400, 88);
            feedbackRoot.anchorMin = feedbackRoot.anchorMax = new Vector2(0.5f, 0.4f);
            feedbackRoot.pivot = new Vector2(0.5f, 0.5f);
            ImageOf(feedbackRoot, new Color(0.03f, 0.04f, 0.08f, 0.78f));
            feedback = TextAt("Feedback", feedbackRoot, 0, 38, 400, 46, 32, TextAnchor.MiddleCenter);
            timingFeedback = TextAt("TimingFeedback", feedbackRoot, 0, 4, 400, 34, 24, TextAnchor.MiddleCenter);
            feedbackRoot.gameObject.SetActive(false);
            counts = TextAt("Counts", stage, 980, 190, 260, 156, 19, TextAnchor.UpperLeft);
            status = TextAt("Status", stage, 980, 40, 260, 136, 14, TextAnchor.UpperLeft);
        }

        public void SetChoices(List<string> names, int current, Action<int> onChange)
        {
            if (EventSystem.current == null)
            {
                ownedEventSystem = new GameObject("ChartSelectionEventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                ownedEventSystem.transform.SetParent(canvasObject.transform.parent, false);
                ownedEventSystem.GetComponent<EventSystem>().sendNavigationEvents = false;
                ownedEventSystem.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }
            TextAt("ChartLabel", stage, 40, 618, 260, 24, 14, TextAnchor.MiddleLeft).text = "SELECT CHART";
            GameObject dropdownObject = DefaultControls.CreateDropdown(new DefaultControls.Resources());
            dropdownObject.name = "ChartSelection";
            dropdownObject.transform.SetParent(stage, false);
            var rect = (RectTransform)dropdownObject.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;
            rect.anchoredPosition = new Vector2(40, 580);
            rect.sizeDelta = new Vector2(260, 36);
            chartDropdown = dropdownObject.GetComponent<Dropdown>();
            chartDropdown.navigation = new Navigation { mode = Navigation.Mode.None };
            foreach (Text label in dropdownObject.GetComponentsInChildren<Text>(true))
            {
                label.font = font;
                label.fontSize = 15;
                label.color = new Color(0.08f, 0.1f, 0.15f);
            }
            chartDropdown.ClearOptions();
            chartDropdown.AddOptions(names);
            chartDropdown.SetValueWithoutNotify(current);
            chartDropdown.onValueChanged.AddListener(index =>
            {
                onChange(index);
                if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            });
            Text arrow = TextAt("ArrowLabel", rect, 232, 0, 24, 36, 16);
            arrow.text = "v";
            arrow.color = new Color(0.08f, 0.1f, 0.15f);
        }

        public void SetDemoControls(ScrollSpeedSettings speed, bool soundEnabled,
            Action<int> onSpeedChange, Action<bool> onSoundChange)
        {
            speedLabel = TextAt("ScrollSpeedLabel", stage, 980, 436, 260, 26, 18, TextAnchor.MiddleLeft);
            GameObject sliderObject = DefaultControls.CreateSlider(new DefaultControls.Resources());
            sliderObject.name = "ScrollSpeed";
            PlaceControl(sliderObject, 980, 398, 250, 28);
            speedSlider = sliderObject.GetComponent<Slider>();
            speedSlider.navigation = new Navigation { mode = Navigation.Mode.None };
            speedSlider.minValue = ScrollSpeedSettings.MinimumTenths;
            speedSlider.maxValue = ScrollSpeedSettings.MaximumTenths;
            speedSlider.wholeNumbers = true;
            sliderObject.transform.Find("Background").GetComponent<Image>().color = new Color(0.15f, 0.2f, 0.3f);
            speedSlider.fillRect.GetComponent<Image>().color = new Color(0.2f, 0.7f, 0.9f);
            speedSlider.handleRect.GetComponent<Image>().color = Color.white;
            speedSlider.onValueChanged.AddListener(value =>
            {
                onSpeedChange(Mathf.RoundToInt(value));
                if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            });

            GameObject toggleObject = DefaultControls.CreateToggle(new DefaultControls.Resources());
            toggleObject.name = "HitSound";
            PlaceControl(toggleObject, 980, 352, 260, 32);
            hitSoundToggle = toggleObject.GetComponent<Toggle>();
            hitSoundToggle.navigation = new Navigation { mode = Navigation.Mode.None };
            hitSoundToggle.targetGraphic.color = new Color(0.15f, 0.2f, 0.3f);
            hitSoundToggle.graphic.color = new Color(0.2f, 0.9f, 0.65f);
            Text toggleLabel = toggleObject.GetComponentInChildren<Text>();
            toggleLabel.font = font;
            toggleLabel.fontSize = 17;
            toggleLabel.color = Color.white;
            toggleLabel.text = "Hit sound (F4)";
            hitSoundToggle.onValueChanged.AddListener(enabled =>
            {
                onSoundChange(enabled);
                if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            });
            UpdateDemoControls(speed, soundEnabled);
        }

        public void UpdateDemoControls(ScrollSpeedSettings speed, bool soundEnabled)
        {
            speedLabel.text = "SCROLL SPEED  " + speed.Multiplier.ToString("0.0") + "x";
            speedSlider.SetValueWithoutNotify(speed.Tenths);
            hitSoundToggle.SetIsOnWithoutNotify(soundEnabled);
        }

        private void PlaceControl(GameObject obj, float x, float y, float width, float height)
        {
            obj.transform.SetParent(stage, false);
            var rect = (RectTransform)obj.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;
            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(width, height);
        }

        public void SetChart(ChartData chart, RhythmSession session)
        {
            combo.gameObject.SetActive(false);
            foreach (var widgets in noteWidgets) UnityEngine.Object.Destroy(widgets.Root.gameObject);
            noteWidgets.Clear();
            title.text = "CHART  " + chart.title + "\nSONG  " + (string.IsNullOrWhiteSpace(chart.audio.songTitle) ? (string.IsNullOrEmpty(chart.audio.path) ? "Metronome / Silent" : System.IO.Path.GetFileNameWithoutExtension(chart.audio.path)) : chart.audio.songTitle) + "\n\n" + chart.timing.initialBpm + " BPM\n" + session.Notes.Count + " notes\n" + chart.events.Length + " events (ignored)";
            hint.text = "Enter   Start / Resume\nMouse   Select chart above\nF3   Metronome on / off\nF4   Hit sound on / off\nF5   Restart / apply offset\nF6/F7   Offset -/+10 ms\nShift + F6/F7   1 ms steps\nF8   Reset offset\nF9/F10   Speed -/+0.1x\nF11   Reset speed to 1.0x\nFocus loss pauses play";
            foreach (RuntimeNote note in session.Notes)
            {
                bool floor = note.IsFloor;
                NoteVisualSpan span = NoteLayout.GetSpan(note.Data);
                float padding = floor ? 0 : 4;
                float x = (float)span.Left * LaneWidth + padding;
                float width = (float)span.Width * LaneWidth - 2 * padding;
                var widgets = new NoteWidgets();
                widgets.Root = Rect("Note_" + note.Data.id, noteLayer, x, SpawnY - BoardBottom, width, 18);
                Color color = floor ? new Color(0.88f, 0.55f, 1f) : note.IsHold ? new Color(0.3f, 0.95f, 0.5f) :
                    note.Data.type == "DOUBLE" ? new Color(1f, 0.8f, 0.2f) : new Color(0.2f, 0.9f, 0.95f);
                if (note.IsHold)
                {
                    widgets.BodyImage = ImageOf(widgets.Root, new Color(color.r, color.g, color.b, 0.28f));
                    widgets.Head = Rect("Head", widgets.Root, 0, 0, width, 22);
                    widgets.HeadImage = ImageOf(widgets.Head, color);
                    widgets.Tail = Rect("Tail", widgets.Root, 0, 0, width, 6);
                    ImageOf(widgets.Tail, color);
                }
                else
                {
                    widgets.Head = widgets.Root;
                    widgets.HeadImage = ImageOf(widgets.Root, color);
                    if (note.Data.type == "DOUBLE") ImageOf(Rect("SecondBar", widgets.Root, 0, 24, width, 6), color);
                }
                if (floor || note.IsHold || note.Data.type == "DOUBLE")
                {
                    widgets.Label = TextAt("Label", widgets.Head, 0, 0, width, 22, 13);
                    widgets.Label.color = new Color(0.12f, 0.08f, 0.18f);
                    widgets.Label.text = floor ? (note.IsHold ? "SPACE HOLD" : "SPACE") : note.IsHold ? "LONG" : "DOUBLE 0/2";
                }
                if (floor || note.IsHold) widgets.Root.SetAsFirstSibling();
                noteWidgets.Add(widgets);
            }
            // Keep status labels visible over moving notes.
            feedback.transform.SetAsLastSibling();
            counts.transform.SetAsLastSibling();
            status.transform.SetAsLastSibling();
        }

        public void Render(RhythmSession session, double chartSeconds, double travelSeconds,
            bool[] held, string state, bool metronomeEnabled, double audioSeconds, NoteTimingSettings noteTiming, bool hasSong, double zeroAtAudio)
        {
            for (int i = 0; i < lanes.Length; i++)
                lanes[i].color = held[i] ? new Color(0.16f, 0.35f, 0.40f) : new Color(0.09f, 0.12f, 0.19f);
            floorKeyIndicator.color = held[10] ? new Color(0.46f, 0.24f, 0.58f) : new Color(0.20f, 0.12f, 0.30f);
            for (int i = 0; i < session.Notes.Count; i++)
            {
                RuntimeNote note = session.Notes[i];
                double remaining = note.TimeSeconds - chartSeconds;
                bool visible = note.State != NoteState.Completed && remaining <= travelSeconds &&
                    (note.IsHold || remaining >= -0.3);
                var widgets = noteWidgets[i];
                widgets.Root.gameObject.SetActive(visible);
                if (visible)
                {
                    Vector2 pos = widgets.Root.anchoredPosition;
                    float headY = HitY + (float)(remaining / travelSeconds) * (SpawnY - HitY);
                    if (note.State == NoteState.Holding || note.State == NoteState.MissedStart) headY = HitY;
                    pos.y = headY - BoardBottom;
                    widgets.Root.anchoredPosition = pos;
                    if (note.IsHold)
                    {
                        float tailY = HitY + (float)((note.EndTimeSeconds - chartSeconds) / travelSeconds) * (SpawnY - HitY);
                        float height = Math.Max(22, tailY - headY + 22);
                        widgets.Root.sizeDelta = new Vector2(widgets.Root.sizeDelta.x, height);
                        widgets.Tail.anchoredPosition = new Vector2(0, height - 6);
                        widgets.HeadImage.color = note.State == NoteState.MissedStart ? new Color(1f, 0.35f, 0.3f) :
                            note.State != NoteState.Holding ?
                            (note.IsFloor ? new Color(0.88f, 0.55f, 1f) : new Color(0.3f, 0.95f, 0.5f)) :
                            note.IsHeld ? new Color(0.3f, 1f, 0.6f) : new Color(1f, 0.35f, 0.3f);
                        widgets.Label.text = note.State == NoteState.MissedStart ?
                            (note.IsFloor ? "SPACE: PRESS TO JOIN" : "LONG: PRESS TO JOIN") :
                            (note.IsFloor ? "SPACE HOLD" : "LONG");
                    }
                    if (note.Data.type == "DOUBLE") widgets.Label.text = "DOUBLE " + note.DoubleCandidateCount + "/2";
                }
            }
            JudgementEvent last = session.LastJudgement;
            feedback.transform.parent.gameObject.SetActive(last != null);
            feedback.text = last == null ? "" : last.Result.ToString().ToUpperInvariant();
            timingFeedback.text = MusicTiming.TimingLabel(last);
            timingFeedback.color = last != null && last.HasTimingError && (last.Result == Judge.Great || last.Result == Judge.Good) && last.ErrorSeconds != 0 ?
                (last.ErrorSeconds < 0 ? new Color(0.25f, 0.65f, 1f) : new Color(1f, 0.3f, 0.3f)) : Color.white;
            combo.gameObject.SetActive(session.Combo >= 3);
            combo.text = "<size=18>COMBO</size>\n" + session.Combo + "\n<size=18>MAX " + session.MaxCombo + "</size>";
            counts.text = "PERFECT   " + session.PerfectCount + "\nGREAT       " + session.GreatCount + "\nGOOD        " + session.GoodCount + "\nMISS          " + session.MissCount;
            status.text = state + "\n\n" + (hasSong ? "Audio " + Math.Max(0, MusicTiming.AudioSeconds(audioSeconds, zeroAtAudio)).ToString("0.00") + " s\n" : "") + "Clock " + audioSeconds.ToString("0.00") + " s\nNotes " + chartSeconds.ToString("0.00") + " s\n" +
                session.ResolvedCount + "/" + session.Notes.Count + " notes finished\nActive holds " + session.ActiveHoldCount;
            audioStatus.text = (hasSong ? "SONG AUDIO\n" : "NO SONG AUDIO\n") + (metronomeEnabled ? "Metronome ON (F3)" : "Metronome OFF (F3)");
            timingStatus.text = "NOTE OFFSET " + noteTiming.ActiveMilliseconds.ToString("+0;-0;0") + " ms\n" +
                (noteTiming.HasPendingChange ? "Saved: " + noteTiming.SavedMilliseconds.ToString("+0;-0;0") + " ms (pending)\nF5, then Enter to apply" :
                "+ = later / - = earlier\nSaved on this device");
        }

        public void ShowError(string message)
        {
            combo.gameObject.SetActive(false);
            feedback.text = timingFeedback.text = "";
            feedback.transform.parent.gameObject.SetActive(false);
            title.text = "Chart could not be loaded";
            hint.text = "Check Console and the JSON file.\nPress F5 after fixing it.";
            status.text = message;
            foreach (var widgets in noteWidgets) widgets.Root.gameObject.SetActive(false);
        }

        public void Dispose() { if (ownedEventSystem != null) UnityEngine.Object.Destroy(ownedEventSystem); if (canvasObject != null) UnityEngine.Object.Destroy(canvasObject); }

        private static RectTransform Rect(string name, Transform parent, float x, float y, float w, float h)
        {
            var obj = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)obj.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = Vector2.zero;
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(w, h);
            return rt;
        }

        private static Image ImageOf(RectTransform rect, Color color)
        {
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private Text TextAt(string name, Transform parent, float x, float y, float w, float h, int size,
            TextAnchor alignment = TextAnchor.MiddleCenter)
        {
            Text text = Rect(name, parent, x, y, w, h).gameObject.AddComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.alignment = alignment;
            text.color = new Color(0.88f, 0.94f, 1f);
            text.raycastTarget = false;
            return text;
        }
    }
}
