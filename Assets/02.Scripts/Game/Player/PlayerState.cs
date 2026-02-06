using UnityEngine;
using Mirror;
using UnityEngine.UI;
using TMPro;
using System;
/// <summary>
/// 플레이어의 상태를 관리하는 클래스 - 네트워크 동기화
/// [박상민 - 2025.06.12]
/// </summary>
[RequireComponent(typeof(Collider2D), typeof(LTDPlayer))]
public class PlayerState : NetworkBehaviour
{
    [Header("플레이어 설정")]
    [Tooltip("플레이어 기절 지속시간")]
    [SerializeField] private float _stunDuration = 2f;
    [SerializeField] private float _doubleStunIgnoreDuration = 0.1f;
    [Tooltip("기절 후 사망까지 무적시간")]
    [SerializeField] private float _invincibleTimeAfterStun = 0.2f;
    [Tooltip("부활에 걸리는 시간")]
    [SerializeField] private float _reviveTime = 3f;
    [Tooltip("리스폰에 걸리는 시간")]
    [SerializeField] private float _respawnTime = 5f;

    [Tooltip("리스폰 위치 - 나중에 스폰 포인트로 변경")]
    [SerializeField] private Vector2 _respawnPosition => ShipSpawner.Instance.CurrentShip.transform.position;
    [Tooltip("리스폰 중인 플레이어의 UI")]
    [SerializeField] private Image _reviveProgressUI = null; // 리스폰 중인 플레이어의 UI
    [SerializeField] private TMP_Text _respawnText = null;
    [Tooltip("스프라이트")]
    [SerializeField] private SpriteRenderer _playerSpriteRenderer = null; // 기절 상태일 때 사용할 스프라이트

    [Header("플레이어 상태")]
    [Tooltip("플레이어의 현재 상태")]
    [SerializeField]
    [SyncVar(hook = nameof(OnStateChanged))]
    private PlayerStateType _playerState = PlayerStateType.일반;
    [Tooltip("플레이어가 다른 플레이어게 들렸는지")]
    [SerializeField]
    private bool _isLTDByOtherPlayer = false;

    private PlayerController _playerController;

    public bool IsRevivable => _playerState == PlayerStateType.사망 && !_isReviving;
    public bool IsReviving => _isReviving;
    public bool IsRespawnable => _playerState == PlayerStateType.사망 && _respawnTimer >= _respawnTime;
    public bool IsControllable => _playerState != PlayerStateType.사망 && _playerState != PlayerStateType.기절;
    public bool IsStunned => _playerState == PlayerStateType.기절;
    public bool IsDead => _playerState == PlayerStateType.사망;
    public bool IsBeingLifted => _isLTDByOtherPlayer;
    [SerializeField]
    private bool _isReviving = false;
    [SerializeField]
    [SyncVar(hook = nameof(OnReviveTimerChanged))] private float _reviveTimer = 0f;
    [SyncVar(hook = nameof(OnRespawnTimerChanged))] private float _respawnTimer = 0f;
    private float _stunTimer = 0f;
    private float _stunIgnoreTimer = 0;
    private bool _doubleStunIgnore = false;

    // 이벤트
    public event Action OnStunned;

    private void Awake()
    {
        _playerController = GetComponent<PlayerController>();
        _reviveProgressUI.fillAmount = 0f; // 초기화
        _respawnText.text = ""; // 초기화
    }

    void Start()
    {
        if (isServer)
        {
            // 초기 상태 설정
            ResetState();
        }
    }

