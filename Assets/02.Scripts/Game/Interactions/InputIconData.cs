using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 입력 액션에 대한 키보드/게임패드 아이콘을 매핑하는 ScriptableObject
/// [박상민 - 2025.11.20]
/// </summary>
[CreateAssetMenu(fileName = "InputIconData", menuName = "Input/Input Icon Data")]
public class InputIconData : ScriptableObject
{
    [SerializeField] private List<InputIconMapping> iconMappings = new List<InputIconMapping>();
    
    /// <summary>
    /// 특정 액션과 디바이스 타입에 해당하는 아이콘 스프라이트를 가져옵니다
    /// </summary>
    /// <param name="actionName">액션 이름</param>
    /// <param name="isGamepad">게임패드 여부</param>
    /// <returns>해당하는 아이콘 스프라이트</returns>
    public Sprite GetIconForAction(string actionName, bool isGamepad)
    {
        InputIconMapping mapping = iconMappings.Find(m => m.actionName == actionName);
        
        if (mapping != null)
        {
            return isGamepad ? mapping.gamepadIcon : mapping.keyboardIcon;
        }
        
        return null;
    }
}

/// <summary>
/// 액션 이름과 해당 아이콘 매핑 정보
/// </summary>
[Serializable]
public class InputIconMapping
{
    [Tooltip("액션 이름 (예: Player/Interact, Player/Use)")]
    public string actionName;
    
    [Tooltip("키보드/마우스 입력 아이콘")]
    public Sprite keyboardIcon;
    
    [Tooltip("게임패드 입력 아이콘")]
    public Sprite gamepadIcon;
}
