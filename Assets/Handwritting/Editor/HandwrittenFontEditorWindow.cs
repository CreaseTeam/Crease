using System.Collections.Generic;
using Crease.Handwritting;
using UnityEditor;
using UnityEngine;

namespace Crease.Handwritting.Editor
{
    public class HandwrittenFontEditorWindow : EditorWindow
    {
        const string GlyphSet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789.,!?'\"-:;() ";

        HandwrittenFontAsset _font;
        char _selectedCharacter = 'A';
        bool _isDrawing;
        HandwrittenStroke _activeStroke;
        Vector2 _scroll;

        readonly Stack<HandwrittenStroke> _undoStack = new Stack<HandwrittenStroke>();

        [MenuItem("Crease/Handwritting/Font Editor")]
        public static void Open()
        {
            GetWindow<HandwrittenFontEditorWindow>("Handwritten Font");
        }

        void OnGUI()
        {
            EditorGUILayout.LabelField("Handwritten Font Editor", EditorStyles.boldLabel);
            _font = (HandwrittenFontAsset)EditorGUILayout.ObjectField("Font Asset", _font, typeof(HandwrittenFontAsset), false);

            if (_font == null)
            {
                EditorGUILayout.HelpBox("Create or assign a HandwrittenFontAsset.", MessageType.Info);
                if (GUILayout.Button("Create Font Asset"))
                    CreateFontAsset();
                return;
            }

            DrawSettings();
            EditorGUILayout.Space(8f);
            DrawGlyphPicker();
            EditorGUILayout.Space(8f);
            DrawGlyphSettings();
            EditorGUILayout.Space(8f);
            DrawCanvas();
            EditorGUILayout.Space(8f);
            DrawActions();
        }

        void CreateFontAsset()
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "Create Handwritten Font",
                "HandwrittenFont",
                "asset",
                "Choose a location for the font asset.");

            if (string.IsNullOrEmpty(path))
                return;