    void Update()
    {
        if (!isServer)
        {
            return; // 서버에서만 상태 업데이트 처리
        }

        // if (Input.GetKeyDown(KeyCode.V))
        // {
        //     CmdTestStun(); // 테스트용 - V키를 누르면 기절 상태로 전환
        // }

        // if (IsDead)
        // {
        //     // 처음에만 리스폰 UI 활성화
        //     if (_respawnTimer == 0f)
        //     {
        //         RpcToggleRespawnUI(connectionToClient, true);
        //     }
        //     _respawnTimer += Time.deltaTime;
        //     _respawnTimer = Mathf.Clamp(_respawnTimer, 0f, _respawnTime); // 리스폰 타이머 클램프
        // }
        // else
        // {
        //     // 처음에만 리스폰 UI 비활성화
        //     if (_respawnTimer > 0f)
        //     {
        //         RpcToggleRespawnUI(connectionToClient, false);
        //     }
        //     _respawnTimer = 0f; // 리스폰 타이머 초기화
        // }

        // 다른 플레이어가 살려주는 경우
        if (IsReviving)
        {
            _reviveTimer += Time.deltaTime;
            // 부활 시간이 다 되면 상태를 일반으로 변경 (내 캐릭터일 경우에만)
            if (_reviveTimer >= _reviveTime)
            {
                ResetState(); //제자리 스폰
                GetComponent<LTDPlayer>().Reset(); // LTD 오브젝트 초기화
                _reviveTimer = 0f;
            }
        }

        // 스턴 타이머
        if (IsStunned)
        {
            if (_isLTDByOtherPlayer)
            {
                // 다른 플레이어에게 들려있는 상태에서는 스턴 타이머를 초기화하지 않음
                return;
            }
            _stunTimer += Time.deltaTime;
            // 스턴 효과 재생

            // 스턴 지속시간이 지나면 일반 상태로 변경 (내 캐릭터인 경우에만)
            if (_stunTimer >= _stunDuration)
            {
                SetState(PlayerStateType.일반);
            }
        }
        else
        {
            _stunTimer = 0f; // 스턴 타이머 초기화
        }

        // 스턴 무시 타이머
        if (_doubleStunIgnore)
        {
            _stunIgnoreTimer += Time.deltaTime;
            // 스턴 효과 재생

            if (_stunIgnoreTimer >= _doubleStunIgnoreDuration)
            {
                _doubleStunIgnore = false;
            }
        }
        else
        {
            _stunIgnoreTimer = 0f; // 스턴 타이머 초기화
        }
    }
    /// <summary>
    /// 리스폰 위치로 이동하는 RPC
    /// </summary>
    public void MoveToRespawnPosition(NetworkConnection target)
    {
        // 플레이어의 위치를 스폰 지점으로 이동
        transform.position = _respawnPosition;
        // 회전값 배의 Rotation 방향으로 초기화
        transform.rotation = Quaternion.identity;
    }

    /// <summary>
    /// [SERVER-side] 플레이어 상태를 지정해주는 메서드
    /// 기존에 있던 상태 설정 커멘드 메서드를 확장성을 위해 서버 메서드로 변경함.
    /// 커맨드 필요 시 각 상태 변경마다 커맨드를 추가해주는 식으로 수정/추가 하면 될 듯
    /// [조우석 - 2025.07.03]
    /// </summary>
    /// <param name="newState"></param>
    [Server]
    private void SetState(PlayerStateType newState)
    {
        if (_playerState == newState)
        {
            return;
        }

        _playerState = newState;

        switch (newState)
        {
            case PlayerStateType.기절:
                _stunTimer = 0f;
                _doubleStunIgnore = true; // 투터치 방지 플래그 설정
                GetComponent<PlayerCombatController>().ResetAttackState(); // 공격 상태 초기화
                GetComponent<PlayerModuleController>().CallExitModule();
                ToggleLiftable(true);
                break;
            case PlayerStateType.사망:
                ToggleLiftable(true);
                break;
            case PlayerStateType.일반:
                _stunTimer = 0f; // 스턴 타이머 초기화
                ToggleLiftable(false);
                break;
        }
    }

    /// <summary>
    /// [SERVER-side] 플레이어 스턴 적용
    /// [조우석 - 2025.07.03]
    /// </summary>
    [Server]
    public void ApplyStun()
    {
        if (_doubleStunIgnore)
        {
            Debug.Log("<color=yellow>[투터치 방지 스턴 무시]</color>: 아주 최근에 스턴된 적이 있음");
            return;
        }

        switch (_playerState)
        {
            case PlayerStateType.일반:
                SetState(PlayerStateType.기절); // 일반 상태에서 기절로 변경
                break;
            case PlayerStateType.기절:
                SetState(PlayerStateType.사망); // 기절 상태에서 사망으로 변경
                break;
            case PlayerStateType.사망:
                break;
        }

        OnStunned?.Invoke();
    }

