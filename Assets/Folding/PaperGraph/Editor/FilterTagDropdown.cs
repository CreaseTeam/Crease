using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Crease.Folding.PaperGraph.Editor
{
    public static class FilterTagDropdown
    {
        public const string EverythingLabel = "Everything";
        private const string TagsRelativePath = "_tags";
        private static readonly string[] AppliedTagSuffixes = { "_moved", "_static", "_edge" };

        public static string GetSummary(IReadOnlyList<string> selectedTags) {
            if (selectedTags == null || selectedTags.Count == 0)
                return EverythingLabel;
            return string.Join(", ", selectedTags);
        }

        public static string GetSummary(SerializedProperty filterTagsProp) {
            return GetSummary(ReadTags(ResolveTagsArray(filterTagsProp)));
        }

        public static void DrawLayout(SerializedProperty filterTagsProp, IReadOnlyList<string> availableTags, string label = "Filter Tags") {
            Rect rect = EditorGUILayout.GetControlRect();
            Draw(rect, filterTagsProp, availableTags, label);
        }

        public static void Draw(Rect rect, SerializedProperty filterTagsProp, IReadOnlyList<string> availableTags, string label = "Filter Tags") {
            Rect labelRect = new Rect(rect.x, rect.y, EditorGUIUtility.labelWidth, rect.height);
            Rect fieldRect = new Rect(rect.x + EditorGUIUtility.labelWidth, rect.y, rect.width - EditorGUIUtility.labelWidth, rect.height);

            EditorGUI.LabelField(labelRect, label);
            if (EditorGUI.DropdownButton(fieldRect, new GUIContent(GetSummary(filterTagsProp)), FocusType.Keyboard))
                ShowMenu(filterTagsProp, availableTags);
        }

        public static void DrawLayout(List<string> selectedTags, IReadOnlyList<string> availableTags, System.Action<List<string>> onChanged, string label = "Filter Tags") {
            Rect rect = EditorGUILayout.GetControlRect();
            Draw(rect, selectedTags, availableTags, onChanged, label);
        }

        public static void Draw(Rect rect, List<string> selectedTags, IReadOnlyList<string> availableTags, System.Action<List<string>> onChanged, string label = "Filter Tags") {
            Rect labelRect = new Rect(rect.x, rect.y, EditorGUIUtility.labelWidth, rect.height);
            Rect fieldRect = new Rect(rect.x + EditorGUIUtility.labelWidth, rect.y, rect.width - EditorGUIUtility.labelWidth, rect.height);

            EditorGUI.LabelField(labelRect, label);
            if (EditorGUI.DropdownButton(fieldRect, new GUIContent(GetSummary(selectedTags)), FocusType.Keyboard))
                ShowMenu(selectedTags, availableTags, onChanged);
        }

        public static List<string> CollectAvailableTags(FoldInstruction instruction) {
            HashSet<string> tags = new HashSet<string>();

            if (instruction != null && instruction.Steps != null) {
                foreach (FoldStep step in instruction.Steps) {
                    AddAppliedTagVariants(tags, step.ApplyTag);
                    AddAppliedTagVariants(tags, step.AccordionCreaseTagA);
                    AddAppliedTagVariants(tags, step.AccordionCreaseTagB);
                    if (step.FilterTags?.Tags != null) {
                        foreach (string tag in step.FilterTags.Tags)
                            AddAppliedTagName(tags, tag);
                    }
                }
            }

            foreach (PaperGraph graph in Object.FindObjectsByType<PaperGraph>(FindObjectsSortMode.None))
                AddAppliedTagsFromGraph(tags, graph);

            return tags.OrderBy(t => t).ToList();
        }

        public static List<string> CollectAvailableTags(PaperGraph graph) {
            HashSet<string> tags = new HashSet<string>();
            AddAppliedTagsFromGraph(tags, graph);
            return tags.OrderBy(t => t).ToList();
        }

        private static void ShowMenu(SerializedProperty filterTagsProp, IReadOnlyList<string> availableTags) {
            SerializedObject serializedObject = filterTagsProp.serializedObject;
            string propertyPath = filterTagsProp.propertyPath;
            List<string> currentTags = ReadTags(ResolveTagsArray(filterTagsProp));

            GenericMenu menu = new GenericMenu();
            menu.AddItem(new GUIContent(EverythingLabel), currentTags.Count == 0, () => {
                ApplyTags(serializedObject, propertyPath, new List<string>());
            });

            if (availableTags != null && availableTags.Count > 0)
                menu.AddSeparator("");

            if (availableTags != null) {
                foreach (string tag in availableTags) {
                    string capturedTag = tag;
                    bool selected = currentTags.Contains(capturedTag);
                    menu.AddItem(new GUIContent(capturedTag), selected, () => {
                        List<string> next = ReadTags(FindTagsArray(serializedObject, propertyPath));
                        if (next.Contains(capturedTag))
                            next.Remove(capturedTag);
                        else
                            next.Add(capturedTag);
                        ApplyTags(serializedObject, propertyPath, next);
                    });
                }
            }

            menu.ShowAsContext();
        }

        private static void ShowMenu(List<string> selectedTags, IReadOnlyList<string> availableTags, System.Action<List<string>> onChanged) {
            GenericMenu menu = new GenericMenu();
            bool isEverything = selectedTags == null || selectedTags.Count == 0;

            menu.AddItem(new GUIContent(EverythingLabel), isEverything, () => {
                onChanged?.Invoke(new List<string>());
            });

            if (availableTags != null && availableTags.Count > 0)
                menu.AddSeparator("");

            if (availableTags != null) {
                foreach (string tag in availableTags) {
                    string capturedTag = tag;
                    bool selected = selectedTags != null && selectedTags.Contains(capturedTag);
                    menu.AddItem(new GUIContent(capturedTag), selected, () => {
                        List<string> next = selectedTags != null ? new List<string>(selectedTags) : new List<string>();
                        if (next.Contains(capturedTag))
                            next.Remove(capturedTag);
                        else
                            next.Add(capturedTag);
                        onChanged?.Invoke(next);
                    });
                }
            }

            menu.ShowAsContext();
        }

        private static SerializedProperty ResolveTagsArray(SerializedProperty filterTagsProp) {
            if (filterTagsProp == null)
                return null;

            if (filterTagsProp.isArray)
                return filterTagsProp;

            SerializedProperty tagsArray = filterTagsProp.FindPropertyRelative(TagsRelativePath);
            return tagsArray != null ? tagsArray : filterTagsProp;
        }

        private static SerializedProperty FindTagsArray(SerializedObject serializedObject, string propertyPath) {
            SerializedProperty property = serializedObject.FindProperty(propertyPath);
            return ResolveTagsArray(property);
        }

        private static List<string> ReadTags(SerializedProperty tagsArrayProp) {
            var tags = new List<string>();
            if (tagsArrayProp == null || !tagsArrayProp.isArray)
                return tags;

            for (int i = 0; i < tagsArrayProp.arraySize; i++) {
                SerializedProperty element = tagsArrayProp.GetArrayElementAtIndex(i);
                if (element.propertyType == SerializedPropertyType.String) {
                    string tag = element.stringValue;
                    if (!string.IsNullOrEmpty(tag))
                        tags.Add(tag);
                }
            }

            return tags;
        }

        private static void ApplyTags(SerializedObject serializedObject, string propertyPath, List<string> tags) {
            serializedObject.Update();

            SerializedProperty tagsArray = FindTagsArray(serializedObject, propertyPath);
            if (tagsArray == null || !tagsArray.isArray)
                return;

            tagsArray.ClearArray();
            tagsArray.arraySize = tags.Count;
            for (int i = 0; i < tags.Count; i++) {
                SerializedProperty element = tagsArray.GetArrayElementAtIndex(i);
                if (element.propertyType == SerializedPropertyType.String)
                    element.stringValue = tags[i];
            }

            serializedObject.ApplyModifiedProperties();
        }

        private static void AddAppliedTagVariants(HashSet<string> tags, string baseTag) {
            if (string.IsNullOrEmpty(baseTag) || baseTag == "none") return;
            foreach (string suffix in AppliedTagSuffixes)
                tags.Add(baseTag + suffix);
        }

        private static void AddAppliedTagName(HashSet<string> tags, string tag) {
            if (IsAppliedTagName(tag))
                tags.Add(tag);
        }

        private static void AddAppliedTagsFromGraph(HashSet<string> tags, PaperGraph graph) {
            if (graph?.Tags == null) return;
            foreach (string tag in graph.Tags.Keys)
                AddAppliedTagName(tags, tag);
        }

        private static bool IsAppliedTagName(string tag) {
            if (string.IsNullOrEmpty(tag)) return false;
            foreach (string suffix in AppliedTagSuffixes) {
                if (tag.EndsWith(suffix))
                    return true;
            }
            return false;
        }
    }
}
