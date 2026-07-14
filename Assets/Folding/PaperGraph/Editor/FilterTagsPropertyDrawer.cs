using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Crease.Folding.PaperGraph.Editor
{
    [CustomPropertyDrawer(typeof(FilterTagsAttribute))]
    public class FilterTagsPropertyDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
            EditorGUI.BeginProperty(position, label, property);

            FoldInstruction instruction = property.serializedObject.targetObject as FoldInstruction;
            List<string> availableTags = FilterTagDropdown.CollectAvailableTags(instruction);
            FilterTagDropdown.Draw(position, property, availableTags, label.text);

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label) {
            return EditorGUIUtility.singleLineHeight;
        }
    }
}
