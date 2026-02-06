using UnityEngine;
using Mirror;
/// <summary>
/// 모듈이 설치되는 지점 - 데이터는 함선스크립트 내부에 저장
/// [박상민 - 2025.06.18]
/// </summary>
public class ModulePoint : NetworkBehaviour
{
    [Header("모듈 포인트 설정")]
    [Tooltip("감지 대상 태그")]
    [SerializeField] private string ltdObjectTag = "LTDObject";
    [Tooltip("외부 모듈 설치 피벗")]
    [SerializeField] private Transform _externalModulePivot = null;
    public Transform ExternalModulePivot => _externalModulePivot;
    [Tooltip("내부 모듈 설치 피벗")]
    [SerializeField] private Transform _internalModulePivot = null;
    public Transform InternalModulePivot => _internalModulePivot;
    [Tooltip("모듈 포인트의 순서")]
    [SerializeField] private int _modulePointIndex = -1;
    [SerializeField] private Vector2 _modulePointLocalPosition = Vector2.zero;
    [Tooltip("포인트 비활성화")]
    [SerializeField] private GameObject _repairPoint;
    [SerializeField] private Color _enabledColor = Color.white;
    [SerializeField] private Color _disabledColor = Color.red;

    [Header("모듈 포인트 상태")]
    [Tooltip("설치된 외부 모듈")]
    [SerializeField] private Module _externalModule;
    [Tooltip("설치된 내부 모듈")]
    [SerializeField] private ModuleController _internalModule;
    [SerializeField] private ModuleBoxObject _installedModuleBox;
    public ModuleBoxObject InstalledModuleBox => _installedModuleBox;

    // 모듈이 설치되었는지 여부
    public bool IsInstalled => _installedModuleBox != null;
    private SpriteRenderer _spriteRenderer;
    private ShipController _shipController;
    private bool isPointDisabled = false;
    public bool IsPointDisabled => isPointDisabled;

    void Start()
    {
        _spriteRenderer = GetComponent<SpriteRenderer>();
        if (_spriteRenderer == null)
        {
            Debug.LogError("ModulePoint에 SpriteRenderer가 없습니다.");
        }

        // 내부 모듈 피벗 위치 초기화
        if (_internalModulePivot != null)
        {
            _internalModulePivot.transform.position = transform.position;
            _internalModulePivot.transform.rotation = transform.rotation;
        }

        _repairPoint.SetActive(false); // 시작할 때 비활성화
        _spriteRenderer.color = _enabledColor; // 시작할 때 활성화 색상
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        _shipController = ShipSpawner.Instance.CurrentShip.GetComponent<ShipController>();
        // 설정된 모듈 인덱스를 통해 데이터 연동
        SetModuleIndetData(_modulePointIndex);
    }

    /// <summary>
    /// 생성할때 몇번째 모듈 포인트인지 설정
    /// </summary>
    [Server]
    public void SetModulePointIndex(int index)
    {
        _modulePointIndex = index;
    }

    [Server]
    public void SetModulePointLocalPosition(Vector2 position)
    {
        _modulePointLocalPosition = position;
    }

    /// <summary>
    /// 모듈 포인트 인덱스에 따라 내부 모듈 피벗 위치를 설정
    /// </summary>
    private void SetModuleIndetData(int newIndex)
    {
        if (newIndex == -1)
        {
            return; // 유효하지 않은 인덱스
        }
        //_connectedHatch = _shipController.GetHatchByIndex(newIndex);

        // 모듈 설치지점도 업데이트
        transform.position = _modulePointLocalPosition;

        _internalModulePivot.transform.position = transform.position;
        _internalModulePivot.transform.rotation = transform.rotation;

        Transform externalModulePivot = _shipController.GetExternalModulePivotByIndex(newIndex);
        _externalModulePivot.transform.position = externalModulePivot.transform.position;
        _externalModulePivot.transform.rotation = transform.rotation;
    }

    /// <summary>
    /// 모듈 포인트에 외부 모듈과 내부 모듈을 설정합니다.
    /// </summary>
    [Server]
    public void SetModules(Module externalModule, ModuleController internalModule, ModuleBoxObject moduleBox)
    {
        _externalModule = externalModule;
        _internalModule = internalModule;
        _installedModuleBox = moduleBox;

        // if (ExternalModule == null)
        // {
        //     _spriteRenderer.enabled = true; // 외부 모듈이 없으면 스프라이트 비활성화
        // }
        // else
        // {
        //     _spriteRenderer.enabled = false; // 외부 모듈이 있으면 스프라이트 활성화
        // }
    }

    /// <summary>
    /// 모듈 설치를 비활성화/활성화
    /// </summary>
    [Server]
    public void ToggleModulePoint(bool disable)
    {
        isPointDisabled = !disable;
        RpcToggleModulePointVisual(disable);
    }

    [ClientRpc]
    private void RpcToggleModulePointVisual(bool active)
    {
        if (_repairPoint != null)
        {
            _repairPoint.SetActive(!active);
        }
        if(_spriteRenderer != null)
        {
            _spriteRenderer.color = active ? _enabledColor : _disabledColor;
        }
    }

    // private void OnTriggerEnter2D(Collider2D other)
    // {
    //     if(!isServer)
    //     {
    //         return; // 서버에서만 작동
    //     }
    //     // LTD 오브젝트의 트리거가 감지되면, 해당 LTD 오브젝트의 타입을 확인하고 행동을 수행
    //     if (other.CompareTag(ltdObjectTag))
    //     {
    //         LTDObject otherLTD = other.GetComponent<LTDObject>();
    //         if (otherLTD != null && otherLTD.LtdobjectType == LTDObjectType.모듈)
    //         {
    //             ModuleBoxObject moduleBox = otherLTD.GetComponent<ModuleBoxObject>();
    //             if (moduleBox != null && !IsInstalled)
    //             {
    //                 moduleBox.InstallModule(this);
    //             }
    //         }
    //     }
    // }
}
