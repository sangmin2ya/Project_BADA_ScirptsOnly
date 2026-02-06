using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using Steamworks;
using TMPro;

/// <summary>
/// 로비 UI 전체를 관리하는 매니저 클래스
/// [박상민 - 2025.06.10]
/// </summary>
public class LobbyUIManager : MonoBehaviour
{
    [Header("<color=yellow>메인 패널들")]
    [Tooltip("메인 메뉴 패널")]
    [SerializeField] private GameObject _mainMenuPanel;
    [Tooltip("로비 내부 패널")]
    [SerializeField] private GameObject _insideLobbyPanel;

    [Header("<color=yellow>로비 생성/참가 UI")]
    [Tooltip("로비 코드 입력 필드")]
    [SerializeField] private TMP_InputField _lobbyCodeInput;

    [Header("<color=yellow>로비 내부 UI")]
    [Tooltip("로비 코드 표시 텍스트")]
    [SerializeField] private TMP_Text _lobbyCodeDisplayText;
    [Tooltip("로비 제목 표시 텍스트")]
    [SerializeField] private TMP_Text _lobbyTitleText;
    [Tooltip("참가자 수 표시 텍스트")]
    [SerializeField] private TMP_Text _memberCountText;
    [Tooltip("참가자 리스트 컨텐츠 Transform")]
    [SerializeField] private Transform _playerListContent;

    [Header("<color=yellow>버튼들")]
    [Tooltip("로비 생성 버튼")]
    [SerializeField] private Button _createLobbyButton;
    [Tooltip("로비 참가 버튼")]
    [SerializeField] private Button _joinLobbyButton;
    [Tooltip("로비 코드 복사 버튼")]
    [SerializeField] private Button _copyCodeButton;
    [Tooltip("Steam 친구 초대 버튼")]
    [SerializeField] private Button _inviteFriendsButton;
    [Tooltip("게임 준비 버튼")]
    [SerializeField] private ReadyButton _readyButton;
    [Tooltip("게임 시작 버튼")]
    [SerializeField] private Button _startGameButton;
    [Tooltip("로비 나가기 버튼")]
    [SerializeField] private Button _leaveLobbyButton;

    [Header("<color=yellow>알림 UI")]
    [Tooltip("알림 메시지 패널")]
    [SerializeField] private GameObject _notificationPanel;
    [Tooltip("알림 메시지 텍스트")]
    [SerializeField] private TMP_Text _notificationText;
    [Tooltip("알림 표시 시간")]
    [SerializeField] private float _notificationDuration = 3f;

    private SteamLobby _steamLobby;
    private Coroutine _notificationCoroutine;

    public enum UIState
    {
        MainMenu,
        InsideLobby
    }

    private UIState _currentState = UIState.MainMenu;

    void Start()
    {
        _steamLobby = FindObjectOfType<SteamLobby>();
        InitializeButtons();
        SetupInputField();
        SetUIState(UIState.MainMenu);
    }

    /// <summary>
    /// 버튼 이벤트 초기화
    /// </summary>
    private void InitializeButtons()
    {
        if (_createLobbyButton != null)
        {
            _createLobbyButton.onClick.AddListener(() => _steamLobby?.HostLobby());
        }
        if (_joinLobbyButton != null)
        {
            _joinLobbyButton.onClick.AddListener(() => _steamLobby?.JoinLobbyByCode());
        }
        if (_copyCodeButton != null)
        {
            _copyCodeButton.onClick.AddListener(CopyLobbyCode);
        }
        if (_inviteFriendsButton != null)
        {
            _inviteFriendsButton.onClick.AddListener(InviteSteamFriends);
        }
        if (_startGameButton != null)
        {
            _startGameButton.onClick.AddListener(() => _steamLobby?.StartGame());
        }
        if (_leaveLobbyButton != null)
        {
            _leaveLobbyButton.onClick.AddListener(() => _steamLobby?.LeaveLobby());
            _leaveLobbyButton.onClick.AddListener(() => SetUIState(UIState.MainMenu));
            _readyButton.gameObject.SetActive(true);
            _readyButton.ResetButton();
        }
    }

    /// <summary>
    /// 입력 필드 설정
    /// </summary>
    private void SetupInputField()
    {
        if (_lobbyCodeInput != null)
        {
            _lobbyCodeInput.onValueChanged.AddListener(OnLobbyCodeInputChanged);
            _lobbyCodeInput.characterLimit = 5;
            _lobbyCodeInput.contentType = TMP_InputField.ContentType.Alphanumeric;
        }
    }

    /// <summary>
    /// 로비 코드 입력 시 자동 대문자 변환
    /// </summary>
    /// <param name="input">입력된 텍스트</param>
    private void OnLobbyCodeInputChanged(string input)
    {
        string upperInput = input.ToUpper();
        if (input != upperInput)
        {
            _lobbyCodeInput.text = upperInput;
            _lobbyCodeInput.caretPosition = upperInput.Length;
        }
    }

