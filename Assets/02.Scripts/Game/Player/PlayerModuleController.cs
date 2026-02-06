using UnityEngine;
using Mirror;
using Cinemachine;
/// <summary>
/// 근처 모듈을 감지하고, 사용하는데 필요한 컴포넌트
/// [박상민 - 2025.06.20]
/// </summary>
public class PlayerModuleController : NetworkBehaviour
{
    [Header("<color=green>모듈 설정")]
    [Tooltip("일반상태 FOV")]
    [SerializeField] private float _normalFOV = 10f;
    [Tooltip("운전상태 FOV")]
    [SerializeField] private float _moduleFOV = 40f;
    [Tooltip("모듈을 감지하는 반경")]
    [SerializeField] private float _detectRadius = 1f;
    [Tooltip("모듈 오브젝트 태그")]
    [SerializeField] private string _moduleObjectTag = "ModuleObject";

    [Header("<color=yellow>모듈 상태")]
    [Tooltip("감지한 주변 모듈 오브젝트")]
    [SerializeField] private Module _detectedModule;
    [Tooltip("사용중인 모듈 오브젝트")]
    [SerializeField] private Module _activeModule;
    [Tooltip("감지된 레이더 오브젝트")]
    [SerializeField] private Radar _detectedRadar;
    [Tooltip("감지된 가방 오브젝트")]
    [SerializeField] private BackPack _detectedBackPack;

    [Header("운전시 카메라 피벗 오프셋")]
    [SerializeField] private float _cameraPivotOffsetY = 10f;

    // private
    private PlayerController _playerController;
    private TeleportPoint _teleportPoint; // 플레이어가 도킹 후 이동할 위치
    private float _teleportTimer = 0f;
    private Module _previousDetectedModule; // 이전에 감지된 모듈 저장용
    private Radar _previousDetectedRadar; // 이전에 감지된 레이더 저장용
    private BackPack _previousDetectedBackPack; // 이전에 감지된 가방 저장용

    // 접근 프로퍼티
    public float NormalFOV => _normalFOV;
    public float ModuleFOV => _moduleFOV;
    public Module DetectedModule => _detectedModule;
    public Module ActiveModule => _activeModule;
    private PlayerState _playerState => _playerController.PlayerState;

    private void Awake()
    {
        _playerController = GetComponent<PlayerController>();
    }

    void Update()
    {
        if (!isServer)
        {
            return;
        }
        // 플레이어가 모듈 오브젝트를 감지
        DetectModule();
        if (_activeModule != null)
        {
            _teleportTimer = 0f;
            _teleportPoint?.RpcUpdateTeleportProgressBar(connectionToClient, 0f);
            return; // 모듈 사용중이면 텔레포트 처리 안함
        }
        if (_teleportPoint != null)
        {
            _teleportTimer += Time.deltaTime;
            _teleportPoint.RpcUpdateTeleportProgressBar(connectionToClient, _teleportTimer / _teleportPoint.teleportDuration);
            if (_teleportTimer >= _teleportPoint.teleportDuration)
            {
                CallTeleport();
                _teleportTimer = 0f;
                _teleportPoint.RpcUpdateTeleportProgressBar(connectionToClient, 0f);
                _teleportPoint = null;
            }
        }
    }

