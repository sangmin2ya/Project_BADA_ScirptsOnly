// using Mirror.Examples.BilliardsPredicted;
// using System;
// using System.Collections;
// using System.Collections.Generic;
// using System.Reflection;
// using Unity.VisualScripting;
// using UnityEngine;
// using UnityEngine.InputSystem;
// using UnityEngine.InputSystem.Users;

// /// <summary>
// /// 클라이언트 플레이어 인풋 관리 클래스
// /// [조우석 - 2025.09.07.]
// /// </summary>
// public class InputHandler : Singleton<InputHandler>
// {
//     [SerializeField] private UnityEngine.InputSystem.PlayerInput _playerInput;
//     private bool _isSetup = false;

//     // 플레이어 모드 액션
//     private InputAction _playerMoveAction;
//     private InputAction _playerLiftDropAction;
//     private InputAction _playerThrowAction;
//     private InputAction _playerEnterAction;
//     private InputAction _playerSpecialAction;
//     private InputAction _playerReviveAction;        // 사용되지 않을 가능성이 있음
//     private InputAction _playerDashAction;
//     private InputAction _playerPauseAction;

//     // 장비 공통 액션
//     private InputAction _moduleAimAction;
//     private InputAction _moduleAbility1Action;
//     private InputAction _moduleAbility2Action;
//     private InputAction _moduleExitAction;

//     // 지속적인 입력 상태 추적
//     private bool _isAbility1Active = false;
//     private bool _isAbility2Active = false;
//     private Vector2 _aimInput = Vector2.zero;
//     private Vector2 _aimUIPosition = Vector2.zero;
//     private bool _isUsingGamepad = false;

//     // 마우스 입력 벡터
//     private Vector2 _mouseScreenPos;
//     private Vector3 _mouseWorldPos;

//     // 외부 클래스 참조
//     private PlayerController _localPlayerController = null;
//     private PlayerLTDController _localPlayerLTDController = null;
//     private PlayerModuleController _localPlayerModuleController = null;
//     private PlayerState _localPlayerState = null;

//     #region Monobehavior

//     private void OnEnable()
//     {
//         // 플레이어 모드 활성화
//         EnablePlayerMode();
//     }

//     private void OnDestroy()
//     {
//         // 콜백 해제
//         _playerEnterAction.performed -= OnEnter;
//         _moduleExitAction.performed -= OnExit;
//         _playerLiftDropAction.performed -= OnLiftDrop;
//         _playerThrowAction.performed -= OnThrow;
//         _playerPauseAction.performed -= OnPause;
//         _playerDashAction.performed -= OnDash;

//         InputUser.onChange -= OnInputDeviceChanged;
//     }

//     private void Update()
//     {
//         // Setup 하기 전까지는 동작하지 않음
//         if (!_isSetup)
//         {
//             return;
//         }

//         // 플레이어 이동 처리
//         Vector2 moveInput = _playerMoveAction.ReadValue<Vector2>();
//         _localPlayerController.HandleMovement(moveInput);

//         // 모듈 탑승상태일때 동작
//         if (_playerInput.currentActionMap.name == "Module")
//         {
//             _aimInput = _moduleAimAction.ReadValue<Vector2>();
//             // 모듈 조준 입력 처리
//             if (_localPlayerModuleController.ActiveModule.ModuleType == ModuleType.함포 || _localPlayerModuleController.ActiveModule.ModuleType == ModuleType.방어막)
//             {
//                 //마우스 사용시
//                 if (!_isUsingGamepad)
//                 {
//                     // 마우스 위치를 이용한 조준
//                     _mouseScreenPos = Mouse.current.position.ReadValue();
//                     _mouseWorldPos = Camera.main.ScreenToWorldPoint(
//                         new Vector3(_mouseScreenPos.x, _mouseScreenPos.y, Camera.main.nearClipPlane)
//                         );
//                     // 플레이어 컨트롤러에 데이터 전송
//                     _localPlayerModuleController.ActiveModule?.Rotate(Vector2.zero);
//                     _localPlayerModuleController.ActiveModule?.Aim(_mouseWorldPos);
//                 }
//                 else
//                 {
//                     // 게임패드 사용시
//                     _localPlayerModuleController.ActiveModule?.Aim(Vector2.zero);
//                     _localPlayerModuleController.ActiveModule?.Rotate(_aimInput);
//                 }
//             }
//             else
//             {
//                 _localPlayerModuleController.ActiveModule?.Rotate(_aimInput);
//             }

//             // 지속적인 입력 상태에 따른 장비 기능 활성화
//             if (_isAbility1Active)
//             {
//                 // 현재 모듈의 Ability1 사용 Update
//                 _localPlayerModuleController.ActiveModule?.ActivateAbility1();
//             }
//             else
//             {
//                 // 현재 모듈의 Ability1 미사용 Update
//                 _localPlayerModuleController.ActiveModule?.DeactivateAbility1();
//             }

