using UnityEngine;
using Mirror;
using Cinemachine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// 에이전트 관리 클래스
/// [김도형 - 2025.07.22]
/// </summary>
public class AgentController : NetworkBehaviour, IDamageable
{
    [Header("Agent Settings")]
    [SerializeField] private Camera _agentCamera;
    [SerializeField] private Camera _minimapCamera;
    [Tooltip("에이전트의 전방 회전 피벗")]
    [SerializeField] private Vector2 _forwardGear1CenterOfMass = new Vector2(0, 0.5f);
    [SerializeField] private Vector2 _forwardGear2CenterOfMass = new Vector2(0, 1.0f);
    [SerializeField] private Vector2 _forwardGear3CenterOfMass = new Vector2(0, 1.5f);
    [SerializeField] private Vector2 _boostCenterOfMass = new Vector2(0, 3.0f);
    [Tooltip("에이전트의 후방 회전 피벗")]
    [SerializeField] private Vector2 _backwardCenterOfMass = new Vector2(0, -0.5f);
    [Tooltip("바닥에서부터 배갑판 높이")]
    [SerializeField] private float _heightOffset = 2.5f;
    private Camera _mainCamera;
    private ShipController _shipController;
    private ExtractionManager _extractionManager;
    private Rigidbody2D _rigidbody;
    private int _currentGear = 0;
    private bool _isBoosting = false;
    [SyncVar] private float _shipFloorHeight = 0f;

    void Awake()
    {
        _rigidbody = GetComponent<Rigidbody2D>();
    }

    void OnEnable()
    {
        CinemachineCore.CameraUpdatedEvent.AddListener(OnCinemachineUpdated);
    }

    void OnDisable()
    {
        CinemachineCore.CameraUpdatedEvent.RemoveListener(OnCinemachineUpdated);
    }

    /// <summary>
    /// 에이전트가 데미지를 받으면 본함에 전달
    /// [박상민 - 2025.07.24]
    /// </summary>
    public void TakeDamage(float damage, Vector2 hitPoint)
    {
        _shipController.TakeDamage(damage, hitPoint);
    }

    public void DestroySelf()
    {
        if (isServer)
        {
            NetworkServer.Destroy(gameObject);
        }
    }

    // Start is called before the first frame update
    void Start()
    {
        _shipController = ShipSpawner.Instance?.CurrentShip?.GetComponent<ShipController>();
        _extractionManager = _shipController?.GetComponent<ExtractionManager>();
        _mainCamera = Camera.main;
        if (_mainCamera != null)
        {
            _mainCamera.GetUniversalAdditionalCameraData().renderType = CameraRenderType.Overlay;
        }

        if (_agentCamera != null)
        {
            _agentCamera.GetUniversalAdditionalCameraData().cameraStack.Add(_mainCamera);
        }
    }

    // // Update is called once per frame
    // void LateUpdate()
    // {
    //     //플레이어가 위치한 본체 ShipController의 메인카메라를 기준으로 카메라 위치 동기화
    //     if (_mainCamera == null || _agentCamera == null) return;

    //     _agentCamera.transform.localPosition = _mainCamera.transform.position - (Vector3)TempShipSpawner.Instance.OriginOffset + _mainCamera.transform.up * (_shipFloorHeight * 0.5f);
    //     _agentCamera.orthographicSize = _mainCamera.orthographicSize;
    // }
    
    /// <summary>
    /// Cinemachine 카메라 업데이트 후 에이전트 카메라 동기화
    /// </summary>
    private void OnCinemachineUpdated(CinemachineBrain brain)
    {
        // Cinemachine이 카메라를 업데이트한 직후 실행
        if (_mainCamera == null || _agentCamera == null) return;

        _agentCamera.transform.localPosition = _mainCamera.transform.position
            - (Vector3)ShipSpawner.Instance.OriginOffset
            + new Vector3(0, 0, -10)
            + _mainCamera.transform.up * (_shipFloorHeight * 0.5f);
        _agentCamera.orthographicSize = _mainCamera.orthographicSize;
    }

    // private void LateUpdate()
    // {
    //     if (_mainCamera != null && _agentCamera != null)
    //     {
    //         // 카메라 동기화를 LateUpdate로 이동
    //         _agentCamera.transform.position = _mainCamera.transform.position - (Vector3)(TempShipSpawner.Instance.OriginOffset);
    //         _agentCamera.transform.rotation = _mainCamera.transform.rotation;
    //         _agentCamera.orthographicSize = _mainCamera.orthographicSize;
    //     }
    // }

    /// <summary>
    /// 기어에 따라 에이전트의 중심점을 설정합니다.
    /// [박상민 - 2025.08.01]
    /// </summary>
    public void SetCenterOfMass(int gear)
    {
        if (_isBoosting)
        {
            return;
        }
        if (_shipController != null && gear != _currentGear)
        {
            _currentGear = gear;
            switch (gear)
            {
                case 1:
                    _rigidbody.centerOfMass = _forwardGear1CenterOfMass;
                    break;
                case 2:
                    _rigidbody.centerOfMass = _forwardGear2CenterOfMass;
                    break;
                case 3:
                    _rigidbody.centerOfMass = _forwardGear3CenterOfMass;
                    break;
                case 0:
                    _rigidbody.centerOfMass = Vector2.zero; // 기본 중심점
                    break;
                case -1:
                    _rigidbody.centerOfMass = _backwardCenterOfMass;
                    break;
                default:
                    _rigidbody.centerOfMass = Vector2.zero; // 기본 중심점
                    break;
            }
        }
    }

    /// <summary>
    /// 부스터 활성화 상태를 설정하고 중심점이동
    /// [박상민 - 2025.08.01]
    /// </summary>
    public void SetBoosting(bool isBoosting)
    {
        _isBoosting = isBoosting;
        if (_isBoosting)
        {
            _rigidbody.centerOfMass = _boostCenterOfMass;
        }
    }

    [ClientRpc]
    public void RpcChangeHeightOffset(float depth)
    {
        _shipFloorHeight = depth + _heightOffset;
    }

    #region 충돌처리

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Extraction"))
        {
            _extractionManager.StartExtraction();
        }
    }
    
    private void OnTriggerExit2D(Collider2D collision)
    {
        if (collision.CompareTag("Extraction"))
        {
            _extractionManager.StopExtraction();
        }
    }

    #endregion
}
