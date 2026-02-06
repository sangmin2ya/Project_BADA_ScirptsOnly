using UnityEngine;
using Mirror;

/// <summary>
/// 플레이어가 NPC 혹은 텍스트를 띄우는 오브젝트와 상호작용하는 컨트롤러
/// [박상민 - 2025.11.21]
/// </summary>
public class PlayerInteractController : NetworkBehaviour
{
    // 플레이어의 상호작용 관련 로직을 처리하는 컨트롤러
    [SerializeField] private float _detectRadius = 2f;
    private NPC _detectedNPC;
    
    void Update()
    {
        if (!isServer)
        {
            return;
        }

        DetectNPC();
    }

    private void DetectNPC()
    {
        Collider2D[] colliders = Physics2D.OverlapCircleAll(transform.position, _detectRadius);
        NPC nearestNPC = null;
        float nearestDistanceSqr = float.MaxValue;

        foreach (var collider in colliders)
        {
            if (collider.TryGetComponent<NPC>(out var npc))
            {
                float distanceSqr = (npc.transform.position - transform.position).sqrMagnitude;
                if (distanceSqr < nearestDistanceSqr)
                {
                    nearestDistanceSqr = distanceSqr;
                    nearestNPC = npc;
                }
            }
        }

        if (nearestNPC != _detectedNPC)
        {
            // 이전에 감지된 NPC가 있으면 UI 숨김
            if (_detectedNPC != null)
            {
                RpcHideInteractionPrompt(connectionToClient, _detectedNPC.netId);
                _detectedNPC.RpcCloseDialogueUI(connectionToClient);
            }

            _detectedNPC = nearestNPC;

            // 새로 감지된 NPC가 있으면 UI 표시
            if (_detectedNPC != null)
            {
                InteractionInfo info = _detectedNPC.GetInteractionInfo();
                RpcShowInteractionPrompt(connectionToClient, info);
            }
        }
    }

    public bool CallInteractWithNPC()
    {
        if (!isServer)
        {
            return false;
        }

        if (_detectedNPC != null)
        {
            _detectedNPC.InteractAction(this);
            RpcHideInteractionPrompt(connectionToClient, _detectedNPC.netId);
            return true;
        }
        return false;
    }

    #region 상호작용 프롬프트 RPC

    /// <summary>
    /// 클라이언트에 상호작용 프롬프트 표시를 요청합니다
    /// </summary>
    [TargetRpc]
    private void RpcShowInteractionPrompt(NetworkConnection target, InteractionInfo info)
    {
        if (InteractionPromptManager.Instance == null)
        {
            return;
        }

        InteractionPromptManager.Instance.ShowPromptFromServer(info);
    }

    /// <summary>
    /// 클라이언트에 상호작용 프롬프트 숨김을 요청합니다
    /// </summary>
    [TargetRpc]
    private void RpcHideInteractionPrompt(NetworkConnection target, uint targetNetId)
    {
        if (InteractionPromptManager.Instance == null)
        {
            return;
        }

        InteractionPromptManager.Instance.HidePrompt(targetNetId);
    }

    #endregion
}