//             if (_isAbility2Active)
//             {
//                 // 현재 모듈의 Ability2 사용 Update
//                 _localPlayerModuleController.ActiveModule?.ActivateAbility2();
//             }
//             else
//             {
//                 // 현재 모듈의 Ability2 미사용 Update
//                 _localPlayerModuleController.ActiveModule?.DeactivateAbility2();
//             }
//         }
//         else
//         {
//             if (!_isUsingGamepad)
//             {
//                 // 마우스 위치 읽기
//                 _mouseScreenPos = Mouse.current.position.ReadValue();
//                 _mouseWorldPos = Camera.main.ScreenToWorldPoint(
//                     new Vector3(_mouseScreenPos.x, _mouseScreenPos.y, Camera.main.nearClipPlane)
//                     );
//                 // 플레이어 컨트롤러에 데이터 전송
//                 _localPlayerController.SetMouseAimVector(_mouseWorldPos);
//             }
//             else
//             {
//                 // 플레이어 컨트롤러에 이동 데이터 전송
//                 _localPlayerController.SetLastMoveVector(moveInput);
//             }
//         }
//         // 또는 조준 방향 계산 등
//         Vector2 aimDir = (_mouseWorldPos - transform.position).normalized;
//     }
//     #endregion

//     #region 초기화 메서드

//     /// <summary>
//     /// 인풋 핸들러의 이벤트와 연결될 플레이어를 셋업하는 메서드
//     /// - 로컬 플레이어 생성 시 호출
//     /// </summary>
//     /// <param name="controller">인풋과 연결할 플레이어 오브젝트</param>
//     public void Setup(PlayerController controller)
//     {
//         if (_playerInput == null) _playerInput = GetComponent<UnityEngine.InputSystem.PlayerInput>();

//         // 액션 참조 가져오기
//         _playerMoveAction = _playerInput.actions["Player/Move"];
//         _playerLiftDropAction = _playerInput.actions["Player/LiftDrop"];
//         _playerThrowAction = _playerInput.actions["Player/Throw"];
//         _playerEnterAction = _playerInput.actions["Player/Enter"];
//         _playerSpecialAction = _playerInput.actions["Player/Special"];
//         _playerReviveAction = _playerInput.actions["Player/Revive"];
//         _playerDashAction = _playerInput.actions["Player/Dash"];
//         _playerPauseAction = _playerInput.actions["Player/Pause"];

//         _moduleAimAction = _playerInput.actions["Module/Aim"];
//         _moduleAbility1Action = _playerInput.actions["Module/Ability1"];
//         _moduleAbility2Action = _playerInput.actions["Module/Ability2"];
//         _moduleExitAction = _playerInput.actions["Module/Exit"];

//         // 플레이어 오브젝트 참조
//         _localPlayerController = controller;
//         _localPlayerLTDController = controller.GetComponent<PlayerLTDController>();
//         _localPlayerModuleController = controller.GetComponent<PlayerModuleController>();

//         // 콜백 등록
//         _playerEnterAction.performed += OnEnter;
//         _moduleExitAction.performed += OnExit;
//         _playerLiftDropAction.performed += OnLiftDrop;
//         _playerThrowAction.performed += OnThrow;
//         _playerPauseAction.performed += OnPause;
//         _playerDashAction.performed += OnDash;

//         // 지속적인 입력에 대한 액션 콜백
//         _moduleAbility1Action.performed += ctx => _isAbility1Active = true;
//         _moduleAbility1Action.canceled += ctx => _isAbility1Active = false;

//         _moduleAbility2Action.performed += ctx => _isAbility2Active = true;
//         _moduleAbility2Action.canceled += ctx => _isAbility2Active = false;

//         _playerSpecialAction.performed += ctx => OnSpecial(ctx);
//         _playerSpecialAction.canceled += ctx => OnSpecial(ctx);

//         _playerReviveAction.performed += ctx => OnRevive(ctx);
//         _playerReviveAction.canceled += ctx => OnRevive(ctx);

//         InputUser.onChange += OnInputDeviceChanged;

//         _isSetup = true;
//     }

//     #endregion

//     #region 입력 콜백 메서드
//     /// <summary>
//     /// 플레이어 특수 행동 버튼 입력 시
//     /// </summary>
//     /// <param name="context"></param>
//     private void OnSpecial(InputAction.CallbackContext context)
//     {
//         if (context.performed)
//         {
//             _localPlayerLTDController.CallSpecialAction(true);
//             _localPlayerModuleController.CallTeleport();
//             Debug.Log($"<color=green>플레이어 특수 행동 시작</color>");
//         }
//         else
//         {
//             _localPlayerLTDController.CallSpecialAction(false);
//             Debug.Log($"<color=green>플레이어 특수 행동 취소</color>");
//         }
//     }

//     /// <summary>
//     /// 플레이어 LTD 오브젝트 들고 내리기 버튼 입력 시
//     /// </summary>
//     /// <param name="context"></param>
//     private void OnLiftDrop(InputAction.CallbackContext context)
//     {
//         _localPlayerLTDController.CallLiftDrop();
//         Debug.Log($"<color=green>플레이어 오브젝트 들고 내리기</color>");
//     }