    /// <summary>
    /// 플레이어 상태를 초기화하는 함수 - 포지션 스폰 지점으로 이동해야됨
    /// </summary>
    [Server]
    private void ResetState()
    {
        if (_isLTDByOtherPlayer)
        {
            LTDPlayer ltdPlayer = GetComponent<LTDPlayer>();
            ltdPlayer.LiftingPlayer.CallResetLTDObject(); // 다른 플레이어에게 들려있는 상태라면 내려놓기
        }
        SetState(PlayerStateType.일반); // 상태를 일반으로 초기화
        _isLTDByOtherPlayer = false;
        _isReviving = false;
    }

    /// <summary>
    /// 다른 플레이어에게 들렸을 때, 내려졌을때 상태변경
    /// </summary>
    public void CarriedByOtherPlayer(bool isCarried)
    {
        _isLTDByOtherPlayer = isCarried;
    }

    /// <summary>
    /// 리스폰 시작 명령
    /// </summary>
    public void CallRespawn()
    {
        if (_isLTDByOtherPlayer)
        {
            LTDPlayer ltdPlayer = GetComponent<LTDPlayer>();
            ltdPlayer.LiftingPlayer.CallResetLTDObject(); // 다른 플레이어에게 들려있는 상태라면 내려놓기
        }
        SetState(PlayerStateType.일반); // 상태를 일반으로 초기화
        _isLTDByOtherPlayer = false;
        _isReviving = false;
        // 호출한 플레이어에게 리스폰 위치로 이동 명령
        MoveToRespawnPosition(connectionToClient);
    }

    /// <summary>
    /// 부활 시작 명령 - 다른플레이어가 외부에서 호출
    /// </summary>
    public void StartRevive()
    {
        if (IsRevivable)
        {
            _isReviving = true;
        }
    }

    /// <summary>
    /// 부활 중지 명령 - 다른플레이어가 외부에서 호출
    /// </summary>
    public void StopRevive()
    {
        if (_isReviving)
        {
            _isReviving = false;
        }
    }

    /// <summary>
    /// 플레이어의 상태를 들수 있는 LTD 오브젝트로 변경
    /// </summary>
    private void ToggleLiftable(bool isLiftable)
    {
        GetComponent<LTDPlayer>().enabled = isLiftable; // LTD 오브젝트의 상호작용을 활성화/비활성화
        //tag변경
        gameObject.tag = isLiftable ? "LTDObject" : _playerController.PlayerTag; // 스턴 상태에서는 플레이어 태그를 제거
        GetComponent<PlayerPhysicsHandler>().SetIsLTDObject(isLiftable); // 물리 핸들러의 LTD 오브젝트 상태 변경
    }

    #region SyncVar Hook함수
    private void OnStateChanged(PlayerStateType oldState, PlayerStateType newState)
    {
        // 상태가 변경되었을 때 처리할 로직
        Debug.Log($"플레이어 상태가 {oldState}에서 {newState}로 변경되었습니다.");
        // 상태에 따라 스프라이트 변경 (임시 가시성)
        switch (newState)
        {
            case PlayerStateType.기절:
                _playerSpriteRenderer.color = Color.yellow; // 기절 상태 스프라이트로 변경
                break;
            case PlayerStateType.사망:
                _playerSpriteRenderer.color = Color.red; // 사망 상태 스프라이트로 변경
                break;
            case PlayerStateType.일반:
                _playerSpriteRenderer.color = Color.white; // 일반 상태 스프라이트로 변경
                break;
        }
    }

    private void OnReviveTimerChanged(float oldTimer, float newTimer)
    {
        if (_reviveProgressUI != null)
        {
            _reviveProgressUI.color = Color.green;
            _reviveProgressUI.fillAmount = Mathf.Clamp01(newTimer / _reviveTime);
        }// 플레이어 부활 상태 모든 플레이어에게 표시
    }

    private void OnRespawnTimerChanged(float oldTimer, float newTimer)
    {
        if (_respawnText != null)
        {
            int respawnTime = Mathf.CeilToInt(_respawnTime - newTimer);
            _respawnText.text = respawnTime > 0 ? respawnTime.ToString() : "F";
        } // 리스폰 타이머 UI 업데이트
    }

    [TargetRpc]
    private void RpcToggleRespawnUI(NetworkConnection target, bool isActive)
    {
        if (_respawnText != null)
        {
            _respawnText.gameObject.SetActive(isActive);
        }
    }

    #endregion
}