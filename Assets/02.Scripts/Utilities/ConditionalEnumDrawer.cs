#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

/// <summary>
/// ConditionalEnum 속성에 대한 커스텀 드로어
/// [박상민 - 2025.10.03]
/// </summary>
[CustomPropertyDrawer(typeof(ConditionalEnumAttribute))]
public class ConditionalEnumDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        ConditionalEnumAttribute condAttr = (ConditionalEnumAttribute)attribute;
        
        // 상대 경로로 조건 필드 찾기
        SerializedProperty sourceProperty = GetRelativeProperty(property, condAttr.ConditionalSourceField);
        
        if (sourceProperty != null && sourceProperty.enumValueIndex == condAttr.EnumValue)
        {
            EditorGUI.PropertyField(position, property, label, true);
        }
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        ConditionalEnumAttribute condAttr = (ConditionalEnumAttribute)attribute;
        SerializedProperty sourceProperty = GetRelativeProperty(property, condAttr.ConditionalSourceField);
        
        if (sourceProperty != null && sourceProperty.enumValueIndex == condAttr.EnumValue)
        {
            return EditorGUI.GetPropertyHeight(property, label, true);
        }
        
        return -EditorGUIUtility.standardVerticalSpacing;
    }

    /// <summary>
    /// 현재 프로퍼티의 부모를 기준으로 상대 경로의 프로퍼티를 찾음
    /// </summary>
    private SerializedProperty GetRelativeProperty(SerializedProperty property, string relativePath)
    {
        string path = property.propertyPath;
        int lastDotIndex = path.LastIndexOf('.');
        
        if (lastDotIndex >= 0)
        {
            string parentPath = path.Substring(0, lastDotIndex);
            return property.serializedObject.FindProperty(parentPath + "." + relativePath);
        }
        
        return property.serializedObject.FindProperty(relativePath);
    }
}
#endif