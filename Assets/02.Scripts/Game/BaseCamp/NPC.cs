using UnityEngine;
using Mirror;
using System.Collections.Generic;

/// <summary>
/// NPC 기본 클래스 - 쉬운 활용을 위해 모듈 사용
/// [박상민 - 2025.11.21]
/// </summary>
public class NPC : NetworkBehaviour, IInteractable
{
    [SerializeField] private GameObject _dialogueUIObject;
    [SerializeField] private List<GameObject> _dialoguePages;


    #region IInteractable 구현

    public InteractionInfo GetInteractionInfo()
    {
        // 서버에서만 정확한 정보 계산 가능
        if (!isServer)
        {
            Debug.LogWarning("GetInteractionInfo는 서버에서만 호출되어야 합니다!");
            return new InteractionInfo();
        }

        string text = "대화하기";
        string action = "Player/Interact";
        bool interactable = true;
        Vector3 position = transform.position + Vector3.up * 1.5f;

        return new InteractionInfo(netId, text, action, interactable, position);
    }

    #endregion

    void Awake()
    {
        // 대화 UI 비활성화
        if (_dialogueUIObject != null)
        {
            _dialogueUIObject.SetActive(false);
            foreach (var line in _dialoguePages)
            {
                line.SetActive(false);
            }
        }
    }

    public void InteractAction(PlayerInteractController caller)
    {
        if (!isServer)
        {
            return;
        }
        // 대화 UI 토글
        if (_dialogueUIObject != null)
        {
            // 클라이언트에 RPC 호출
            TargetInteractDialogueUI(caller.connectionToClient);
        }
    }

    #region 클라이언트 동기화

    /// <summary>
    /// 대화 UI를 표시하고 첫번째 페이지를 표시합니다.. 페이지가 남아있다면 다음 페이지로, 마지막 페이지면 UI를 닫습니다.
    /// </summary>
    /// <param name="target"></param>
    [TargetRpc]
    public void TargetInteractDialogueUI(NetworkConnection target)
    {
        if (_dialogueUIObject == null || _dialoguePages.Count == 0)
        {
            return;
        }

        // 대화 UI가 비활성화 상태라면 활성화 및 첫 페이지 표시
        if (!_dialogueUIObject.activeSelf)
        {
            _dialogueUIObject.SetActive(true);
            _dialoguePages[0].SetActive(true);
        }
        else
        {
            // 현재 활성화된 페이지 찾기
            int currentPageIndex = -1;
            for (int i = 0; i < _dialoguePages.Count; i++)
            {
                if (_dialoguePages[i].activeSelf)
                {
                    currentPageIndex = i;
                    break;
                }
            }

            // 다음 페이지로 이동 또는 UI 닫기
            if (currentPageIndex >= 0 && currentPageIndex < _dialoguePages.Count - 1)
            {
                _dialoguePages[currentPageIndex].SetActive(false);
                _dialoguePages[currentPageIndex + 1].SetActive(true);
            }
            else
            {
                // 마지막 페이지면 UI 닫기
                _dialoguePages[currentPageIndex].SetActive(false);
                _dialogueUIObject.SetActive(false);
            }
        }
    }

    [TargetRpc]
    public void RpcCloseDialogueUI(NetworkConnection target)
    {
        if (_dialogueUIObject != null)
        {
            _dialogueUIObject.SetActive(false);
            foreach (var line in _dialoguePages)
            {
                line.SetActive(false);
            }
        }
    }
    
    #endregion
}