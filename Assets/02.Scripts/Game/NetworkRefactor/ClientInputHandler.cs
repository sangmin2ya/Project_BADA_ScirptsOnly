using Mirror;
using Mirror.Examples.Basic;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Users;

/// <summary>
/// 클라이언트 측 인풋 관리 클래스
/// [조우석 - 2025.09.07.]
/// [김도형 - 2025.11.06]
/// </summary>
public class ClientInputHandler : NetworkSingleton<ClientInputHandler>
{
    [SerializeField] private UnityEngine.InputSystem.PlayerInput _playerInput;
    private bool _isSetup = false;
    private uint _clientId = 0;
    private PlayerController _clientController;

    // 플레이어 모드 액션
    private InputAction _playerMoveAction;
    private InputAction _playerAimAction;
    private InputAction _playerUseAction;
    private InputAction _playerThrowAction;
    private InputAction _playerInteractAction;
    private InputAction _playerLTDAction;

    // 장비 공통 액션
    private InputAction _moduleAimAction;
    private InputAction _moduleAbility1Action;
    private InputAction _moduleAbility2Action;
    private InputAction _moduleExitAction;

    // 공통 조작 액션
    private InputActionMap _globalActionMap;
    private InputAction _globalPauseAction;
    private InputAction _globalMapAction;

    // 지속적인 입력 상태 추적
    private bool _isAbility1Active = false;
    private bool _isAbility2Active = false;
    private Vector2 _aimInput = Vector2.zero;
    private Vector2 _aimUIPosition = Vector2.zero;
    private bool _isUsingGamepad = false;

    // 마우스 입력 벡터
    private Vector2 _mouseScreenPos;
    private Vector3 _mouseWorldPos;

    [SerializeField] private float _sendInterval = 1.0f / 20.0f; // 1초에 20번 전송 (20Hz)
    private float _syncTimer;

    #region Monobehavior

    protected override void Awake()
    {
        base.Awake();
        DontDestroyOnLoad(gameObject);  // 씬 전환 시에도 유지
    }

    private void OnEnable()
    {
        // 플레이어 모드 활성화
        EnablePlayerMode();
    }

    private void OnDestroy()
    {
        InputUser.onChange -= OnInputDeviceChanged;
        _playerUseAction.performed -= OnUse;
        _playerUseAction.canceled -= OnUse;
        _playerThrowAction.performed -= OnThrow;
        _moduleExitAction.performed -= OnExit;
        _playerInteractAction.performed -= OnInteract;
        _playerInteractAction.canceled -= OnInteract;

        _moduleAbility1Action.performed -= OnAbility1Action;
        _moduleAbility1Action.canceled -= OnAbility1Action;

        _moduleAbility2Action.performed -= OnAbility2Action;
        _moduleAbility2Action.canceled -= OnAbility2Action;

        if (_globalPauseAction != null)
        {
            _globalPauseAction.performed -= OnPause;
        }
        if (_globalMapAction != null)
        {
            _globalMapAction.performed -= OnMap;
        }
    }

    private void Update()
    {
        // Setup 하기 전까지는 동작하지 않음
        if (!_isSetup)
        {
            return;
        }

        // 클라이언트의 지속적인 인풋 데이터 값 생성
        PlayerInputState currentInputState = new PlayerInputState
        {
            moveInput = _playerMoveAction.ReadValue<Vector2>(),
            isAbility1Pressed = _isAbility1Active,
            isAbility2Pressed = _isAbility2Active,
            isUsingGamepad = _isUsingGamepad
        };

        // "Module" Action Map
        if (_playerInput.currentActionMap.name == "Module")
        {
            currentInputState.isModuleMode = true;
            currentInputState.aimInput = _moduleAimAction.ReadValue<Vector2>();
            if (!_isUsingGamepad)
            {
                _mouseScreenPos = Mouse.current.position.ReadValue();
                _mouseWorldPos = Camera.main.ScreenToWorldPoint(new Vector3(_mouseScreenPos.x, _mouseScreenPos.y, Camera.main.nearClipPlane));
                currentInputState.mouseWorldPos = _mouseWorldPos;
            }
        }
        else // "Player" Action Map
        {
            currentInputState.isModuleMode = false;
            currentInputState.aimInput = _playerAimAction.ReadValue<Vector2>();
            if (!_isUsingGamepad)
            {
                _mouseScreenPos = Mouse.current.position.ReadValue();
                _mouseWorldPos = Camera.main.ScreenToWorldPoint(new Vector3(_mouseScreenPos.x, _mouseScreenPos.y, Camera.main.nearClipPlane));
                currentInputState.mouseWorldPos = _mouseWorldPos;
            }
        }

        // 주기적으로 서버측으로 클라이언트의 인풋 데이터 값을 전달
        if (_sendInterval > 0)
        {
            _syncTimer += Time.deltaTime;
            if (_syncTimer >= _sendInterval)
            {
                _syncTimer = 0f;
                ServerInputHandler.Instance.CmdSendInputState(_clientId, currentInputState);
            }
        }
        // 주기 0으로 지정 시 매 프레임마다 데이터를 전달
        else if (_sendInterval <= 0)
        {
            ServerInputHandler.Instance.CmdSendInputState(_clientId, currentInputState);
        }

    }
    #endregion

