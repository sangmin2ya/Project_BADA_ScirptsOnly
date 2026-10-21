using Steamworks;
using Mirror;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Collections;

/// <summary>
/// Steam 로비 생성, 참가 및 호스트/클라이언트 연결을 관리하는 클래스
/// [박상민 - 2025.06.11 - 네트워크 리팩토링]
/// </summary>
public class SteamLobby : MonoBehaviour
{
    [Header("<color=yellow>매니저 참조")]
    [Tooltip("로비 UI 매니저")]
    [SerializeField] private LobbyUIManager _uiManager;
    [Tooltip("로비 데이터 매니저")]
    [SerializeField] private LobbyDataManager _dataManager;

    [Header("<color=blue>프리팹")]
    [Tooltip("참가자 UI 프리팹 (NetworkIdentity 포함)")]
    [SerializeField] private GameObject _playerItemPrefab;

    // Steam 콜백
    protected Callback<LobbyCreated_t> _lobbyCreated;
    protected Callback<GameLobbyJoinRequested_t> _gameLobbyJoinRequested;
    protected Callback<LobbyEnter_t> _lobbyEntered;
    protected Callback<LobbyChatUpdate_t> _lobbyChatUpdate;
    protected Callback<LobbyDataUpdate_t> _lobbyDataUpdate;

    private GameNetworkManager _gameNetworkManager;
    private CSteamID _currentLobbyId;
    private bool _isHost;

    // 네트워크 플레이어 UI 관리
    private Dictionary<NetworkConnectionToClient, LobbyPlayerItem> _networkPlayerItems = new Dictionary<NetworkConnectionToClient, LobbyPlayerItem>();

    // 로비 검색 관련
    private CallResult<LobbyMatchList_t> _lobbyMatchListCallResult;

    // 정적 프로퍼티 (UI에서 접근용)
    public static CSteamID CurrentLobbyId { get; private set; }

    void Start()
    {
        _gameNetworkManager = GameNetworkManager.singleton;

        if (!SteamManager.Initialized)
        {
            Debug.LogError("Steam이 초기화되지 않았습니다!");
            return;
        }

        InitializeCallbacks();
        InitializeManagers();
        RegisterNetworkEvents();
    }

    /// <summary>
    /// Steam 콜백 초기화
    /// </summary>
    private void InitializeCallbacks()
    {
        _lobbyCreated = Callback<LobbyCreated_t>.Create(OnLobbyCreated);
        _gameLobbyJoinRequested = Callback<GameLobbyJoinRequested_t>.Create(OnGameLobbyJoinRequested);
        _lobbyEntered = Callback<LobbyEnter_t>.Create(OnLobbyEntered);
        _lobbyChatUpdate = Callback<LobbyChatUpdate_t>.Create(OnLobbyChatUpdate);
        _lobbyDataUpdate = Callback<LobbyDataUpdate_t>.Create(OnLobbyDataUpdate);

        _lobbyMatchListCallResult = CallResult<LobbyMatchList_t>.Create(OnLobbyMatchList);
    }

    /// <summary>
    /// 매니저 초기화
    /// </summary>
    private void InitializeManagers()
    {
        if (_uiManager == null)
        {
            _uiManager = FindObjectOfType<LobbyUIManager>();
        }

        if (_dataManager == null)
        {
            _dataManager = FindObjectOfType<LobbyDataManager>();
        }

        if (_uiManager != null)
        {
            _uiManager.SetUIState(LobbyUIManager.UIState.MainMenu);
        }
    }

    /// <summary>
    /// 네트워크 이벤트 등록
    /// </summary>
    private void RegisterNetworkEvents()
    {
        if (_gameNetworkManager != null)
        {
            // 네트워크 매니저에 커스텀 플레이어 생성 이벤트 등록
            _gameNetworkManager.OnServerAddPlayerEvent += OnServerAddPlayer;
            _gameNetworkManager.OnClientConnectEvent += OnClientConnect;
            _gameNetworkManager.OnClientDisconnectEvent += OnClientDisconnect;
        }
    }

