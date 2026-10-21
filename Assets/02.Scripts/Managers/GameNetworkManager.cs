using UnityEngine;
using Mirror;
using System;

/// <summary>
/// GameNetworkManager - 네트워크 관리 및 씬 전환
/// [박상민 - 2025.06.11 - 네트워크 리팩토링]
/// </summary>
public class GameNetworkManager : NetworkManager
{
    [Header("씬 전환 설정")]
    [Tooltip("게임 준비 씬 이름")]
    [SerializeField][Scene] private string _baseCampSceneName;
    [Tooltip("게임 씬 이름")]
    [SerializeField][Scene] private string _gameSceneName;
    [Tooltip("실제 플레이어 프리팹 (게임 씬용)")]
    [SerializeField] private GameObject _gamePlayerPrefab;
    [Header("매니저 프리팹")]
    [SerializeField] private GameObject _gameManagerPrefab;
    [SerializeField] private GameObject _sessionManagerPrefab;
    [SerializeField] private GameObject _interactionManagerPrefab;
    [Header("스포너 프리팹")]
    [Tooltip("스포너 프리팹")]
    [SerializeField] private GameObject _baseCampSpawnerPrefab;
    [SerializeField] private GameObject _shipSpawnerPrefab;
    [SerializeField] private GameObject _objectSpawnerPrefab;
    [SerializeField] private GameObject _backgroundSpawnerPrefab;
    [SerializeField] private GameObject _dummyObjectSpawnerPrefab;
    [SerializeField] private GameObject _inputHandlerPrefab;

    // 커스텀 이벤트들
    public event Action<NetworkConnectionToClient> OnServerAddPlayerEvent;
    public event Action<NetworkConnectionToClient> OnClientConnectEvent;
    public event Action<NetworkConnectionToClient> OnClientDisconnectEvent;

    //프로퍼티
    public string BaseCampSceneName => System.IO.Path.GetFileNameWithoutExtension(_baseCampSceneName);
    public string GameSceneName => System.IO.Path.GetFileNameWithoutExtension(_gameSceneName);

    // 플레이어 교체 관련
    private bool _isGameStarted = false;

    private bool _isSessionStarted = false;

    // 싱글톤 참조 (새로운 타입으로)
    public static new GameNetworkManager singleton { get; private set; }

    public override void Awake()
    {
        base.Awake();
        singleton = this;
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        Debug.Log("서버 시작됨");
    }

    /// <summary>
    /// 게임 시작 (베이스 캠프로 전환)
    /// </summary>
    public void StartGame()
    {
        if (!NetworkServer.active)
        {
            Debug.LogError("서버가 활성화되지 않았습니다!");
            return;
        }

        _isGameStarted = true;

        // 매니져 프리팹 스폰
        if (_gameManagerPrefab != null)
        {
            GameObject gameManager = Instantiate(_gameManagerPrefab);
            NetworkServer.Spawn(gameManager);
            Debug.Log("GameManager 스폰 완료");
        }

        // 게임 씬으로 전환
        ServerChangeScene(_baseCampSceneName);
    }

    /// <summary>
    /// 세션 시작 (게임 씬으로 전환)
    /// </summary>
    public void StartSession()
    {
        if (!NetworkServer.active)
        {
            Debug.LogError("서버가 활성화되지 않았습니다!");
            return;
        }

        _isSessionStarted = true;

        // 게임 씬으로 전환
        ServerChangeScene(_gameSceneName);
    }

    /// <summary>
    /// 세션을 마무리하고 베이스 캠프로 돌아갑니다. (탈출 혹은 게임오버)
    /// </summary>
    public void ReturnToBaseCamp()
    {
        if (!NetworkServer.active)
        {
            Debug.LogError("서버가 활성화되지 않았습니다!");
            return;
        }

        _isSessionStarted = false;

        // 베이스 캠프 씬으로 전환
        ServerChangeScene(_baseCampSceneName);
    }