//     /// <summary>
//     /// 플레이어 던지기 버튼 입력 시
//     /// </summary>
//     /// <param name="context"></param>
//     private void OnThrow(InputAction.CallbackContext context)
//     {
//         if (context.performed)
//         {
//             // 버튼을 누르기 시작할 때 - 차징 시작
//             _localPlayerLTDController.CallThrow();
//             Debug.Log($"<color=green>플레이어 오브젝트 던지기 차징 시작</color>");
//         }
//     }

//     /// <summary>
//     /// 플레이어 모듈 않기 버튼 입력 시
//     /// </summary>
//     /// <param name="context"></param>
//     private void OnEnter(InputAction.CallbackContext context)
//     {
//         // 플레이어가 스턴에 의해서 모듈에 접근할 수 없다면 return
//         //if (_isStunned)
//         //{
//         //    return;
//         //}
//         //만약 저장고와 상호작용 할수 있다면
//         int storageResult = _localPlayerModuleController.CallStorageInteract();
//         if (storageResult == 0) // 물건을 갖고있는 상태에서 저장소 모듈을 사용할경우 물건만 넣음
//         {
//             Debug.Log($"<color=green>플레이어 저장소 상호작용</color>");
//             return;
//         }
//         if (storageResult == 1) // 아니라면 저장소 모듈 사용 (모듈 모드 전환)
//         {
//             // 저장소 모듈 사용
//             EnableModuleMode();
//             return;
//         }

//         if (_localPlayerModuleController.CallUseModule())
//         {
//             // 장비 모드 활성화
//             EnableModuleMode();
//             Debug.Log($"<color=green>플레이어 모듈 앉기</color>");
//             return;
//         }
//     }

//     /// <summary>
//     /// 모듈 상태 플레이어 모듈 내리기 버튼 입력 시
//     /// </summary>
//     /// <param name="context"></param>
//     private void OnExit(InputAction.CallbackContext context)
//     {
//         _isAbility1Active = false;
//         _isAbility2Active = false;

//         // 플레이어 모드 활성화
//         EnablePlayerMode();

//         _localPlayerModuleController.CallExitModule();
//         Debug.Log($"<color=green>모듈 플레이어 내리기</color>");
//     }

//     /// <summary>
//     /// 모듈 사용중인 플레이어를 상호작용 키 외의 조건에서 강제로 내리기
//     /// </summary>
//     public void OnForceExit()
//     {
//         _isAbility1Active = false;
//         _isAbility2Active = false;

//         // 플레이어 모드 활성화
//         EnablePlayerMode();

//         _localPlayerModuleController.CallExitModule();
//         Debug.Log($"<color=green>모듈 플레이어 강제로 내리기</color>");
//     }

//     /// <summary>
//     /// 플레이어 부활 버튼 입력 시
//     /// </summary>
//     /// <param name="context"></param>
//     private void OnRevive(InputAction.CallbackContext context)
//     {
//         if (context.performed)
//         {
//             Debug.Log($"<color=green>플레이어 부활 시작</color>");
//         }
//         else
//         {
//             Debug.Log($"<color=green>플레이어 부활 취소</color>");
//         }
//     }

//     /// <summary>
//     /// 플레이어 대쉬 버튼 입력 시
//     /// </summary>
//     /// <param name="context"></param>
//     private void OnDash(InputAction.CallbackContext context)
//     {
//         Debug.Log($"<color=green>플레이어 대쉬</color>");
//     }

//     /// <summary>
//     /// 플레이어 일시정지 버튼 입력 시
//     /// </summary>
//     /// <param name="context"></param>
//     private void OnPause(InputAction.CallbackContext context)
//     {
//         Debug.Log($"<color=green>플레이어 일시정지</color>");
//     }
//     #endregion

//     /// <summary>
//     /// 인풋 모드 변경(Player)
//     /// </summary>
//     private void EnablePlayerMode()
//     {
//         // 액션 맵 전환
//         _playerInput.SwitchCurrentActionMap("Player");
//     }

//     /// <summary>
//     /// 인풋 모드 변경(Module)
//     /// </summary>
//     private void EnableModuleMode()
//     {
//         // 액션 맵 전환
//         _playerInput.SwitchCurrentActionMap("Module");
//     }

//     /// <summary>
//     /// 입력 장치가 변경되었을 때 호출됨
//     /// </summary>
//     private void OnInputDeviceChanged(InputUser user, InputUserChange change, InputDevice device)
//     {
//         if (change == InputUserChange.ControlSchemeChanged)
//         {
//             string controlScheme = _playerInput.currentControlScheme;

//             // 키보드&마우스 사용 중 게임 패드 입력이 들어올 시
//             if (controlScheme == "Gamepad")
//             {
//                 Debug.Log("<color=yellow>패드 입력 감지됨</color>");

//                 _isUsingGamepad = true;
//             }
//             else if (controlScheme == "KeyboardMouse")
//             {
//                 Debug.Log("<color=yellow>키보드&마우스 입력 감지됨</color>");

//                 _isUsingGamepad = false;
//             }
//         }
//     }
// }
