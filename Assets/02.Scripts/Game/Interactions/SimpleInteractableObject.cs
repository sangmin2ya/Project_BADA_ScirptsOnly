using Mirror;
using UnityEngine;

/// <summary>
/// 테스트용 간단한 상호작용 오브젝트
/// 문, 레버, 스위치 등에 사용 가능
/// [박상민 - 2025.11.20]
/// </summary>
public class SimpleInteractableObject : NetworkBehaviour, IInteractable
{
    #region SerializeField

    [Tooltip("상호작용 시 표시될 텍스트")]
    [SerializeField] private string interactionText = "상호작용";
    
    [Tooltip("사용할 액션 이름")]
    [SerializeField] private string actionName = "Player/Interact";
    
    [Tooltip("UI 오프셋 (오브젝트 위 높이)")]
    [SerializeField] private float uiOffsetY = 2f;
    
    [Tooltip("현재 상호작용 가능 여부")]
    [SerializeField] private bool isInteractable = true;

    #endregion

    #region IInteractable 구현

    public string InteractionText => interactionText;

    public string ActionName => actionName;

    public Vector3 GetInteractionUIPosition()
    {
        return transform.position + Vector3.up * uiOffsetY;
    }

    public bool IsInteractable => isInteractable;

    public InteractionInfo GetInteractionInfo()
    {
        // 서버에서만 정확한 정보 계산 가능
        uint netId = 0;
        if (TryGetComponent<NetworkIdentity>(out var netIdentity))
        {
            netId = netIdentity.netId;
        }

        return new InteractionInfo(
            netId,
            interactionText,
            actionName,
            isInteractable,
            GetInteractionUIPosition()
        );
    }

    #endregion

    #region Public 메서드

    /// <summary>
    /// 상호작용 가능 여부를 설정합니다
    /// </summary>
    /// <param name="value">상호작용 가능 여부</param>
    public void SetInteractable(bool value)
    {
        isInteractable = value;
    }

    /// <summary>
    /// 상호작용 텍스트를 변경합니다
    /// </summary>
    /// <param name="text">새로운 텍스트</param>
    public void SetInteractionText(string text)
    {
        interactionText = text;
    }

    #endregion

    #region 테스트용 Gizmo

    private void OnDrawGizmosSelected()
    {
        // UI가 표시될 위치를 Gizmo로 표시
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(GetInteractionUIPosition(), 0.2f);
        
        // 상호작용 가능 여부에 따라 색상 변경
        Gizmos.color = isInteractable ? Color.green : Color.red;
        Gizmos.DrawWireSphere(transform.position, 0.5f);
    }

    #endregion
}
