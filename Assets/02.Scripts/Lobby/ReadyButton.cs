using UnityEngine;
using UnityEngine.UI;
using Steamworks;
using TMPro;
using Mirror;

/// <summary>
/// 플레이어의 준비 상태를 UI로 보여주는 버튼 컨트롤러
/// [박상민 - 2025.06.11 - 네트워크 리팩토링]
/// </summary>
public class ReadyButton : MonoBehaviour
{
    [Header("<color=yellow>UI 컴포넌트")]
    [Tooltip("준비 버튼")]
    [SerializeField] private Button _readyButton;
    [Tooltip("준비 상태 텍스트")]
    [SerializeField] private TMP_Text _readyButtonText;

    [Header("<color=green>UI 설정")]
    [Tooltip("준비 완료 시 버튼 색상")]
    [SerializeField] private Color _readyColor = Color.green;
    [Tooltip("준비 해제 시 버튼 색상")]
    [SerializeField] private Color _notReadyColor = Color.red;
    [Tooltip("준비 완료 텍스트")]
    [SerializeField] private string _readyText = "준비 완료";
    [Tooltip("준비 해제 텍스트")]
    [SerializeField] private string _notReadyText = "준비하기";

    private SteamLobby _steamLobby;
    private LobbyPlayerItem _localPlayerItem;

    void Start()
    {
        _steamLobby = FindObjectOfType<SteamLobby>();

        if (_readyButton != null)
        {
            _readyButton.onClick.AddListener(OnReadyButtonClicked);
        }

        UpdateButtonAppearance();
    }

    void Update()
    {
        // 로컬 플레이어 아이템을 동적으로 찾기
        if (_localPlayerItem == null)
        {
            FindLocalPlayerItem();
        }

        // UI 업데이트
        UpdateUI();
    }

    /// <summary>
    /// 로컬 플레이어 아이템 찾기
    /// </summary>
    private void FindLocalPlayerItem()
    {
        if (_steamLobby == null)
        {
            return;
        }

        // 내 플레이어 반환
        _localPlayerItem = NetworkClient.localPlayer?.GetComponent<LobbyPlayerItem>();
    }

    /// <summary>
    /// UI 상태 업데이트
    /// </summary>
    private void UpdateUI()
    {
        if (_localPlayerItem == null)
        {
            return;
        }

        // 호스트 상태에 따른 버튼 표시/숨김
        bool isHost = _localPlayerItem.IsHost;
        _readyButton.gameObject.SetActive(!isHost);

        // 준비 상태에 따른 버튼 외형 업데이트
        bool isReady = _localPlayerItem.IsReady;
        UpdateButtonAppearance(isReady);
    }

    /// <summary>
    /// 준비 버튼 클릭 처리
    /// </summary>
    private void OnReadyButtonClicked()
    {
        if (_steamLobby == null || !_steamLobby.HasValidLobby())
        {
            Debug.LogWarning("유효한 로비가 없습니다.");
            return;
        }

        if (_localPlayerItem == null)
        {
            Debug.LogWarning("로컬 플레이어 아이템을 찾을 수 없습니다.");
            return;
        }

        // 호스트는 준비 상태를 토글할 수 없음
        if (_localPlayerItem.IsHost)
        {
            return;
        }

        // 네트워크를 통해 준비 상태 토글
        _localPlayerItem.ToggleReady();
    }

    /// <summary>
    /// 버튼 외형 업데이트
    /// </summary>
    private void UpdateButtonAppearance(bool isReady = false)
    {
        if (_readyButtonText != null)
        {
            _readyButtonText.text = isReady ? _readyText : _notReadyText;
        }

        if (_readyButton != null)
        {
            ColorBlock colors = _readyButton.colors;
            colors.normalColor = isReady ? _readyColor : _notReadyColor;
            colors.highlightedColor = isReady ? _readyColor * 1.1f : _notReadyColor * 1.1f;
            _readyButton.colors = colors;
        }
    }
    /// <summary>
    /// 버튼 상태 초기화
    /// </summary>
    public void ResetButton()
    {
        _localPlayerItem = null;
        UpdateButtonAppearance(false);
    }
}