    /// <summary>
    /// // 세션 종료 (베이스 캠프로 전환)
    /// </summary>
    public void StopSession()
    {
        if (!NetworkServer.active)
        {
            Debug.LogError("서버가 활성화되지 않았습니다!");
            return;
        }

        _isSessionStarted = false;

        // 베이스 캠프 씬으로 전환
        ServerChangeScene(_baseCampSceneName);
    }

    #region Network Manager Overrides

    /// <summary>
    /// 플레이어 추가 요청 처리
    /// </summary>
    public override void OnServerAddPlayer(NetworkConnectionToClient conn)
    {
        if (_isGameStarted)
        {
            // 게임이 시작된 경우 실제 플레이어 프리팹 사용
            if (_gamePlayerPrefab == null)
            {
                return;
            }

            // 게임 스폰 위치 계산 - BaseCampManager에서 스폰위치 가져오기
            GameObject playerObject = Instantiate(_gamePlayerPrefab, Vector2.zero, Quaternion.identity);

            // 실제 게임 플레이어 스폰
            NetworkServer.AddPlayerForConnection(conn, playerObject);

            // 플레이어 설정
            PlayerController playerController = playerObject.GetComponent<PlayerController>();
            if (playerController != null)
            {
                playerController.SetupPlayer(conn.connectionId);
            }

            Debug.Log($"게임 플레이어 스폰: {conn.connectionId}");
        }
        else
        {
            // 로비에서는 커스텀 이벤트로 처리
            OnServerAddPlayerEvent?.Invoke(conn);
        }
    }

    /// <summary>
    /// 씬 변경 시 플레이어 교체 처리
    /// </summary>
    public override void OnServerSceneChanged(string sceneName)
    {
        base.OnServerSceneChanged(sceneName);
        if (sceneName == _baseCampSceneName && _isGameStarted)
        {
            Debug.Log("베이스 캠프 씬으로 전환됨");

            // 씬 변경 후에는 자동으로 OnServerAddPlayer가 호출되어 베이스캠프 플레이어가 생성됩니다.
            // 스포너 프리팹 스폰
            if (_baseCampSpawnerPrefab != null)
            {
                GameObject baseCampSpawner = Instantiate(_baseCampSpawnerPrefab);
                NetworkServer.Spawn(baseCampSpawner);
            }
            // 인터랙션 매니져 프리팹 스폰
            if (_interactionManagerPrefab != null)
            {
                GameObject interactionManager = Instantiate(_interactionManagerPrefab);
                NetworkServer.Spawn(interactionManager);
            }
            // 입력 핸들러 프리팹 스폰
            if (_inputHandlerPrefab != null)
            {
                GameObject inputHandler = Instantiate(_inputHandlerPrefab);
                NetworkServer.Spawn(inputHandler);
            }
        }

        if (sceneName == _gameSceneName && _isGameStarted)
        {
            Debug.Log("게임 씬으로 전환됨");
            // 씬 변경 후에는 자동으로 OnServerAddPlayer가 호출되어 게임 플레이어가 생성됩니다.
            // 매니져 프리팹 스폰
            if (_sessionManagerPrefab != null)
            {
                GameObject sessionManager = Instantiate(_sessionManagerPrefab);
                NetworkServer.Spawn(sessionManager);
            }

            // 호스트만 스포너 네트워크 스폰

            if (_objectSpawnerPrefab != null)
            {
                GameObject objectSpawner = Instantiate(_objectSpawnerPrefab);
                NetworkServer.Spawn(objectSpawner);
            }
            if (_shipSpawnerPrefab != null)
            {
                GameObject shipSpawner = Instantiate(_shipSpawnerPrefab);
                NetworkServer.Spawn(shipSpawner);
            }
            if (_backgroundSpawnerPrefab != null)
            {
                GameObject backgroundSpawner = Instantiate(_backgroundSpawnerPrefab);
                NetworkServer.Spawn(backgroundSpawner);
            }
            if (_dummyObjectSpawnerPrefab != null)
            {
                GameObject dummyObjectSpawner = Instantiate(_dummyObjectSpawnerPrefab);
                NetworkServer.Spawn(dummyObjectSpawner);
            }
        }
    }

