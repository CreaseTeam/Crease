using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace Crease.Folding.Paper.Editor
{
    public static class FreezeAxisTagDropdown
    {
        public const string NoneLabel = "None";
        private const string TagsRelativePath = "_tags";
        private static readonly Regex StepIndexPattern = new Regex(@"Steps\.Array\.data\[(\d+)\]", RegexOptions.Compiled);

        public static string GetSummary(IReadOnlyList<string> selectedTags) {
            if (selectedTags == null || selectedTags.Count == 0)
                return NoneLabel;
            return string.Join(", ", selectedTags);
        }

        public static string GetSummary(SerializedProperty freezeAxisTagsProp) {
            return GetSummary(ReadTags(ResolveTagsArray(freezeAxisTagsProp)));
        }

        public static void Draw(Rect rect, SerializedProperty freezeAxisTagsProp, IReadOnlyList<string> availableTags, string label = "Freeze Axes") {
            Rect labelRect = new Rect(rect.x, rect.y, EditorGUIUtility.labelWidth, rect.height);
            Rect fieldRect = new Rect(rect.x + EditorGUIUtility.labelWidth, rect.y, rect.width - EditorGUIUtility.labelWidth, rect.height);

            EditorGUI.LabelField(labelRect, label);
            if (EditorGUI.DropdownButton(fieldRect, new GUIContent(GetSummary(freezeAxisTagsProp)), FocusType.Keyboard))
                ShowMenu(freezeAxisTagsProp, availableTags);
        }

        public static List<string> CollectAvailableAxisTags(FoldInstruction instruction, int upToStepIndexExclusive) {
            HashSet<string> tags = new HashSet<string>();

            if (instruction?.Steps == null)
                return tags.OrderBy(t => t).ToList();

            int limit = upToStepIndexExclusive >= 0
                ? Mathf.Min(upToStepIndexExclusive, instruction.Steps.Count)
                : instruction.Steps.Count;

            for (int i = 0; i < limit; i++) {
                FoldStep step = instruction.Steps[i];
                AddAxisTag(tags, step.ApplyTag);
                AddAxisTag(tags, step.AccordionCreaseTagA);
                AddAxisTag(tags, step.AccordionCreaseTagB);
            }

            return tags.OrderBy(t => t).ToList();
        }

        public static int TryGetStepIndexFromPropertyPath(string propertyPath) {
            Match match = StepIndexPattern.Match(propertyPath ?? string.Empty);
            if (!match.Success)
                return -1;

            return int.TryParse(match.Groups[1].Value, out int index) ? index : -1;
        }

        private static void ShowMenu(SerializedProperty freezeAxisTagsProp, IReadOnlyList<string> availableTags) {
            SerializedObject serializedObject = freezeAxisTagsProp.serializedObject;
            string propertyPath = freezeAxisTagsProp.propertyPath;
            List<string> currentTags = ReadTags(ResolveTagsArray(freezeAxisTagsProp));

            GenericMenu menu = new GenericMenu();
            menu.AddItem(new GUIContent(NoneLabel), currentTags.Count == 0, () => {
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

        private static SerializedProperty ResolveTagsArray(SerializedProperty freezeAxisTagsProp) {
            if (freezeAxisTagsProp == null)
                return null;

            if (freezeAxisTagsProp.isArray)
                return freezeAxisTagsProp;

            SerializedProperty tagsArray = freezeAxisTagsProp.FindPropertyRelative(TagsRelativePath);
            return tagsArray != null ? tagsArray : freezeAxisTagsProp;
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

        private static void AddAxisTag(HashSet<string> tags, string tag) {
            if (string.IsNullOrEmpty(tag) || tag == "none")
                return;
            tags.Add(tag);
        }
    }
}