    /// <summary>
    /// 플레이어 콜라이더를 사용하여 근처 모듈 오브젝트를 감지합니다.
    /// </summary>
    private void DetectModule()
    {
        _previousDetectedModule = _detectedModule;
        _previousDetectedRadar = _detectedRadar;
        _previousDetectedBackPack = _detectedBackPack;
        _detectedModule = null; // 초기화
        _detectedRadar = null; // 초기화
        _detectedBackPack = null; // 초기화

        Collider2D[] colliders = Physics2D.OverlapCircleAll(transform.position, _detectRadius);
        float distance = float.MaxValue;

        // 감지된 모듈 중 가장 가까운 모듈을 찾습니다.
        foreach (Collider2D collider in colliders)
        {
            if (collider.CompareTag(_moduleObjectTag))
            {
                Module module = null;
                if (collider.TryGetComponent<Module>(out module))
                {
                    // 직접 Module 컴포넌트가 있는 경우 그대로 사용
                }
                else
                {
                    // 부모 오브젝트에서 ModuleController를 찾아 연결된 모듈 가져오기
                    module = collider.transform.parent.GetComponent<ModuleController>().ConnectedModule;
                }

                if (module != null)
                {
                    float currentDistance = Vector2.Distance(transform.position, module.transform.position);
                    if (currentDistance < distance)
                    {
                        distance = currentDistance;
                        _detectedModule = module; // 가장 가까운 모듈로 업데이트
                    }
                }
            }
            if (collider.CompareTag("Radar"))
            {
                Radar radar = collider.GetComponent<Radar>();
                if (radar != null)
                {
                    _detectedRadar = radar;
                }
            }
            if (collider.TryGetComponent<BackPack>(out BackPack backPack))
            {
                if (backPack != null && !backPack.IsBeingLifted)
                {
                    _detectedBackPack = backPack;
                }
            }
        }

        // 모듈 감지 변경 시 UI 업데이트
        if (_previousDetectedModule != _detectedModule)
        {
            // 이전 모듈 UI 숨김
            if (_previousDetectedModule != null)
            {
                RpcHideInteractionPrompt(connectionToClient, _previousDetectedModule.netId);
            }

            // 새로운 모듈 UI 표시
            if (_detectedModule != null && _detectedModule is IInteractable interactable)
            {
                // 서버에서 상호작용 정보 계산 후 클라이언트로 전달
                InteractionInfo info = interactable.GetInteractionInfo();
                RpcShowInteractionPrompt(connectionToClient, info);
            }
        }

        // 레이더 감지 처리 (옵션: 레이더도 IInteractable 구현 가능)
        if (_detectedRadar != null)
        {
            if (_previousDetectedRadar != _detectedRadar)
            {
                // 이전 레이더 UI 숨김
                if (_previousDetectedRadar != null)
                {
                    RpcHideInteractionPrompt(connectionToClient, _previousDetectedRadar.netId);
                }

                // 새로운 레이더 UI 표시
                if (_detectedRadar is IInteractable interactable)
                {
                    InteractionInfo info = interactable.GetInteractionInfo();
                    RpcShowInteractionPrompt(connectionToClient, info);
                }
            }
            _previousDetectedRadar = _detectedRadar;
        }
        else
        {
            // 레이더가 감지되지 않았을 때 이전 레이더 UI 숨김
            if (_previousDetectedRadar != null)
            {
                RpcHideInteractionPrompt(connectionToClient, _previousDetectedRadar.netId);
                _previousDetectedRadar = null;
            }
        }

        if (_detectedBackPack != null)
        {
            if (_previousDetectedBackPack != _detectedBackPack)
            {
                // 이전 가방 UI 숨김
                if (_previousDetectedBackPack != null)
                {
                    RpcHideInteractionPrompt(connectionToClient, _previousDetectedBackPack.netId);
                }

                // 새로운 가방 UI 표시
                if (_detectedBackPack is IInteractable interactable)
                {
                    InteractionInfo info = interactable.GetInteractionInfo();
                    RpcShowInteractionPrompt(connectionToClient, info);
                }
            }
            _previousDetectedBackPack = _detectedBackPack;
        }
        else
        {
            // 가방이 감지되지 않았을 때 이전 가방 UI 숨김
            if (_previousDetectedBackPack != null)
            {
                RpcHideInteractionPrompt(connectionToClient, _previousDetectedBackPack.netId);
                _previousDetectedBackPack.CallCloseBackPack(connectionToClient);
                _previousDetectedBackPack = null;
            }
        }
    }
    /// <summary>
    /// 감지된 모듈을 사용합니다. - InputHandler에서 호출됨
    /// 이슈 - 만약? 플레이어가 동기화 되기전에 동시에 모듈을 사용하면?? 어떻게 되나
    /// </summary>
    public bool CallUseModule()
    {
        if (_detectedModule == null)
        {
            Debug.LogWarning("근처에 모듈이 없습니다.");
            return false;
        }
        if (_detectedModule.IsOccupied)
        {
            Debug.LogWarning("이미 모듈이 사용 중입니다.");
            return false;
        }

        // UI 숨김
        RpcHideInteractionPrompt(connectionToClient, _detectedModule.netId);

        // 모듈을 사용 중인 플레이어 설정
        _detectedModule.PlayerEntered(this);
        _activeModule = _detectedModule;
        _detectedModule = null; // 사용 후 감지된 모듈 초기화

        // 플레이어 고정 위치로 이동
        Transform interactSpot = _activeModule.transform;
        // 모듈 컨트롤러가 있다면 그 위치로 이동
        if (_activeModule.ModuleController != null)
        {
            interactSpot = _activeModule.ModuleController.InteractionPosition;
        }
        transform.position = interactSpot.position;

        _playerController.RpcSetCameraFOV(connectionToClient, _moduleFOV);

        // 조종간 모듈이면 피벗 앞으로 이동
        if (_activeModule.TryGetComponent<CockpitAndEngine>(out CockpitAndEngine cockpit))
        {
            _playerController.RpcSetCameraPos(connectionToClient, _cameraPivotOffsetY);
        }

        return true;
    }