    /// <summary>
    /// 로비를 생성하고 호스트를 시작
    /// </summary>
    public void HostLobby()
    {
        Debug.Log("로비 생성 시작...");
        _uiManager?.ShowNotification("로비를 생성하는 중...");

        SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypePublic, _gameNetworkManager.maxConnections);
    }

    /// <summary>
    /// 로비 코드로 참가
    /// </summary>
    public void JoinLobbyByCode()
    {
        string inputCode = _uiManager?.GetInputLobbyCode();

        if (string.IsNullOrEmpty(inputCode))
        {
            _uiManager?.ShowNotification("로비 코드를 입력해주세요.");
            return;
        }

        if (!_dataManager.IsValidLobbyCode(inputCode))
        {
            _uiManager?.ShowNotification("올바른 5자리 로비 코드를 입력해주세요.");
            return;
        }

        _uiManager?.ShowNotification($"로비 '{inputCode}' 검색 중...");
        FindAndJoinLobbyByCode(inputCode);
    }

    /// <summary>
    /// 로비 코드로 로비 검색 후 참가
    /// </summary>
    private void FindAndJoinLobbyByCode(string code)
    {
        Debug.Log($"로비 코드 검색 중: {code}");

        // 필터 초기화
        SteamMatchmaking.AddRequestLobbyListNumericalFilter("MaxPlayers", 0, ELobbyComparison.k_ELobbyComparisonGreaterThan);
        SteamMatchmaking.AddRequestLobbyListDistanceFilter(ELobbyDistanceFilter.k_ELobbyDistanceFilterWorldwide);

        // 게임 식별자 필터 설정 (공용 App ID를 쓰는 다른 게임의 로비 제외)
        SteamMatchmaking.AddRequestLobbyListStringFilter(LobbyDataManager.GameIdKey, LobbyDataManager.GameName, ELobbyComparison.k_ELobbyComparisonEqual);
        // 게임 버전 필터 설정 (버전이 다른 호스트와 연결되지 않도록)
        SteamMatchmaking.AddRequestLobbyListStringFilter(LobbyDataManager.GameVersionKey, Application.version, ELobbyComparison.k_ELobbyComparisonEqual);
        // 로비 코드 필터 설정
        SteamMatchmaking.AddRequestLobbyListStringFilter(LobbyDataManager.LobbyCodeKey, code, ELobbyComparison.k_ELobbyComparisonEqual);

        // 로비 리스트 요청
        SteamAPICall_t apiCall = SteamMatchmaking.RequestLobbyList();
        _lobbyMatchListCallResult.Set(apiCall);
    }

    /// <summary>
    /// 로비 나가기
    /// </summary>
    public void LeaveLobby()
    {
        if (_currentLobbyId.IsValid())
        {
            SteamMatchmaking.LeaveLobby(_currentLobbyId);

            if (_isHost)
            {
                _gameNetworkManager.StopHost();
            }
            else
            {
                _gameNetworkManager.StopClient();
            }
        }

        ResetLobbyState();
        _uiManager?.OnLeaveLobby();
        _uiManager?.ShowNotification("로비에서 나왔습니다.");
    }

    /// <summary>
    /// 게임 시작 (호스트만)
    /// </summary>
    public void StartGame()
    {
        if (!_isHost)
        {
            _uiManager?.ShowNotification("호스트만 게임을 시작할 수 있습니다.");
            return;
        }

        if (!AreAllPlayersReady())
        {
            _uiManager?.ShowNotification("모든 플레이어가 준비되지 않았습니다.");
            return;
        }

        Debug.Log("게임 시작!");
        _uiManager?.ShowNotification("게임을 시작합니다!");

        // 로비 메타데이터 업데이트
        _dataManager?.SetLobbyMetadata(_currentLobbyId, GameStateType.게임중);
        // 게임시작 - 씬 전환 및 플레이어 교체
        _gameNetworkManager.StartGame();
    }

    /// <summary>
    /// 플레이어 준비 상태 설정 (ReadyButton에서 호출)
    /// </summary>
    public void SetPlayerReady(bool isReady)
    {
        if (!_currentLobbyId.IsValid() || _isHost)
        {
            return;
        }

        // 로컬 플레이어의 LobbyPlayerItem 찾기
        var localPlayerItem = GetLocalPlayerItem();
        if (localPlayerItem != null)
        {
            localPlayerItem.CmdSetReadyStatus(isReady);
        }
    }

    /// <summary>
    /// 모든 플레이어가 준비되었는지 확인
    /// </summary>
    private bool AreAllPlayersReady()
    {
        foreach (var playerItem in _networkPlayerItems.Values)
        {
            if (playerItem != null && !playerItem.IsReady)
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>
    /// 로컬 플레이어 아이템 찾기
    /// </summary>
    private LobbyPlayerItem GetLocalPlayerItem()
    {
        foreach (var playerItem in _networkPlayerItems.Values)
        {
            if (playerItem != null && playerItem.IsLocalPlayer())
            {
                return playerItem;
            }
        }
        return null;
    }

    /// <summary>
    /// 로비 상태 초기화
    /// </summary>
    private void ResetLobbyState()
    {
        _currentLobbyId = CSteamID.Nil;
        CurrentLobbyId = CSteamID.Nil;
        _isHost = false;

        // 데이터 매니저 초기화
        _dataManager?.ClearLobbyData();

        // 네트워크 플레이어 아이템 정리
        _networkPlayerItems.Clear();
    }

    /// <summary>
    /// 로비 정보 업데이트
    /// </summary>
    private void UpdateLobbyInfo()
    {
        if (!_currentLobbyId.IsValid()) return;

        int memberCount = _networkPlayerItems.Count;
        string lobbyCode = _dataManager?.CurrentLobbyCode ?? "";
        string hostName = _dataManager?.CurrentHostName ?? "";

        _uiManager?.UpdateLobbyInfo(
            hostName,
            lobbyCode,
            memberCount,
            _gameNetworkManager.maxConnections,
            _isHost
        );

        Debug.Log($"로비 정보 업데이트: {memberCount}명 참가 중");
    }

    #region Network Events

    /// <summary>
    /// 서버에서 플레이어 추가 시 호출
    /// </summary>
    private void OnServerAddPlayer(NetworkConnectionToClient conn)
    {
        if (_playerItemPrefab == null)
        {
            Debug.LogError("플레이어 아이템 프리팹이 설정되지 않았습니다!");
            return;
        }

        // 로비 플레이어 아이템 생성
        GameObject playerItemObj = Instantiate(_playerItemPrefab);
        LobbyPlayerItem playerItem = playerItemObj.GetComponent<LobbyPlayerItem>();

        if (playerItem == null)
        {
            Debug.LogError("LobbyPlayerItem 컴포넌트를 찾을 수 없습니다!");
            Destroy(playerItemObj);
            return;
        }

        // 네트워크에 플레이어로 추가
        NetworkServer.AddPlayerForConnection(conn, playerItemObj);

        // 플레이어 아이템 등록
        _networkPlayerItems[conn] = playerItem;

        // 호스트 설정 (첫 번째 연결이 호스트)
        if (_networkPlayerItems.Count == 1)
        {
            playerItem.SetHostStatus(true);
        }

        Debug.Log($"플레이어 아이템 생성 완료: {conn.connectionId}");

        // 로비 정보 업데이트
        UpdateLobbyInfo();
    }

    /// <summary>
    /// 클라이언트 연결 시 호출
    /// </summary>
    private void OnClientConnect(NetworkConnectionToClient conn)
    {
        Debug.Log($"클라이언트 연결: {conn.connectionId}");
        UpdateLobbyInfo();
    }

    /// <summary>
    /// 클라이언트 연결 해제 시 호출
    /// </summary>
    private void OnClientDisconnect(NetworkConnectionToClient conn)
    {
        Debug.Log($"클라이언트 연결 해제: {conn.connectionId}");

        // 플레이어 아이템 제거
        if (_networkPlayerItems.ContainsKey(conn))
        {
            var playerItem = _networkPlayerItems[conn];
            if (playerItem != null && playerItem.gameObject != null)
            {
                // UI에서도 제거
                Destroy(playerItem.gameObject);
            }
            _networkPlayerItems.Remove(conn);
        }

        UpdateLobbyInfo();
    }

    #endregion

    #region Steam 콜백 처리

    /// <summary>
    /// 로비 생성 후 호출되는 콜백
    /// </summary>
    void OnLobbyCreated(LobbyCreated_t callback)
    {
        if (callback.m_eResult != EResult.k_EResultOK)
        {
            Debug.LogError($"로비 생성 실패: {callback.m_eResult}");
            _uiManager?.ShowNotification($"로비 생성 실패: {callback.m_eResult}");
            return;
        }

        _currentLobbyId = new CSteamID(callback.m_ulSteamIDLobby);
        CurrentLobbyId = _currentLobbyId;
        _isHost = true;

        string lobbyCode = _dataManager.GenerateLobbyCode();
        string hostAddress = SteamUser.GetSteamID().ToString();

        // 로비 메타데이터 설정
        _dataManager.SetLobbyMetadata(_currentLobbyId, lobbyCode, SteamFriends.GetFriendPersonaName(SteamUser.GetSteamID()), hostAddress, _gameNetworkManager.maxConnections);

        // UI 상태 변경
        _uiManager?.SetUIState(LobbyUIManager.UIState.InsideLobby);

        // Mirror 호스트 시작
        _gameNetworkManager.StartHost();

        _uiManager?.ShowNotification($"로비 생성 성공! 코드: {lobbyCode}");
        Debug.Log($"<color=green>로비 생성 성공! 코드: {lobbyCode}</color>");

        // 생성 후 즉시 확인
        string createdCode = SteamMatchmaking.GetLobbyData(_currentLobbyId, "LobbyCode");
        Debug.Log($"생성된 로비 코드 확인: {createdCode}");
    }

    /// <summary>
    /// Steam에서 게임 참가 요청이 들어왔을 때 호출되는 콜백
    /// </summary>
    void OnGameLobbyJoinRequested(GameLobbyJoinRequested_t callback)
    {
        Debug.Log("Steam 초대를 통한 로비 참가 요청");
        _uiManager?.ShowNotification("Steam 초대를 통해 로비에 참가합니다...");
        SteamMatchmaking.JoinLobby(callback.m_steamIDLobby);
    }

    /// <summary>
    /// 로비에 입장했을 때 호출되는 콜백
    /// </summary>
    void OnLobbyEntered(LobbyEnter_t callback)
    {
        _currentLobbyId = new CSteamID(callback.m_ulSteamIDLobby);
        CurrentLobbyId = _currentLobbyId;

        if (NetworkServer.active)
        {
            // 호스트는 이미 처리됨
            return;
        }

        // 클라이언트 처리
        _isHost = false;
        string lobbyCode = _dataManager.GetLobbyData(_currentLobbyId, LobbyDataManager.LobbyCodeKey);
        string hostAddress = _dataManager.GetLobbyData(_currentLobbyId, "HostAddress");

        // 로비 메타데이터 불러와서 저장
        _dataManager.SetLobbyData(_currentLobbyId);

        // UI 상태 변경
        _uiManager?.SetUIState(LobbyUIManager.UIState.InsideLobby);
        _uiManager?.ClearLobbyCodeInput();

        // Mirror 클라이언트로 연결
        _gameNetworkManager.networkAddress = hostAddress;
        _gameNetworkManager.StartClient();

        _uiManager?.ShowNotification($"로비 '{lobbyCode}' 입장 성공!");
        Debug.Log($"<color=green>로비 입장 성공! 코드: {lobbyCode}</color>");
    }

    /// <summary>
    /// 로비 멤버 변경 시 호출되는 콜백
    /// </summary>
    void OnLobbyChatUpdate(LobbyChatUpdate_t callback)
    {
        if (callback.m_ulSteamIDLobby != _currentLobbyId.m_SteamID)
        {
            return;
        }

        Debug.Log($"로비 멤버 변경: {callback.m_rgfChatMemberStateChange}");

        // 멤버 변경 알림
        CSteamID changedUser = new CSteamID(callback.m_ulSteamIDUserChanged);
        string userName = SteamFriends.GetFriendPersonaName(changedUser);

        if ((callback.m_rgfChatMemberStateChange & (uint)EChatMemberStateChange.k_EChatMemberStateChangeEntered) != 0)
        {
            _uiManager?.ShowNotification($"{userName}님이 로비에 참가했습니다.");
        }
        else if ((callback.m_rgfChatMemberStateChange & (uint)EChatMemberStateChange.k_EChatMemberStateChangeLeft) != 0)
        {
            _uiManager?.ShowNotification($"{userName}님이 로비에서 나갔습니다.");
        }

        // 로비 정보 업데이트
        UpdateLobbyInfo();
    }

    /// <summary>
    /// 로비 데이터 업데이트 시 호출되는 콜백
    /// </summary>
    void OnLobbyDataUpdate(LobbyDataUpdate_t callback)
    {
        if (callback.m_ulSteamIDLobby != _currentLobbyId.m_SteamID)
        {
            return;
        }

        Debug.Log("로비 데이터 업데이트");
        UpdateLobbyInfo();
    }

    /// <summary>
    /// 로비 검색 결과 콜백
    /// </summary>
    void OnLobbyMatchList(LobbyMatchList_t callback, bool ioFailure)
    {
        if (ioFailure)
        {
            Debug.LogError("로비 검색 실패");
            _uiManager?.ShowNotification("로비 검색에 실패했습니다.");
            return;
        }

        Debug.Log($"로비 검색 결과: {callback.m_nLobbiesMatching}개 발견");

        if (callback.m_nLobbiesMatching == 0)
        {
            _uiManager?.ShowNotification("해당 코드의 로비를 찾을 수 없습니다.");
            return;
        }

        // 첫 번째 일치하는 로비에 참가
        CSteamID lobbyId = SteamMatchmaking.GetLobbyByIndex(0);
        string foundCode = SteamMatchmaking.GetLobbyData(lobbyId, "LobbyCode");

        Debug.Log($"로비 발견! 참가 시도: {foundCode}");
        _uiManager?.ShowNotification($"로비 '{foundCode}' 발견! 참가 중...");

        // 로비가 진행중이면 참가 불가
        if (SteamMatchmaking.GetLobbyData(lobbyId, LobbyDataManager.GameStatusKey) == GameStateType.게임중.ToString())
        {
            _uiManager?.ShowNotification("이미 게임이 진행 중인 로비입니다.");
            return;
        }

        SteamMatchmaking.JoinLobby(lobbyId);
    }

    #endregion

    #region 공개 API

    /// <summary>
    /// 현재 로비가 유효한지 확인
    /// </summary>
    public bool HasValidLobby()
    {
        return _currentLobbyId.IsValid();
    }

    /// <summary>
    /// 현재 사용자가 호스트인지 확인
    /// </summary>
    public bool IsCurrentUserHost()
    {
        return _isHost;
    }

    /// <summary>
    /// 현재 로비의 멤버 수 반환
    /// </summary>
    public int GetCurrentLobbyMemberCount()
    {
        return _networkPlayerItems.Count;
    }

    /// <summary>
    /// 현재 로비 코드 반환
    /// </summary>
    public string GetCurrentLobbyCode()
    {
        return _dataManager?.CurrentLobbyCode ?? string.Empty;
    }

    /// <summary>
    /// 네트워크 플레이어 아이템들 반환
    /// </summary>
    public Dictionary<NetworkConnectionToClient, LobbyPlayerItem> GetNetworkPlayerItems()
    {
        return _networkPlayerItems;
    }

    #endregion

    void OnDestroy()
    {
        ResetLobbyState();

        // 네트워크 이벤트 해제
        if (_gameNetworkManager != null)
        {
            _gameNetworkManager.OnServerAddPlayerEvent -= OnServerAddPlayer;
            _gameNetworkManager.OnClientConnectEvent -= OnClientConnect;
            _gameNetworkManager.OnClientDisconnectEvent -= OnClientDisconnect;
        }
    }
}