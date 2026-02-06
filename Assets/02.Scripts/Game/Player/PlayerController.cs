using UnityEngine;
using Mirror;
using TMPro;
using Cinemachine;
using Steamworks;
using UnityEngine.SceneManagement;
using System;
/// <summary>
/// 네트워크 플레이어 컨트롤러 (게임 씬용)
/// [박상민 - 2025.06.10]
/// </summary>
[RequireComponent(typeof(NetworkIdentity))]
public class PlayerController : NetworkBehaviour
{
    [Header("이동 설정")]
    [Tooltip("이동 속도")]
    [SerializeField] private float _moveSpeed = 5f;

    [Header("Transform 참조")]
    [Tooltip("마우스 포인트 에임 Rotation의 중심이 될 피벗")]
    [SerializeField] private Transform _rotationPivot;
    [SerializeField] private Transform _headTransform;

    [Header("컴포넌트 참조")]
    [Tooltip("애니메이터")]
    [SerializeField] private Animator _animator;
    [SerializeField] private Animator _overlayAnimator;
    [Tooltip("이름 표시 텍스트")]
    [SerializeField] private TMP_Text _playerNameText;
    [Tooltip("플레이어 스프라이트")]
    [SerializeField] private SpriteRenderer _playerSpriteRenderer;
    [SerializeField] private SpriteRenderer _playerOverlaySpriteRenderer;
    [SerializeField] private SpriteRenderer _innerMapSpriteRenderer;
    private Rigidbody2D _playerRigidbody;
    [SerializeField] private Material _defaultMaterial;
    [SerializeField] private Material _unitMaterial;
    [SerializeField] private FOVStencil _fovStencil;

    // @@@@@@@@@@@@@@ 현재 테스트를 위해서 활성화. 추후 제거 필요
    [Header("카메라")]
    [Tooltip("시네머신 가상 카메라")]
    [SerializeField] private CinemachineVirtualCamera _camera;

    [Header("레이어 및 태그")]
    [Tooltip("플레이어 콜라이더의 충돌 무시를 위한 레이어")]
    [SerializeField] private string _playerLayerName;
    [Tooltip("자기 자신 콜라이더를 판단하기위한 태그")]
    [SerializeField] private string _playerTag;

    // 네트워크 동기화 변수들
    [SyncVar(hook = nameof(OnPlayerIdChanged))]
    private uint _playerId;

    [SyncVar(hook = nameof(OnDisplayNameChanged))]
    private string _playerName = "";

    [SyncVar(hook = nameof(OnSteamIdChanged))]
    private ulong _steamId;

    private PlayerState _playerState;
    public Vector2 LastFaceVector => _aimInputVector.normalized;
    private bool _controlsEnabled => _playerState.IsControllable;

    // 로컬 변수들
    private Vector2 _moveDirection;
    private GameObject _map = null; // 임시 맵 오브젝트
    private bool _isInsideBuilding = false;

    // 입력 관련
    private Vector2 _inputVector;
    [SyncVar(hook = nameof(OnFlipXChanged))]
    private bool _flipX = false;
    private Vector2 _aimInputVector = Vector2.right;
    private bool _localControlEnabled = false;

    // 프로퍼티들
    public uint PlayerId => _playerId;
    public string PlayerName => _playerName;
    public CSteamID SteamId => new CSteamID(_steamId);
    public bool ControlsEnabled => _controlsEnabled;
    public PlayerState PlayerState => _playerState;
    public string PlayerTag { get { return _playerTag; } }
    public CinemachineVirtualCamera PlayerCamera => _camera;
    public Transform HeadTransform => _headTransform;

    // 외부에서 구독할 수 있는 이벤트를 제공합니다.
    // 하지만 이 이벤트의 실제 관리는 stunController에게 위임합니다.
    public event Action OnStunned
    {
        add { _playerState.OnStunned += value; }
        remove { _playerState.OnStunned -= value; }
    }

    #region Unity Lifecycle

