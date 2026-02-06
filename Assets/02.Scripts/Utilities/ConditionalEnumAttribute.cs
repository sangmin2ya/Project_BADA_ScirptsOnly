using UnityEngine;
using System;

/// <summary>
/// 특정 enum 값일 때만 필드를 표시하는 속성
/// [박상민 - 2025.10.03]
/// </summary>
[AttributeUsage(AttributeTargets.Field)]
public class ConditionalEnumAttribute : PropertyAttribute
{
    public string ConditionalSourceField { get; private set; }
    public int EnumValue { get; private set; }

    public ConditionalEnumAttribute(string conditionalSourceField, int enumValue)
    {
        ConditionalSourceField = conditionalSourceField;
        EnumValue = enumValue;
    }
}