    /// <summary>
    /// 현재 사용 중인 모듈에서 나옵니다. - InputHandler에서 호출됨
    /// </summary>
    public void CallExitModule()
    {
        if (_activeModule == null)
        {
            Debug.LogWarning("현재 사용 중인 모듈이 없습니다.");
            return;
        }
        // 모듈 사용 중인 플레이어 초기화
        _activeModule.PlayerExited();
        _activeModule = null; // 사용 후 활성 모듈 초기화
        DetectModule(); // 다시 모듈 감지 시작
        //CameraManager.Instance.SetPriority(false);
        _playerController.RpcSetCameraFOV(connectionToClient, _normalFOV);
        _playerController.RpcSetCameraPos(connectionToClient, 0f);
    }

    public void CallForceExitModule(Vector2 force)
    {
        if (_activeModule == null)
        {
            Debug.LogWarning("현재 사용 중인 모듈이 없습니다.");
            return;
        }
        CallExitModule();
        // 인풋모드 변경
        ClientInputHandler.Instance.RpcEnablePlayerMode(connectionToClient);
        // 플레이어를 잠시 기절시킴
        _playerState.ApplyStun();
        // 플레이어를 밀쳐냄
        GetComponent<Rigidbody2D>().velocity = force;
    }

    public void CallTeleport()
    {
        if (_teleportPoint == null || !_teleportPoint.gameObject.activeSelf)
        {
            return;
        }

        // 플레이어를 텔레포트 포인트로 이동
        transform.position = _teleportPoint.teleportPoint.position;
        RpcPlayerTeleported(connectionToClient);
        Debug.Log("플레이어가 텔레포트되었습니다.");
    }
    /// <summary>
    /// 임시 창고 모듈 사용 메소드
    /// </summary>
    /// <returns>-1: 실패, 0: 성공(모듈 모드로 변경 없이), 1: 성공(모듈 사용)</returns>
    public int CallStorageInteract()
    {
        if (_detectedModule == null)
        {
            return -1;
        }
        PlayerLTDController playerLTDController = GetComponent<PlayerLTDController>();
        // 포탄 창고 모듈이고, 플레이어가 포탄을 들고 있을 때
        if (_detectedModule.TryGetComponent<ShellStorage>(out ShellStorage shellStorage))
        {
            if (playerLTDController.LiftedLTDObject?.LtdobjectType == LTDObjectType.포탄)
            {
                // 들고있는 포탄을 창고에 추가
                shellStorage.AddShells();
                playerLTDController.CallPutItem();
            }
            return 0;
        }
        if (_detectedModule.TryGetComponent<RepairKitStorage>(out RepairKitStorage repairKitStorage))
        {
            if (playerLTDController.LiftedLTDObject?.LtdobjectType == LTDObjectType.수리도구)
            {
                // 들고있는 수리키트를 창고에 추가
                repairKitStorage.AddRepairKits();
                playerLTDController.CallPutItem();
            }
            return 0;
        }
        return -1;
    }

    public int CallRadarInteract()
    {
        if (_detectedRadar == null)
        {
            return -1;
        }
        // 만약 특수 고철 아이템을 들고있으면
        PlayerLTDController playerLTDController = GetComponent<PlayerLTDController>();
        if (playerLTDController.LiftedLTDObject != null && playerLTDController.LiftedLTDObject.LtdobjectType == LTDObjectType.고철)
        {
            if (playerLTDController.LiftedLTDObject.GetComponent<ScrapObject>().ScrapType != ScrapType.특수고철)
            {
                return -1; // 실패
            }
            // 레이더를 활성화시도
            if (_detectedRadar.ActiveRadar())
            {
                // 아이템 소모
                playerLTDController.CallPutItem();
                return 0; // 성공(레이더 비활성화)
            }
            else // 레이더가 이미 최대치까지 활성화되어있음
            {
                return -1; // 실패
            }
        }
        return -1; // 실패
    }

    /// <summary>
    /// 감지된 레이더를 활성화 시도합니다.
    /// </summary>
    public void CallStartActivateRadar()
    {
        if (_detectedRadar == null)
        {
            Debug.LogWarning("근처에 레이더가 없습니다.");
            return;
        }
        _detectedRadar.StartActivateRadar(this);
    }
    /// <summary>
    /// 감지된 레이더의 활성화를 중지합니다.
    /// </summary>
    public void CallStopActivateRadar()
    {
        if (_detectedRadar == null)
        {
            Debug.LogWarning("근처에 레이더가 없습니다.");
            return;
        }
        _detectedRadar.StopActivateRadar();
    }