    private void Awake()
    {
        // 컴포넌트 자동 할당
        if (_animator == null)
        {
            _animator = GetComponentInChildren<Animator>();
        }
        if (_playerRigidbody == null)
        {
            _playerRigidbody = GetComponentInChildren<Rigidbody2D>();
        }
        if (_playerSpriteRenderer == null)
        {
            _playerSpriteRenderer = transform.Find("PlayerSprite").GetComponentInChildren<SpriteRenderer>();
        }
        if (_playerState == null)
        {
            _playerState = GetComponent<PlayerState>();
        }

        DontDestroyOnLoad(gameObject); // 플레이어는 직접 파괴하지 않는이상 씬 전환시에도 유지
        // 씬 로드 이벤트 구독
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void Start()
    {
        gameObject.layer = LayerMask.NameToLayer(_playerLayerName);

        // 로컬 플레이어 오브젝트면 인풋 연결
        if (isLocalPlayer)
        {
            ClientInputHandler.Instance.Setup(this);
            _playerSpriteRenderer.material = _defaultMaterial; // 로컬 플레이어는 기본 머티리얼 사용

            _innerMapSpriteRenderer.gameObject.SetActive(true);

            // @@@@@@@@@@@@@@@@@@ 테스트를 위해서 카메라 활성화
            if (_camera != null)
            {
                _camera.gameObject.SetActive(true);
                //CameraManager.Instance.SetPlayerCinemachine(_camera);
                //CameraManager.Instance.SetLocalPlayerTransform(transform);
            }
            // 초기에는 컨트롤 비활성화 (GameManager에서 활성화)
            EnableControls(true);
        }
        else
        {
            _playerSpriteRenderer.material = _unitMaterial; // 다른 플레이어는 유닛 머티리얼 사용

            _innerMapSpriteRenderer.gameObject.SetActive(false);

            // @@@@@@@@@@@@ 테스트를 위해서 다른 플레이어의 카메라는 비활성화
            if (_camera != null)
            {
                _camera.gameObject.SetActive(false);
            }
            // 다른 플레이어는 컨트롤 비활성화
            EnableControls(false);
        }
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!isServer)
        {
            return;
        }
        if (scene.name == GameNetworkManager.singleton.BaseCampSceneName)
        {
            // 베이스캠프 씬 진입 시 위치 설정
            Debug.Log("베이스캠프 씬 진입, 플레이어 위치 설정");
            transform.position = BaseCampManager.Instance.BaseCampSpawnPosition;
        }
        else if (scene.name == GameNetworkManager.singleton.GameSceneName)
        {
            // 게임 씬 진입 시 위치 설정
            Debug.Log("게임 씬 진입, 플레이어 위치 설정");
            transform.position = BaseCampManager.Instance.OriginSpawnPosition;
        }
        ClientInputHandler.Instance.RpcEnablePlayerMode(connectionToClient);
        RpcSetCameraFOV(connectionToClient, GetComponent<PlayerModuleController>().NormalFOV);
        RpcSetCameraPos(connectionToClient, 0f);
    }

    #endregion

    #region Network Lifecycle

    public override void OnStartClient()
    {
        base.OnStartClient();

        // 클라이언트에서 UI 업데이트
        UpdatePlayerDisplay();
    }

    public override void OnStartLocalPlayer()
    {
        base.OnStartLocalPlayer();

        Debug.Log("로컬 플레이어 시작 (게임)");

        // 로컬 플레이어 정보를 서버에 전송
        CSteamID mySteamId = SteamUser.GetSteamID();
        string myName = SteamFriends.GetPersonaName();

        CmdSetPlayerInfo(mySteamId.m_SteamID, myName);

        if (isLocalPlayer)
        {
            // 초기에는 컨트롤 비활성화 (GameManager에서 활성화)
            EnableControls(true);
        }
        else
        {
            // 다른 플레이어는 컨트롤 비활성화
            EnableControls(false);
        }
    }

    public override void OnStopClient()
    {
        base.OnStopClient();
    }

    // void OnDestroy()
    // {
    //     // 씬 로드 이벤트 구독 해제
    //     SceneManager.sceneLoaded -= OnSceneLoaded;

    //     if (isLocalPlayer)
    //     {
    //         ClientInputHandler.Instance.Cleanup();
    //     }
    // }

    /// <summary>
    /// 플레이어 정보를 서버에 전송
    /// </summary>
    [Command]
    private void CmdSetPlayerInfo(ulong steamId, string playerName)
    {
        _steamId = steamId;
        _playerName = playerName;
    }