    #region 초기화 메서드

    /// <summary>
    /// 인풋 핸들러의 이벤트와 연결될 플레이어를 셋업하는 메서드
    /// - 로컬 플레이어 생성 시 호출
    /// </summary>
    /// <param name="controller">인풋과 연결할 플레이어 오브젝트</param>
    public void Setup(PlayerController controller)
    {
        if (_playerInput == null) _playerInput = GetComponent<UnityEngine.InputSystem.PlayerInput>();

        _globalActionMap = _playerInput.actions.FindActionMap("Global");
        if (_globalActionMap != null)
        {
            _globalActionMap.Enable();
        }
        else
        {
            Debug.LogError("Global Action Map을 찾을 수 없습니다!");
        }

        // 액션 참조 가져오기
        _playerMoveAction = _playerInput.actions["Player/Move"];
        _playerAimAction = _playerInput.actions["Player/Aim"];
        _playerUseAction = _playerInput.actions["Player/Use"];
        _playerThrowAction = _playerInput.actions["Player/Throw"];
        _playerInteractAction = _playerInput.actions["Player/Interact"];
        _playerLTDAction = _playerInput.actions["Player/LiftDrop"];

        _moduleAimAction = _playerInput.actions["Module/Aim"];
        _moduleAbility1Action = _playerInput.actions["Module/Ability1"];
        _moduleAbility2Action = _playerInput.actions["Module/Ability2"];
        _moduleExitAction = _playerInput.actions["Module/Exit"];

        _globalPauseAction = _globalActionMap.FindAction("Pause");
        _globalMapAction = _globalActionMap.FindAction("Map");

        // 콜백 등록
        _playerLTDAction.performed += OnLTD;
        _moduleExitAction.performed += OnExit;
        _playerThrowAction.performed += OnThrow;

        // 지속적인 입력에 대한 액션 콜백
        _playerUseAction.performed += OnUse;
        _playerUseAction.canceled += OnUse;
        _playerInteractAction.performed += OnInteract;
        _playerInteractAction.canceled += OnInteract;

        _moduleAbility1Action.performed += OnAbility1Action;
        _moduleAbility1Action.canceled += OnAbility1Action;

        _moduleAbility2Action.performed += OnAbility2Action;
        _moduleAbility2Action.canceled += OnAbility2Action;

        if (_globalPauseAction != null)
        {
            _globalPauseAction.performed += OnPause;
        }
        if (_globalMapAction != null)
        {
            _globalMapAction.performed += OnMap;
        }

        InputUser.onChange += OnInputDeviceChanged;

        _clientId = controller.netId;
        _clientController = controller;
        _isSetup = true;
    }

    /// <summary>
    /// 씬이 전환되거나 클라이언트가 종료될 때 정리 작업을 수행하는 메서드
    /// - 로컬 플레이어가 파괴될 때 해당 플레이어의 데이터를 사전에서 삭제
    /// </summary>
    public void Cleanup()
    {
        _isSetup = false;
        ServerInputHandler.Instance.CmdRemoveClientController(_clientId);
    }

    #endregion

    #region 입력 콜백 메서드

    /// <summary>
    /// 플레이어 던지기 버튼 입력 시
    /// </summary>
    /// <param name="context"></param>
    private void OnLTD(InputAction.CallbackContext context)
    {
        ServerInputHandler.Instance.CmdCallLTD(_clientId);
    }

    private void OnThrow(InputAction.CallbackContext context)
    {
        ServerInputHandler.Instance.CmdCallThrow(_clientId);
    }

