using Mirror;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

/// <summary>
/// 클라이언트로부터 전달받은 인풋 데이터를 서버측에서 적용하는 클래스
/// [조우석 - 2025-09.07.]
/// [김도형 - 2025.11.06]
/// </summary>
public class ServerInputHandler : NetworkSingleton<ServerInputHandler>
{
    // 각 클라이언트들의 컨트롤러들 딕셔너리. 각 클라이언트의 플레이어의 netId를 key값으로 둔다.
    private Dictionary<uint, PlayerControllerHolder> _clientControllerHolder = new Dictionary<uint, PlayerControllerHolder>();

    protected override void Awake()
    {
        base.Awake();
        DontDestroyOnLoad(gameObject);  // 씬 전환 시에도 유지
    }

    private void Update()
    {
        // 서버에서만 동작
        if (!isServer)
        {
            return;
        }

        // 모든 등록된 클라이언트들에 대해서 인풋 정보를 적용
        foreach (var controllerHolder in _clientControllerHolder.Values)
        {
            // 서버는 _serverInputState에 저장된 값으로 모든 로직을 처리
            controllerHolder.LocalPlayerController.HandleMovement(controllerHolder.ServerInputState.moveInput);

            // 모듈 조작
            if (controllerHolder.ServerInputState.isModuleMode)
            {
                if (!controllerHolder.LocalPlayerModuleController || !controllerHolder.LocalPlayerModuleController.ActiveModule)
                {
                    continue;
                }
                // 조준 로직
                if (controllerHolder.LocalPlayerModuleController.ActiveModule.ModuleType == ModuleType.함포 ||
                    controllerHolder.LocalPlayerModuleController.ActiveModule.ModuleType == ModuleType.방어막 ||
                     controllerHolder.LocalPlayerModuleController.ActiveModule.ModuleType == ModuleType.크레인)
                {
                    if (!controllerHolder.ServerInputState.isUsingGamepad)
                    {
                        controllerHolder.LocalPlayerModuleController.ActiveModule?.Rotate(Vector2.zero);
                        controllerHolder.LocalPlayerModuleController.ActiveModule?.Aim(controllerHolder.ServerInputState.mouseWorldPos);
                    }
                    else
                    {
                        controllerHolder.LocalPlayerModuleController.ActiveModule?.Aim(Vector2.zero);
                        controllerHolder.LocalPlayerModuleController.ActiveModule?.Rotate(controllerHolder.ServerInputState.aimInput);
                    }
                }
                else
                {
                    controllerHolder.LocalPlayerModuleController.ActiveModule?.Rotate(controllerHolder.ServerInputState.aimInput);
                }

                // 어빌리티 로직 1
                if (controllerHolder.ServerInputState.isAbility1Pressed)
                {
                    controllerHolder.LocalPlayerModuleController.ActiveModule?.ActivateAbility1();
                }
                else
                {
                    controllerHolder.LocalPlayerModuleController.ActiveModule?.DeactivateAbility1();
                }

                // 어빌리티 로직 2
                if (controllerHolder.ServerInputState.isAbility2Pressed)
                {
                    controllerHolder.LocalPlayerModuleController.ActiveModule?.ActivateAbility2();
                }
                else
                {
                    controllerHolder.LocalPlayerModuleController.ActiveModule?.DeactivateAbility2();
                }
            }
            // 플레이어 조작
            else
            {
                if (!controllerHolder.ServerInputState.isUsingGamepad)
                {
                    controllerHolder.LocalPlayerController.SetMouseAimVector(controllerHolder.ServerInputState.mouseWorldPos);
                }
                else
                {
                    controllerHolder.LocalPlayerController.SetLastMoveVector(controllerHolder.ServerInputState.moveInput);
                }
            }
        }
    }

    /// <summary>
    /// 해당 클라이언트와 연결된 컨트롤러를 반환
    /// - 없으면 신규 등록 후 반환
    /// </summary>
    /// <param name="clientId"></param>
    /// <returns></returns>
    private PlayerControllerHolder CheckAndGetClientController(uint clientId)
    {
        if (!_clientControllerHolder.ContainsKey(clientId))
        {
            if (!SetClientController(clientId))
            {
                Debug.LogError($"서버측 인풋 핸들러에서 (netId = {clientId}) 컨트롤러들을 등록하는 과정 중 오류가 발생했습니다.");
            }
        }

        return _clientControllerHolder[clientId];
    }

    private bool CheckClientController(uint clientId) => _clientControllerHolder.ContainsKey(clientId);