    /// <summary>
    /// 서버에서 클라이언트 연결 시
    /// </summary>
    public override void OnServerConnect(NetworkConnectionToClient conn)
    {
        base.OnServerConnect(conn);
        OnClientConnectEvent?.Invoke(conn);
        Debug.Log($"서버: 클라이언트 연결 - {conn.connectionId}");
    }

    /// <summary>
    /// 서버에서 클라이언트 연결 해제 시
    /// </summary>
    public override void OnServerDisconnect(NetworkConnectionToClient conn)
    {
        base.OnServerDisconnect(conn);
        OnClientDisconnectEvent?.Invoke(conn);
        Debug.Log($"서버: 클라이언트 연결 해제 - {conn.connectionId}");
    }

    /// <summary>
    /// 클라이언트 시작 시 (호스트 포함)
    /// </summary>
    public override void OnStartClient()
    {
        base.OnStartClient();
        // 포탄 프리팹은 클라이언트에서도 풀에서 꺼내고 반납하도록 스폰 핸들러 교체
        BulletPool.RegisterClientHandlers(spawnPrefabs);
    }

    /// <summary>
    /// 클라이언트 연결 시
    /// </summary>
    public override void OnClientConnect()
    {
        base.OnClientConnect();
        Debug.Log("클라이언트: 서버에 연결됨");
        // 클라이언트 연결 이벤트는 서버에서만 처리 (OnServerConnect에서)
    }

    /// <summary>
    /// 클라이언트 연결 해제 시
    /// </summary>
    public override void OnClientDisconnect()
    {
        base.OnClientDisconnect();
        Debug.Log("클라이언트: 서버와 연결 해제됨");
        // 클라이언트 연결 해제 이벤트는 서버에서만 처리 (OnServerDisconnect에서)
    }
    #endregion

    #region Player Management

    /// <summary>
    /// 호스트 중단 시 게임 상태 리셋
    /// </summary>
    public override void OnStopHost()
    {
        base.OnStopHost();
        ResetGameState();
        Debug.Log("호스트 중단 - 게임 상태 리셋");
    }

    /// <summary>
    /// 클라이언트 중단 시 게임 상태 리셋
    /// </summary>
    public override void OnStopClient()
    {
        base.OnStopClient();
        ResetGameState();
        Debug.Log("클라이언트 중단 - 게임 상태 리셋");
    }

    #endregion

    #region Public API

    /// <summary>
    /// 게임 시작 상태 확인
    /// </summary>
    public bool IsGameStarted => _isGameStarted;

    /// <summary>
    /// 게임 상태 리셋
    /// </summary>
    public void ResetGameState()
    {
        _isGameStarted = false;
        Debug.Log("게임 상태 리셋됨");
    }

    /// <summary>
    /// 게임 플레이어 프리팹 설정 (런타임에서 변경 가능)
    /// </summary>
    public void SetGamePlayerPrefab(GameObject prefab)
    {
        _gamePlayerPrefab = prefab;
    }

    /// <summary>
    /// 게임 씬 이름 설정 (런타임에서 변경 가능)
    /// </summary>
    public void SetGameSceneName(string sceneName)
    {
        _gameSceneName = sceneName;
    }

    #endregion

    /// <summary>
    /// NetId로 네트워크 오브젝트의 컴포넌트를 가져오는 제네릭 함수
    /// </summary>
    /// <typeparam name="T">가져올 컴포넌트 타입</typeparam>
    /// <param name="netId">네트워크 ID</param>
    /// <returns>컴포넌트 또는 null</returns>
    public static T GetNetworkComponent<T>(uint netId) where T : Component
    {
        if (netId == 0) return null;

        // 서버인 경우
        if (NetworkServer.active && NetworkServer.spawned.TryGetValue(netId, out NetworkIdentity serverIdentity))
        {
            return serverIdentity.TryGetComponent<T>(out var component) ? component : null;
        }

        // 클라이언트인 경우
        if (NetworkClient.spawned.TryGetValue(netId, out NetworkIdentity clientIdentity))
        {
            return clientIdentity.TryGetComponent<T>(out var component) ? component : null;
        }

        return null;
    }
}