    private void OnUse(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            ServerInputHandler.Instance.CmdCallUse(_clientId, true);
        }
        else
        {
            ServerInputHandler.Instance.CmdCallUse(_clientId, false);
        }
    }

    /// <summary>
    /// 플레이어 상호작용 버튼 입력 시
    /// </summary>
    /// <param name="context"></param>
    private void OnInteract(InputAction.CallbackContext context)
    {
        ServerInputHandler.Instance.CmdCallInteract(_clientId, context.performed, connectionToClient);
    }

    /// <summary>
    /// 모듈 상태 플레이어 모듈 내리기 버튼 입력 시
    /// </summary>
    /// <param name="context"></param>
    private void OnExit(InputAction.CallbackContext context)
    {
        _isAbility1Active = false;
        _isAbility2Active = false;

        // 플레이어 모드 활성화
        EnablePlayerMode();

        ServerInputHandler.Instance.CmdCallExitModule(_clientId);
        Debug.Log($"<color=green>모듈 플레이어 내리기</color>");
    }

    /// <summary>
    /// 모듈 사용중인 플레이어를 상호작용 키 외의 조건에서 강제로 내리기
    /// 서버에서 실행
    /// </summary>
    public void OnForceExit()
    {
        _isAbility1Active = false;
        _isAbility2Active = false;

        // 플레이어 모드 활성화
        EnablePlayerMode();

        ServerInputHandler.Instance.CmdCallExitModule(_clientId);
        Debug.Log($"<color=green>모듈 플레이어 강제로 내리기</color>");
    }

    private void OnAbility1Action(InputAction.CallbackContext context)
    {
        _isAbility1Active = context.performed;
    }

    private void OnAbility2Action(InputAction.CallbackContext context)
    {
        _isAbility2Active = context.performed;
    }
    
    /// <summary>
    /// Global 일시정지 버튼 입력 시
    /// </summary>
    /// <param name="context"></param>
    private void OnPause(InputAction.CallbackContext context)
    {
        Debug.Log($"<color=green>일시정지</color>");
    }

    /// <summary>
    /// Global 지도 버튼 입력 시
    /// </summary>
    /// <param name="context"></param>
    private void OnMap(InputAction.CallbackContext context)
    {
        Debug.Log($"<color=green>지도 인풋</color>");

        _clientController.ToggleMap();
    }
    #endregion

    /// <summary>
    /// 인풋 모드 변경(Player)
    /// 서버에서 강제 변경용
    /// </summary>
    [TargetRpc]
    public void RpcEnablePlayerMode(NetworkConnection target)
    {
        // 액션 맵 전환
        EnablePlayerMode();
    }

    /// <summary>
    /// 인풋 모드 변경(Player)
    /// 클라자체에서 변경용
    /// </summary>
    public void EnablePlayerMode()
    {
        // 액션 맵 전환
        _playerInput.SwitchCurrentActionMap("Player");
    }

    /// <summary>
    /// 인풋 모드 변경(Module)
    /// 서버에서 강제 변경용
    /// </summary>
    [TargetRpc]
    public void RpcEnableModuleMode(NetworkConnection target)
    {
        // 액션 맵 전환
        EnableModuleMode();
    }

    public void EnableModuleMode()
    {
        // 액션 맵 전환
        _playerInput.SwitchCurrentActionMap("Module");
    }

    [TargetRpc]
    public void SetAbilityDeactive(NetworkConnection target)
    {
        _isAbility1Active = false;
        _isAbility2Active = false;
    }

    /// <summary>
    /// 입력 장치가 변경되었을 때 호출됨
    /// </summary>
    private void OnInputDeviceChanged(InputUser user, InputUserChange change, InputDevice device)
    {
        if (change == InputUserChange.ControlSchemeChanged)
        {
            string controlScheme = _playerInput.currentControlScheme;

            // 키보드&마우스 사용 중 게임 패드 입력이 들어올 시
            if (controlScheme == "Gamepad")
            {
                Debug.Log("<color=yellow>패드 입력 감지됨</color>");

                _isUsingGamepad = true;
            }
            else if (controlScheme == "KeyboardMouse")
            {
                Debug.Log("<color=yellow>키보드&마우스 입력 감지됨</color>");

                _isUsingGamepad = false;
            }
            // 키 가이드 변경
            if (InteractionPromptManager.Instance != null)
            {
                InteractionPromptManager.Instance.OnInputDeviceChanged(_isUsingGamepad);
            }
        }
    }
}