            var asset = CreateInstance<HandwrittenFontAsset>();
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            _font = asset;
            Selection.activeObject = asset;
        }

        void DrawSettings()
        {
            EditorGUI.BeginChangeCheck();
            _font.LineWidth = EditorGUILayout.Slider("Line Width (px)", _font.LineWidth, 1f, 40f);
            _font.AtlasCellSize = EditorGUILayout.IntSlider("Atlas Cell Size", _font.AtlasCellSize, 64, 512);
            _font.GlobalStartPadding = EditorGUILayout.Slider("Start Padding (px)", _font.GlobalStartPadding, 0f, 32f);
            _font.GlobalAdvancePadding = EditorGUILayout.Slider("Advance Padding (px)", _font.GlobalAdvancePadding, 0f, 32f);
            _font.GlobalLetterSpacing = EditorGUILayout.Slider("Global Letter Spacing (px)", _font.GlobalLetterSpacing, -32f, 32f);
            _font.DefaultWriteDuration = EditorGUILayout.Slider("Default Write Duration (s)", _font.DefaultWriteDuration, 0.05f, 2f);
            if (EditorGUI.EndChangeCheck())
                EditorUtility.SetDirty(_font);
        }

        void DrawGlyphSettings()
        {
            HandwrittenGlyph glyph = _font.GetOrCreateGlyph(_selectedCharacter);
            EditorGUILayout.LabelField("Selected Glyph", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            float startNorm = EditorGUILayout.Slider("Start (0 = auto)", glyph.StartNormalized, 0f, 1f);
            float advanceNorm = EditorGUILayout.Slider("Advance (0 = auto)", glyph.AdvanceNormalized, 0f, 1f);
            glyph.WriteDuration = EditorGUILayout.Slider("Write Duration (0 = default)", glyph.WriteDuration, 0f, 2f);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Reset Start"))
                glyph.StartNormalized = 0f;
            if (GUILayout.Button("Reset Advance"))
                glyph.AdvanceNormalized = 0f;
            if (GUILayout.Button("Estimate Write Time"))
                glyph.WriteDuration = EstimateWriteDuration(glyph);
            EditorGUILayout.EndHorizontal();

            if (EditorGUI.EndChangeCheck())
            {
                glyph.StartNormalized = startNorm;
                glyph.AdvanceNormalized = advanceNorm;
                EditorUtility.SetDirty(_font);
                Repaint();
            }
        }

        static float EstimateWriteDuration(HandwrittenGlyph glyph)
        {
            float totalLength = 0f;
            foreach (HandwrittenStroke stroke in glyph.Strokes)
            {
                if (stroke.Points.Count < 2)
                    continue;

                for (int p = 1; p < stroke.Points.Count; p++)
                    totalLength += Vector2.Distance(stroke.Points[p - 1], stroke.Points[p]);
            }

            return Mathf.Clamp(totalLength * 1.2f, 0.05f, 2f);
        }

        void DrawGlyphPicker()
        {
            EditorGUILayout.LabelField("Glyphs", EditorStyles.boldLabel);
            _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.Height(120f));

            float buttonWidth = 28f;
            int columns = Mathf.Max(1, Mathf.FloorToInt((position.width - 24f) / buttonWidth));
            int column = 0;
            EditorGUILayout.BeginHorizontal();
            foreach (char character in GlyphSet)
            {
                if (column >= columns)
                {
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.BeginHorizontal();
                    column = 0;
                }

                bool hasGlyph = _font.TryGetGlyph(character, out HandwrittenGlyph glyph);
                bool hasStrokes = hasGlyph && glyph.Strokes.Count > 0;
                Color previousColor = GUI.backgroundColor;
                if (character == _selectedCharacter)
                    GUI.backgroundColor = Color.cyan;
                else if (hasStrokes)
                    GUI.backgroundColor = new Color(0.7f, 1f, 0.7f);
                else
                    GUI.backgroundColor = Color.white;

                string label = character == ' ' ? "SP" : character.ToString();
                if (GUILayout.Button(label, GUILayout.Width(buttonWidth), GUILayout.Height(24f)))
                    _selectedCharacter = character;

                GUI.backgroundColor = previousColor;
                column++;
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndScrollView();
            EditorGUILayout.LabelField($"Editing: '{DisplayCharacter(_selectedCharacter)}'");
        }

        void DrawCanvas()
        {
            const float canvasSize = 280f;
            Rect canvasRect = GUILayoutUtility.GetRect(canvasSize, canvasSize, GUILayout.ExpandWidth(false));
            EditorGUI.DrawRect(canvasRect, new Color(0.95f, 0.95f, 0.92f, 1f));

            if (Event.current.type == EventType.Repaint)
            {
                float baselineY = canvasRect.y + canvasRect.height * (1f - HandwrittenFontLayout.BaselineNormalized);
                Handles.color = new Color(0.2f, 0.2f, 0.2f, 0.35f);
                Handles.DrawLine(new Vector3(canvasRect.x, baselineY), new Vector3(canvasRect.xMax, baselineY));

                HandwrittenGlyph glyph = _font.GetOrCreateGlyph(_selectedCharacter);
                DrawStartGuide(canvasRect, glyph);
                DrawAdvanceGuide(canvasRect, glyph);
                DrawStrokes(canvasRect, glyph);
            }

            Event evt = Event.current;
            if (evt.type == EventType.MouseDown && canvasRect.Contains(evt.mousePosition))
            {
                HandwrittenGlyph glyph = _font.GetOrCreateGlyph(_selectedCharacter);
                _isDrawing = true;
                _activeStroke = new HandwrittenStroke();
                _activeStroke.Points.Add(Normalize(canvasRect, evt.mousePosition));
                glyph.Strokes.Add(_activeStroke);
                _undoStack.Clear();
                evt.Use();
                Repaint();
            }
            else if (evt.type == EventType.MouseDrag && _isDrawing && canvasRect.Contains(evt.mousePosition))
            {
                HandwrittenGlyph glyph = _font.GetOrCreateGlyph(_selectedCharacter);
                Vector2 point = Normalize(canvasRect, evt.mousePosition);
                if (_activeStroke.Points.Count == 0 || Vector2.Distance(_activeStroke.Points[^1], point) > 0.005f)
                    _activeStroke.Points.Add(point);
                evt.Use();
                Repaint();
            }
            else if (evt.type == EventType.MouseUp && _isDrawing)
            {
                HandwrittenGlyph glyph = _font.GetOrCreateGlyph(_selectedCharacter);
                _isDrawing = false;
                if (_activeStroke != null && _activeStroke.Points.Count <= 1)
                    glyph.Strokes.Remove(_activeStroke);
                _activeStroke = null;
                EditorUtility.SetDirty(_font);
                evt.Use();
                Repaint();
            }
        }

        void DrawStartGuide(Rect canvasRect, HandwrittenGlyph glyph)
        {
            float startNorm = HandwrittenFontLayout.GetPreviewStartNormalized(glyph, _font, _font.AtlasCellSize);
            float guideX = canvasRect.x + startNorm * canvasRect.width;

            Handles.color = new Color(0.15f, 0.45f, 0.9f, 0.85f);
            Handles.DrawLine(new Vector3(guideX, canvasRect.y), new Vector3(guideX, canvasRect.yMax));

            GUIStyle labelStyle = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = new Color(0.15f, 0.45f, 0.9f) } };
            GUI.Label(new Rect(guideX + 2f, canvasRect.y + 16f, 60f, 16f), "start", labelStyle);
        }

        void DrawAdvanceGuide(Rect canvasRect, HandwrittenGlyph glyph)
        {
            float advanceNorm = HandwrittenFontLayout.GetPreviewAdvanceNormalized(glyph, _font, _font.AtlasCellSize);
            float guideX = canvasRect.x + advanceNorm * canvasRect.width;

            Handles.color = new Color(0.9f, 0.35f, 0.1f, 0.85f);
            Handles.DrawLine(new Vector3(guideX, canvasRect.y), new Vector3(guideX, canvasRect.yMax));

            GUIStyle labelStyle = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = new Color(0.9f, 0.35f, 0.1f) } };
            GUI.Label(new Rect(guideX + 2f, canvasRect.y + 2f, 60f, 16f), "advance", labelStyle);
        }

        void DrawStrokes(Rect canvasRect, HandwrittenGlyph glyph)
        {
            Handles.color = Color.black;
            foreach (HandwrittenStroke stroke in glyph.Strokes)
            {
                if (stroke.Points.Count < 2)
                    continue;

                for (int i = 1; i < stroke.Points.Count; i++)
                {
                    Vector3 from = Denormalize(canvasRect, stroke.Points[i - 1]);
                    Vector3 to = Denormalize(canvasRect, stroke.Points[i]);
                    Handles.DrawAAPolyLine(3f, from, to);
                }
            }
        }

        void DrawActions()
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Undo Stroke"))
                UndoLastStroke();
            if (GUILayout.Button("Clear Glyph"))
                ClearSelectedGlyph();
            if (GUILayout.Button("Bake"))
                HandwrittenFontBaker.Bake(_font);
            EditorGUILayout.EndHorizontal();

            if (_font.FontAsset != null)
                EditorGUILayout.HelpBox("Font is baked and ready for HandwrittenTextPlayer.", MessageType.Info);
            else
                EditorGUILayout.HelpBox("Bake to generate the TMP font, reveal atlas, and material.", MessageType.Warning);
        }

        void UndoLastStroke()
        {
            HandwrittenGlyph glyph = _font.GetOrCreateGlyph(_selectedCharacter);
            if (glyph.Strokes.Count == 0)
                return;

            HandwrittenStroke removed = glyph.Strokes[^1];
            glyph.Strokes.RemoveAt(glyph.Strokes.Count - 1);
            _undoStack.Push(removed);
            EditorUtility.SetDirty(_font);
            Repaint();
        }

        void ClearSelectedGlyph()
        {
            HandwrittenGlyph glyph = _font.GetOrCreateGlyph(_selectedCharacter);
            glyph.Strokes.Clear();
            _undoStack.Clear();
            EditorUtility.SetDirty(_font);
            Repaint();
        }

        static Vector2 Normalize(Rect rect, Vector2 mousePosition)
        {
            return new Vector2(
                Mathf.Clamp01((mousePosition.x - rect.x) / rect.width),
                Mathf.Clamp01(1f - (mousePosition.y - rect.y) / rect.height));
        }

        static Vector3 Denormalize(Rect rect, Vector2 normalized)
        {
            return new Vector3(
                rect.x + normalized.x * rect.width,
                rect.y + (1f - normalized.y) * rect.height,
                0f);
        }

        static string DisplayCharacter(char character)
        {
            return character == ' ' ? "space" : character.ToString();
        }
    }
}