    /// <summary>
    /// 해당 클라이언트와 연결된 플레이어 컨트롤러 신규 등록
    /// </summary>
    /// <param name="clientId"></param>
    /// <returns></returns>
    private bool SetClientController(uint clientId)
    {
        PlayerController controller = GameNetworkManager.GetNetworkComponent<PlayerController>(clientId);
        PlayerLTDController ltdController = GameNetworkManager.GetNetworkComponent<PlayerLTDController>(clientId);
        PlayerModuleController moduleController = GameNetworkManager.GetNetworkComponent<PlayerModuleController>(clientId);
        PlayerTradeController tradeController = GameNetworkManager.GetNetworkComponent<PlayerTradeController>(clientId);
        PlayerCombatController combatController = GameNetworkManager.GetNetworkComponent<PlayerCombatController>(clientId);
        PlayerInteractController interactController = GameNetworkManager.GetNetworkComponent<PlayerInteractController>(clientId);

        PlayerControllerHolder clientController = new PlayerControllerHolder(controller, ltdController, moduleController, tradeController, combatController, interactController);
        return _clientControllerHolder.TryAdd(clientId, clientController);
    }

    /// <summary>
    /// 해당 클라이언트와 연결된 플레이어 컨트롤러 제거
    /// </summary>
    /// <param name="clientId"></param>
    [Command(requiresAuthority = false)]
    public void CmdRemoveClientController(uint clientId)
    {
        if (CheckClientController(clientId))
        {
            _clientControllerHolder.Remove(clientId);
        }
    }

    /// <summary>
    /// CLIENT->SERVER : 지속적인 인풋값 데이터를 업데이트
    /// </summary>
    /// <param name="clientId"></param>
    /// <param name="input"></param>
    [Command(requiresAuthority = false)]
    public void CmdSendInputState(uint clientId, PlayerInputState input)
    {
        var clientController = CheckAndGetClientController(clientId);
        clientController.ServerInputState = input;
        //moveInput 값 출력
    }

    /// <summary>
    /// CLIENT->SERVER : 서버측으로 플레이어의 상호작용 조작 신호 전달
    /// </summary>
    /// <param name="clientId"></param>
    /// <param name="isPerformed"></param>
    /// <param name="sender"></param>
    [Command(requiresAuthority = false)]
    public void CmdCallInteract(uint clientId, bool isPerformed, NetworkConnectionToClient sender = null)
    {
        var clientController = CheckAndGetClientController(clientId);

        if (isPerformed)
        {
            // int storageResult = clientController.LocalPlayerModuleController.CallStorageInteract();
            // if (storageResult == 0) // 물건을 갖고있는 상태에서 저장소 모듈을 사용할경우 물건만 넣음
            // {
            //     Debug.Log($"<color=green>플레이어 저장소 상호작용</color>");
            //     return;
            // }
            int radrarResult = clientController.LocalPlayerModuleController.CallRadarInteract();
            if (radrarResult == 0) // 레이더 모듈 상호작용
            {
                Debug.Log($"<color=green>플레이어 레이더 상호작용</color>");
                return;
            }

            // 먼저 가방 넣기 시도
            if (clientController.LocalPlayerModuleController.CallPutItemInBackPack())
            {
                Debug.Log($"<color=green>플레이어 가방에 아이템 넣기</color>");
                return;
            }

            if (clientController.LocalPlayerModuleController.CallToggleBackPack()) // 가방 모듈 상호작용
            {
                Debug.Log($"<color=green>플레이어 가방 상호작용</color>");
                return;
            }

            if (clientController.LocalPlayerTradeController.CallSellItem()) // 들고있는 아이템 판매
            {
                Debug.Log($"<color=green>플레이어 아이템 판매</color>");
                return;
            }
            if (clientController.LocalPlayerInteractController.CallInteractWithNPC())
            {
                Debug.Log($"<color=green>플레이어 NPC 상호작용</color>");
                return;
            }

            if (clientController.LocalPlayerModuleController.CallUseModule())
            {
                // 장비 모드 활성화
                ClientInputHandler.Instance.RpcEnableModuleMode(sender);
                Debug.Log($"<color=green>플레이어 모듈 앉기</color>");
                return;
            }

            //clientController.LocalPlayerModuleController.CallStartActivateRadar();
        }
        else
        {
            //clientController.LocalPlayerModuleController.CallStopActivateRadar();
        }
    }

    /// <summary>
    /// CLIENT->SERVER : 서버측으로 플레이어 Lift/Drop/Throw 조작 신호 전달
    /// </summary>
    /// <param name="clientId"></param>
    [Command(requiresAuthority = false)]
    public void CmdCallLTD(uint clientId)
    {
        var clientController = CheckAndGetClientController(clientId);
        // 먼저 들기/내리기 시도
        if (clientController.LocalPlayerLTDController.CallLiftDrop())
        {
            return;
        }
        // 그 다음 아이템 구매 시도
        if (clientController.LocalPlayerTradeController.CallBuyItem())
        {
            return;
        }
        //clientController.LocalPlayerLTDController.CallThrow(); 던지기는 홀드시
    }

    /// <summary>
    /// CLIENT->SERVER : 서버측으로 플레이어 lift/drop 조작 신호 전달
    /// </summary>
    [Command(requiresAuthority = false)]
    public void CmdCallThrow(uint clientId)
    {
        var clientController = CheckAndGetClientController(clientId);
        if (clientController.LocalPlayerLTDController.LiftedLTDObject != null)
        {
            clientController.LocalPlayerLTDController.CallThrow();
        }
    }