    /// <summary>
    /// UI 상태 변경
    /// </summary>
    /// <param name="newState">새로운 UI 상태</param>
    public void SetUIState(UIState newState)
    {
        _currentState = newState;

        // 모든 패널 비활성화
        if (_mainMenuPanel != null)
        {
            _mainMenuPanel.SetActive(false);
        }
        if (_insideLobbyPanel != null)
        {
            _insideLobbyPanel.SetActive(false);
        }

        // 현재 상태에 맞는 패널 활성화
        switch (newState)
        {
            case UIState.MainMenu:
                if (_mainMenuPanel != null)
                {
                    _mainMenuPanel.SetActive(true);
                }
                break;
            case UIState.InsideLobby:
                if (_insideLobbyPanel != null)
                {
                    _insideLobbyPanel.SetActive(true);
                }
                break;
        }
    }

    /// <summary>
    /// 로비 정보 UI 업데이트
    /// </summary>
    /// <param name="lobbyCode">로비 코드</param>
    /// <param name="memberCount">참가자 수</param>
    /// <param name="maxMembers">최대 참가자 수</param>
    /// <param name="isHost">호스트 여부</param>
    public void UpdateLobbyInfo(string hostName, string lobbyCode, int memberCount, int maxMembers, bool isHost)
    {
        if (_lobbyTitleText != null)
        {
            _lobbyTitleText.text = $"{hostName}의 방";
        }

        if (_lobbyCodeDisplayText != null)
        {
            _lobbyCodeDisplayText.text = $"{lobbyCode}";
        }

        if (_memberCountText != null)
        {
            _memberCountText.text = $"참가자: {memberCount}/{maxMembers}";
        }

        if (_startGameButton != null)
        {
            _startGameButton.gameObject.SetActive(isHost);
        }

        if (_copyCodeButton != null)
        {
            _copyCodeButton.interactable = true;
        }

        if (_inviteFriendsButton != null)
        {
            _inviteFriendsButton.interactable = true;
        }
    }

    /// <summary>
    /// 로비 코드 클립보드에 복사
    /// </summary>
    private void CopyLobbyCode()
    {
        if (_lobbyCodeDisplayText != null)
        {
            string codeText = _lobbyCodeDisplayText.text;
            string code = codeText.Replace("로비 코드: ", "").Trim();

            GUIUtility.systemCopyBuffer = code;
            ShowNotification($"로비 코드 '{code}'가 복사되었습니다!");
        }
    }

    /// <summary>
    /// Steam 친구 초대
    /// </summary>
    private void InviteSteamFriends()
    {
        if (SteamManager.Initialized)
        {
            // Steam Overlay 친구 초대 창 열기
            SteamFriends.ActivateGameOverlayInviteDialog(SteamLobby.CurrentLobbyId);
        }
    }

    /// <summary>
    /// 알림 메시지 표시
    /// </summary>
    /// <param name="message">알림 메시지</param>
    public void ShowNotification(string message)
    {
        if (_notificationPanel == null || _notificationText == null)
        {
            return;
        }

        _notificationText.text = message;
        _notificationPanel.SetActive(true);

        // 기존 코루틴 정지
        if (_notificationCoroutine != null)
        {
            StopCoroutine(_notificationCoroutine);
        }

        // 새로운 알림 코루틴 시작
        _notificationCoroutine = StartCoroutine(HideNotificationAfterDelay());
    }

    /// <summary>
    /// 지정된 시간 후 알림 숨기기
    /// </summary>
    private System.Collections.IEnumerator HideNotificationAfterDelay()
    {
        yield return new WaitForSeconds(_notificationDuration);

        if (_notificationPanel != null)
        {
            _notificationPanel.SetActive(false);
        }

        _notificationCoroutine = null;
    }

    /// <summary>
    /// 입력 필드 클리어
    /// </summary>
    public void ClearLobbyCodeInput()
    {
        if (_lobbyCodeInput != null)
        {
            _lobbyCodeInput.text = string.Empty;
        }
    }

    /// <summary>
    /// 로비 나가기 시 UI 초기화
    /// </summary>
    public void OnLeaveLobby()
    {
        ClearLobbyCodeInput();
        SetUIState(UIState.MainMenu);

        if (_notificationPanel != null)
        {
            _notificationPanel.SetActive(false);
        }
    }

    /// <summary>
    /// 현재 UI 상태 반환
    /// </summary>
    public UIState CurrentState => _currentState;

    /// <summary>
    /// 입력된 로비 코드 반환
    /// </summary>
    public string GetInputLobbyCode()
    {
        return _lobbyCodeInput != null ? _lobbyCodeInput.text.ToUpper().Trim() : string.Empty;
    }

    /// <summary>
    /// 참가자 리스트 컨텐츠 Transform 반환
    /// </summary>
    public Transform GetPlayerListContent()
    {
        return _playerListContent;
    }
}