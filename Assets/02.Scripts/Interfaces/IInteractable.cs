using UnityEngine;
using System;
/// <summary>
/// 상호작용 가능한 오브젝트가 구현해야 하는 인터페이스
/// </summary>
public interface IInteractable
{
    /// <summary>
    /// 서버에서 현재 상호작용 정보를 계산하여 반환
    /// </summary>
    /// <returns>상호작용 정보</returns>
    InteractionInfo GetInteractionInfo();
}

/// <summary>
/// 서버에서 클라이언트로 전달할 상호작용 정보
/// </summary>
[Serializable]
public struct InteractionInfo
{
    public uint targetNetId;
    public string interactionText;
    public string actionName;
    public bool isInteractable;
    public Vector3 uiPosition;

    public InteractionInfo(uint netId, string text, string action, bool interactable, Vector3 position)
    {
        targetNetId = netId;
        interactionText = text;
        actionName = action;
        isInteractable = interactable;
        uiPosition = position;
    }
}