    /// <summary>
    /// CLIENT->SERVER : 서버측으로 모듈 사용 신호 전달
    /// </summary>
    [Command(requiresAuthority = false)]
    public void CmdCallUse(uint clientId, bool isPressed)
    {
        var clientController = CheckAndGetClientController(clientId);

        if (clientController.LocalPlayerLTDController.LiftedLTDObject != null)
        {
            clientController.LocalPlayerLTDController.CallSpecialAction(isPressed);
        }
        else
        {
            // 빈손일 때 근접 공격 처리
            if (clientController.LocalPlayerCombatController != null)
            {
                // 공격 방향 계산 (마우스 위치 또는 마지막 이동 방향)
                Vector2 attackDirection;
                if (!clientController.ServerInputState.isUsingGamepad)
                {
                    // 마우스 사용 시: 플레이어 -> 마우스 방향
                    Vector2 playerPos = clientController.LocalPlayerController.transform.position;
                    attackDirection = ((Vector2)clientController.ServerInputState.mouseWorldPos - playerPos).normalized;
                }
                else
                {
                    // 게임패드 사용 시: 마지막 이동 방향
                    attackDirection = clientController.ServerInputState.moveInput.normalized;
                    if (attackDirection.sqrMagnitude < 0.01f)
                    {
                        // 이동하지 않았다면 플레이어가 보고 있는 방향 사용
                        attackDirection = clientController.LocalPlayerController.LastFaceVector;
                        if (attackDirection.sqrMagnitude < 0.01f)
                        {
                            attackDirection = Vector2.right; // 기본 방향
                        }
                    }
                }

                clientController.LocalPlayerCombatController.OnAttackInput(attackDirection, isPressed);
            }
        }
    }

    /// <summary>
    /// CLIENT->SERVER : 서버측으로 모듈 사용 중지 신호 전달
    /// </summary>
    /// <param name="clientId"></param>
    /// <param name="target"></param>
    [Command(requiresAuthority = false)]
    public void CmdCallExitModule(uint clientId)
    {
        var clientController = CheckAndGetClientController(clientId);

        clientController.LocalPlayerModuleController.CallExitModule();
        Debug.Log($"<color=green>모듈 플레이어 내리기</color>");
    }

    /// <summary>
    /// CLIENT->SERVER : 가방에서 아이템 꺼내기 요청
    /// </summary>
    /// <param name="clientId">요청한 플레이어의 netId</param>
    /// <param name="backPackNetId">대상 가방의 netId</param>
    /// <param name="slotIndex">꺼낼 슬롯 인덱스</param>
    [Command(requiresAuthority = false)]
    public void CmdRequestTakeItemFromBackPack(uint clientId, uint backPackNetId, int slotIndex)
    {
        var clientController = CheckAndGetClientController(clientId);

        // 가방 오브젝트 찾기
        if (NetworkServer.spawned.TryGetValue(backPackNetId, out NetworkIdentity backPackIdentity))
        {
            BackPack backPack = backPackIdentity.GetComponent<BackPack>();
            if (backPack != null)
            {
                backPack.TakeItem(slotIndex, clientController.LocalPlayerLTDController);
            }
        }
    }
}

/// <summary>
/// 플레이어의 조작과 관련된 컴포넌트들 모음
/// [조우석 - 2025.09.08.]
/// </summary>
public class PlayerControllerHolder
{
    public PlayerInputState ServerInputState;   // 해당 클라이언트의 지속적인 Input 데이터 

    public PlayerController LocalPlayerController { get; }
    public PlayerLTDController LocalPlayerLTDController { get; }
    public PlayerModuleController LocalPlayerModuleController { get; }
    public PlayerTradeController LocalPlayerTradeController { get; }
    public PlayerCombatController LocalPlayerCombatController { get; }
    public PlayerInteractController LocalPlayerInteractController { get; }

    public PlayerControllerHolder(PlayerController controller, PlayerLTDController ltdController, PlayerModuleController moduleController, PlayerTradeController tradeController, PlayerCombatController combatController, PlayerInteractController interactController)
    {
        LocalPlayerController = controller;
        LocalPlayerLTDController = ltdController;
        LocalPlayerModuleController = moduleController;
        LocalPlayerTradeController = tradeController;
        LocalPlayerCombatController = combatController;
        LocalPlayerInteractController = interactController;
    }
}

/// <summary>
/// 클라이언트의 지속적인 input값에 대한 구조체
/// </summary>
public struct PlayerInputState
{
    public Vector2 moveInput;       // 이동 벡터
    public Vector2 aimInput;        // 게임패드 조준 벡터
    public Vector3 mouseWorldPos;   // 마우스 월드 위치
    public bool isAbility1Pressed;
    public bool isAbility2Pressed;
    public bool isUsingGamepad;
    public bool isModuleMode;       // 현재 모듈에 앉은 상태인가
}