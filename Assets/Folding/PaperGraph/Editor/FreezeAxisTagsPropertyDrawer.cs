using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Crease.Folding.PaperGraph.Editor
{
    [CustomPropertyDrawer(typeof(FreezeAxisTagsAttribute))]
    public class FreezeAxisTagsPropertyDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
            EditorGUI.BeginProperty(position, label, property);

            FoldInstruction instruction = property.serializedObject.targetObject as FoldInstruction;
            int stepIndex = FreezeAxisTagDropdown.TryGetStepIndexFromPropertyPath(property.propertyPath);
            List<string> availableTags = FreezeAxisTagDropdown.CollectAvailableAxisTags(instruction, stepIndex);
            FreezeAxisTagDropdown.Draw(position, property, availableTags, label.text);

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label) {
            return EditorGUIUtility.singleLineHeight;
        }
    }
}