    [TargetRpc]
    public void RpcSetCameraPos(NetworkConnection conn, float cameraPivotOffsetY)
    {
        _camera.GetCinemachineComponent<CinemachineTransposer>().m_FollowOffset = new Vector3(0, cameraPivotOffsetY, -10);
    }

    [TargetRpc]
    public void RpcSetCameraFOV(NetworkConnection conn, float size)
    {
        _camera.m_Lens.OrthographicSize = size;
    }

    #endregion

    #region SyncVar Hooks

    /// <summary>
    /// 플레이어 ID 변경 시 호출
    /// </summary>
    private void OnPlayerIdChanged(uint oldId, uint newId)
    {
        Debug.Log($"플레이어 ID 변경: {oldId} → {newId}");
    }

    /// <summary>
    /// 플레이어 이름 변경 시 호출
    /// </summary>
    private void OnDisplayNameChanged(string oldName, string newName)
    {
        UpdatePlayerDisplay();
    }

    /// <summary>
    /// Steam ID 변경 시 호출
    /// </summary>
    private void OnSteamIdChanged(ulong oldId, ulong newId)
    {
        UpdatePlayerDisplay();
    }

    #endregion

    #region Input & Movement

    /// <summary>
    /// 이동 처리 (Transform 직접 이동)
    /// [수정: 조우석 - 25.06.11]
    /// </summary>
    public void HandleMovement(Vector2 inputVector)
    {
        if (!isServer)
        {
            _moveDirection = Vector2.zero;
            return;
        }

        if (!_controlsEnabled)
        {
            _moveDirection = Vector2.zero;
            _animator.SetBool("Moving", false);
            _overlayAnimator.SetBool("Moving", false);
            return;
        }

        // 이동 방향 계산
        _inputVector = transform.up * inputVector.y + transform.right * inputVector.x;
        _moveDirection = _inputVector * _moveSpeed;

        // 이동 처리, Rigidbody에 적용
        //_interRigidbody.ApplyNonPhysicsVector(_moveDirection);
        _playerRigidbody.velocity = _moveDirection / _playerRigidbody.mass;

        // 애니메이션 처리
        HandleAnimation(inputVector);
    }

    /// <summary>
    /// 애니메이션 처리
    /// </summary>
    private void HandleAnimation(Vector2 inputVector)
    {
        if (inputVector.x > 0)
        {
            RpcFlipX(false); // 오른쪽 이동 시 스프라이트를 반전 해제
        }
        else if (inputVector.x < 0)
        {
            RpcFlipX(true); // 왼쪽 이동 시 스프라이트를 반전
        }

        // 플레이어 애니메이션 설정
        if (_inputVector.magnitude > 0.1f)
        {
            _animator.SetBool("Moving", true);
            _overlayAnimator.SetBool("Moving", true);
        }
        else
        {
            _animator.SetBool("Moving", false);
            _overlayAnimator.SetBool("Moving", false);
        }

        // 플레이어의 에임 피벗을 회전하는 메서드
        RotatateHandPivot();
    }

    private void RotatateHandPivot()
    {
        float angle = Mathf.Atan2(_aimInputVector.y, _aimInputVector.x) * Mathf.Rad2Deg;
        _rotationPivot.rotation = Quaternion.Euler(0f, 0f, angle);
    }

    [ClientRpc]
    private void RpcFlipX(bool flipX)
    {
        _playerSpriteRenderer.flipX = flipX;
        _playerOverlaySpriteRenderer.flipX = flipX;
    }
    /// <summary>
    /// 스프라이트 좌우 반전 처리
    /// - 추후 애니메이션 스크립트를 따로 관리할 때 정리
    /// [조우석 - 25.06.12]
    /// </summary>
    private void OnFlipXChanged(bool oldFlipX, bool newFlipX)
    {
        _playerSpriteRenderer.flipX = newFlipX;
        _playerOverlaySpriteRenderer.flipX = newFlipX;
    }

    /// <summary>
    /// 마우스의 위치값을 받아 플레이어의 회전이 중심이 되는 위치에서 마우스 포인트를 향하는 에임 벡터를 설정하는 메서드
    /// [조우석 - 25.06.12]
    /// </summary>
    /// <param name="inputVector"></param>
    public void SetMouseAimVector(Vector2 mouseInputVector)
    {
        _aimInputVector = (mouseInputVector - (Vector2)_rotationPivot.position).normalized;
    }