    /// <summary>
    /// 감지된 가방을 엽니다.
    /// </summary>
    public bool CallToggleBackPack()
    {
        if (_detectedBackPack == null)
        {
            Debug.LogWarning("근처에 가방이 없습니다.");
            return false;
        }
        //들고있는게 있으면 안됨
        PlayerLTDController playerLTDController = GetComponent<PlayerLTDController>();
        if (playerLTDController.LiftedLTDObject != null)
        {
            Debug.LogWarning("가방을 열기 전에 들고있는 아이템을 내려놓으세요.");
            return false;
        }
        _detectedBackPack.CallToggleBackPack(connectionToClient);
        return true;
    }

    public void CallCloseBackPack()
    {
        if (_detectedBackPack == null)
        {
            Debug.LogWarning("근처에 가방이 없습니다.");
            return;
        }
        _detectedBackPack.CallCloseBackPack(connectionToClient);
    }

    /// <summary>
    /// 감지된 가방에 아이템을 넣습니다.
    /// </summary>
    /// <param name="item"></param>
    public bool CallPutItemInBackPack()
    {
        if (_detectedBackPack == null)
        {
            Debug.LogWarning("근처에 가방이 없습니다.");
            return false;
        }
        PlayerLTDController playerLTDController = GetComponent<PlayerLTDController>();
        if (playerLTDController.LiftedLTDObject == null)
        {
            Debug.LogWarning("넣을 아이템이 없습니다.");
            return false;
        }
        if (playerLTDController.LiftedLTDObject.LtdobjectType == LTDObjectType.플레이어 || playerLTDController.LiftedLTDObject.LtdobjectType == LTDObjectType.몬스터)
        {
            Debug.LogWarning("플레이어나 몬스터는 가방에 넣을 수 없습니다.");
            return false;
        }
        if (_detectedBackPack.CallPutItem(playerLTDController.LiftedLTDObject))
        {
            playerLTDController.CallPutItem();
            return true;
        }
        return false;
    }

    void OnTriggerEnter2D(Collider2D collision)
    {
        if(!isServer)
        {
            return;
        }
        if (collision.CompareTag("TeleportPoint"))
        {
            _teleportPoint = collision.GetComponent<TeleportPoint>();
            _teleportTimer = 0f;
            _teleportPoint.RpcUpdateTeleportProgressBar(connectionToClient, 0f);
        }
    }

    void OnTriggerExit2D(Collider2D collision)
    {
        if(!isServer)
        {
            return;
        }
        if (collision.CompareTag("TeleportPoint"))
        {
            _teleportPoint?.RpcUpdateTeleportProgressBar(connectionToClient, 0f);
            _teleportPoint = null;
            _teleportTimer = 0f;
        }
    }

    [TargetRpc]
    public void RpcPlayerTeleported(NetworkConnection target)
    {
        CinemachineVirtualCamera vcam = _playerController.PlayerCamera;
        if (vcam != null)
        {
            CinemachineTransposer transposer = vcam.GetCinemachineComponent<CinemachineTransposer>();
            if (transposer != null)
            {
                float originalX = transposer.m_XDamping;
                float originalY = transposer.m_YDamping;
                float originalZ = transposer.m_ZDamping;

                transposer.m_XDamping = 0;
                transposer.m_YDamping = 0;
                transposer.m_ZDamping = 0;

                // 즉시 업데이트
                vcam.PreviousStateIsValid = false;
                Camera.main.GetComponent<CinemachineBrain>()?.ManualUpdate();

                // 댐핑 복원
                transposer.m_XDamping = originalX;
                transposer.m_YDamping = originalY;
                transposer.m_ZDamping = originalZ;
            }
        }
    }
    #region 상호작용 프롬프트 RPC

    /// <summary>
    /// 클라이언트에 상호작용 프롬프트 표시를 요청합니다
    /// </summary>
    [TargetRpc]
    private void RpcShowInteractionPrompt(NetworkConnection target, InteractionInfo info)
    {
        if (InteractionPromptManager.Instance == null)
        {
            return;
        }

        InteractionPromptManager.Instance.ShowPromptFromServer(info);
    }

    /// <summary>
    /// 클라이언트에 상호작용 프롬프트 숨김을 요청합니다
    /// </summary>
    [TargetRpc]
    private void RpcHideInteractionPrompt(NetworkConnection target, uint targetNetId)
    {
        if (InteractionPromptManager.Instance == null)
        {
            return;
        }

        InteractionPromptManager.Instance.HidePrompt(targetNetId);
    }

    #endregion
}