using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

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
        private Text title, status, feedback, counts, hint;
        private const float HitY = 180, SpawnY = 600, LaneWidth = 100, BoardLeft = 140, BoardBottom = 130;
        private static readonly Color Background = new Color(0.045f, 0.055f, 0.09f);

        public SilentChartView(Transform owner)
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            canvasObject = new GameObject("GeneratedSilentChartCanvas", typeof(Canvas), typeof(CanvasScaler));
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
            title = TextAt("Title", stage, 30, 667, 1220, 36, 24, TextAnchor.MiddleLeft);
            hint = TextAt("Hint", stage, 30, 625, 1220, 32, 15, TextAnchor.MiddleLeft);

            for (int i = 0; i < 10; i++)
            {
                RectTransform lane = Rect("Lane" + (i + 1), stage, BoardLeft + i * LaneWidth, 170, LaneWidth - 2, 440);
                lanes[i] = ImageOf(lane, new Color(0.09f, 0.12f, 0.19f));
                TextAt("Number", stage, BoardLeft + i * LaneWidth, 582, LaneWidth - 2, 26, 17).text = (i + 1).ToString();
                TextAt("Keys", stage, BoardLeft + i * LaneWidth, 111, LaneWidth - 2, 54, 14).text =
                    KeyboardLaneInput.Labels[i].Replace(" / ", "\n");
            }
            float floorLeft = BoardLeft + (NoteLayout.FloorLeftCenterLane - 0.5f) * LaneWidth;
            float floorWidth = (NoteLayout.FloorRightCenterLane - NoteLayout.FloorLeftCenterLane) * LaneWidth;
            floorKeyIndicator = ImageOf(Rect("SpaceIndicator", stage, floorLeft, 91, floorWidth, 18),
                new Color(0.20f, 0.12f, 0.30f));
            TextAt("SpaceLabel", stage, floorLeft, 91, floorWidth, 18, 13).text = "SPACE / FLOOR";
            ImageOf(Rect("JudgeLine", stage, BoardLeft, HitY, 10 * LaneWidth, 3), new Color(0.4f, 0.9f, 1f));
            // Clip long tails at the lane top so they do not cover the title or instructions.
            noteLayer = Rect("Notes", stage, BoardLeft, BoardBottom, 10 * LaneWidth, 480);
            noteLayer.gameObject.AddComponent<RectMask2D>();
            feedback = TextAt("Feedback", stage, 40, 48, 550, 40, 25, TextAnchor.MiddleLeft);
            counts = TextAt("Counts", stage, 610, 48, 630, 40, 16, TextAnchor.MiddleRight);
            status = TextAt("Status", stage, 40, 8, 1200, 32, 16, TextAnchor.MiddleLeft);
        }

        public void SetChart(ChartData chart, RhythmSession session)
        {
            foreach (var widgets in noteWidgets) UnityEngine.Object.Destroy(widgets.Root.gameObject);
            noteWidgets.Clear();
            title.text = chart.title + " | " + chart.timing.initialBpm + " BPM | " + session.Notes.Count + " notes";
            hint.text = "Enter: start/resume / F1: basic / F2: LONG+DOUBLE / F5: restart / Events: " + chart.events.Length + " (ignored)";
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
            bool[] held, string state)
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
                    if (note.State == NoteState.Holding) headY = HitY;
                    pos.y = headY - BoardBottom;
                    widgets.Root.anchoredPosition = pos;
                    if (note.IsHold)
                    {
                        float tailY = HitY + (float)((note.EndTimeSeconds - chartSeconds) / travelSeconds) * (SpawnY - HitY);
                        float height = Math.Max(22, tailY - headY + 22);
                        widgets.Root.sizeDelta = new Vector2(widgets.Root.sizeDelta.x, height);
                        widgets.Tail.anchoredPosition = new Vector2(0, height - 6);
                        widgets.HeadImage.color = note.State != NoteState.Holding ?
                            (note.IsFloor ? new Color(0.88f, 0.55f, 1f) : new Color(0.3f, 0.95f, 0.5f)) :
                            note.IsHeld ? new Color(0.3f, 1f, 0.6f) : new Color(1f, 0.35f, 0.3f);
                    }
                    if (note.Data.type == "DOUBLE") widgets.Label.text = "DOUBLE " + note.DoubleCandidateCount + "/2";
                }
            }
            JudgementEvent last = session.LastJudgement;
            feedback.text = last == null ? "Press a key at the line" :
                last.Result.ToString().ToUpperInvariant() + (last.Result == Judge.Miss ? "" :
                    (last.HasTimingError ? "  " + (last.ErrorSeconds * 1000).ToString("+0.0;-0.0;0.0") + " ms" : "")) +
                (last == null ? "" : "  " + last.Kind);
            counts.text = "Events: P " + session.PerfectCount + "  GR " + session.GreatCount + "  GD " + session.GoodCount +
                "  M " + session.MissCount + "    Combo " + session.Combo + " / Max " + session.MaxCombo;
            status.text = state + " | Time " + chartSeconds.ToString("0.00") + " s | " +
                session.ResolvedCount + "/" + session.Notes.Count + " notes finished | Active holds " + session.ActiveHoldCount;
        }

        public void ShowError(string message)
        {
            title.text = "Silent chart could not be loaded";
            hint.text = "Check Console and the JSON file. Press F5 after fixing it.";
            status.text = message;
            foreach (var widgets in noteWidgets) widgets.Root.gameObject.SetActive(false);
        }

        public void Dispose() { if (canvasObject != null) UnityEngine.Object.Destroy(canvasObject); }

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