    /// <summary>
    /// 패드의 스틱 입력을 받아 에임 벡터를 설정하는 메서드
    /// [조우석 - 25.06.12]
    /// </summary>
    /// <param name="padInputVector"></param>
    public void SetLastMoveVector(Vector2 padInputVector)
    {
        // 패드 스틱의 입력이 없으면 에임 벡터를 설정하지 않게 하여,
        // 에임 벡터가 마지막으로 바라본 에임을 가리키도록 한다.
        // 플레이어가 월드 좌표 기준으로는 회전해 있으므로 해당 회전 값도 반영하여 플레이어 기준으로 맞춘다
        if (padInputVector.magnitude > 0.05f)
        {
            // padInputVector를 플레이어의 회전값에 맞춰 회전시킨다
            _aimInputVector = Quaternion.Euler(0, 0, transform.eulerAngles.z) * padInputVector.normalized;
        }
    }
    /// <summary>
    /// 임시 맵 토글 메서드
    /// </summary>
    public void ToggleMap()
    {
        if (_isInsideBuilding)
        {
            _map = UIManager.Instance.InnerMapUI.gameObject;
            UIManager.Instance.MapUI.gameObject.SetActive(false);
        }
        else
        {
            _map = UIManager.Instance.MapUI.gameObject;
            UIManager.Instance.InnerMapUI.gameObject.SetActive(false);
        }
        _map.SetActive(!_map.activeSelf);
    }

    #endregion

    #region Player Setup

    /// <summary>
    /// 플레이어 초기 설정
    /// </summary>
    /// <param name="playerId">플레이어 ID</param>
    public void SetupPlayer(int playerId)
    {
        _playerId = (uint)playerId;
        Debug.Log($"플레이어 설정 완료: ID {_playerId}");
    }

    /// <summary>
    /// 플레이어 표시 정보 업데이트
    /// </summary>
    private void UpdatePlayerDisplay()
    {
        if (_playerNameText != null)
        {
            string displayName = string.IsNullOrEmpty(_playerName) ? $"Player {_playerId}" : _playerName;
            _playerNameText.text = displayName;
        }
    }

    /// <summary>
    /// 컨트롤 활성화/비활성화
    /// </summary>
    /// <param name="enabled">활성화 여부</param>
    public void EnableControls(bool enabled)
    {
        _localControlEnabled = enabled;

        if (!enabled)
        {
            _inputVector = Vector2.zero;
            _moveDirection = Vector2.zero;
        }

        Debug.Log($"플레이어 {_playerId} 컨트롤 {(enabled ? "활성화" : "비활성화")}");
    }
    [TargetRpc]
    public void RpcEnterBuilding(NetworkConnection target)
    {
        _isInsideBuilding = true;
    }
    [TargetRpc]
    public void RpcExitBuilding(NetworkConnection target)
    {
        _isInsideBuilding = false;
    }
    #endregion

    #region Utility
    /// <summary>
    /// 플레이어가 준비되었는지 확인
    /// </summary>
    public bool IsPlayerReady()
    {
        return !string.IsNullOrEmpty(_playerName) && _steamId != 0;
    }

    /// <summary>
    /// 플레이어가 진입 가능한 오브젝트 내부로 들어갔을 때 호출
    /// </summary>
    private void OnTriggerEnter2D(Collider2D collision)
    {
        // if (!isServer) return;
        // if (collision.tag == "Enterable")
        // {
        //     transform.SetParent(collision.transform);
        //     GetComponent<NetworkParentSync>()?.SetNetworkParent(collision.transform);
        // }
    }
    /// <summary>
    /// 플레이어가 진입 가능한 오브젝트 내부에서 나갔을 때 호출
    /// </summary>
    private void OnTriggerExit2D(Collider2D collision)
    {
        // if (!isServer) return;
        // if (collision.tag == "Enterable")
        // {
        //     transform.SetParent(null);
        //     GetComponent<NetworkParentSync>()?.SetNetworkParent(null);
        // }
    }

    #endregion

    public void AddMass(float m)
    {
        _playerRigidbody.mass += m;
    }

    public void UpdateFOV(float radius)
    {
        _fovStencil.SetViewRadius(radius);
    }

    public void ResetFOV()
    {
        _fovStencil.ResetViewRadius();
    }
}