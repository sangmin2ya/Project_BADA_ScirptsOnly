using UnityEngine;
using Mirror;
using DG.Tweening;
using Telepathy;
using Unity.Mathematics;

/// <summary>
/// 드릴 모듈 - 함선에 장착되어 적함에 도킹하는 모듈
/// [박상민 - 2025.07.02]
/// </summary>
public class Drill : Module
{
    [Header("드릴 설정")]
    [Tooltip("드릴의 사거리")]
    [SerializeField] private float _drillRange = 5f;
    [Tooltip("드릴 충전속도")]
    [SerializeField] private float _chargeSpeed = 1f;
    [Tooltip("박혔을때 최대 드릴 연장거리")]
    [SerializeField] private float _maxDockingDistance = 10f; // 도킹시 최대 연장거리
    [Tooltip("끌어당기는 속도")]
    [SerializeField] private float _pullSpeed = 20f; // 끌어당기는 속도
    [Tooltip("끌어당기는 최저속도")]
    [SerializeField] private float _minPullSpeed = 5f; // 끌어당기는 최소 속도
    [Tooltip("끌어당기는 회전속도")]
    [SerializeField] private float _pullRotationSpeed = 50f; // 끌어당기는 회전 속도
    [Tooltip("끌어당기는 최저 회전속도")]
    [SerializeField] private float _minPullRotationSpeed = 10f; // 끌어당기는 최소 회전 속도

    [Header("드릴 참조")]
    [Tooltip("드릴")]
    [SerializeField] private GameObject _drillObject;
    [Tooltip("에임 타겟")]
    [SerializeField] private Transform _aimPoint = null;
    [Tooltip("드릴과 모듈을 잇는 사슬")]
    [SerializeField] private LineRenderer _lineRenderer = null;
    [Tooltip("박힌 드릴이 돌아와서 고정될 위치")]
    [SerializeField] private Transform _dockingPos = null;
    [Tooltip("드릴이 박힐때 생성될 텔레포트 포인트 프리팹")]
    [SerializeField] private GameObject _teleportPointPrefab = null;

    [Header("도킹 상대")]
    [Tooltip("드릴이 박힌 벽")]
    [SerializeField] private DockableWall _drilledWall = null; // 도킹 대상 오브젝트
    [Tooltip("도킹 대상, 움직이는 적함 혹은 고정된 건물 2종류")]
    [SyncVar(hook = nameof(OnDockingTargetChanged))]
    [SerializeField] private uint _dockingTargetNetId = 0;

    // 내부 로컬 변수
    private GameObject _dockingTarget; // 도킹 대상 오브젝트
    private Vector2 _drillInitialLocalPos; // 드릴의 초기 Transform
    private Vector2 _aimInitialLocalPos; // 에임 타겟의 초기 Transform
    private float _currentRange = 0f; // 현재 충전 사거리
    private bool _isCharging = false; // 드릴 충전 중인지 여부
    private float _targetRigidbodyMass; // 도킹 대상의 리지드바디 무게
    private int _isHeavierThanMe // 도킹 대상이 더 무거운지 여부
    {
        get
        {
            if (_dockingTarget == null || !_dockingTarget.TryGetComponent<IEnterable>(out IEnterable dockingTargetEnterable))
            {
                Debug.LogWarning("Drill: 도킹 대상이 없거나 IEnterable을 구현하지 않았습니다.");
                return 0; // 도킹 대상이 없거나 IEnterable을 구현하지 않은 경우
            }
            if (_shipController == null)
            {
                Debug.LogWarning("Drill: ShipController가 null입니다.");
                return 0; // ShipController가 null인 경우
            }
            // 도킹 대상의 무게와 함선의 무게 비교
            uint dockingTargetMass = dockingTargetEnterable.Mass;
            uint shipMass = _shipController.Mass;
            return dockingTargetMass > shipMass ? 1 : (dockingTargetMass < shipMass ? -1 : 0);
        }
    }
    private TeleportPoint _teleportPointAtController; // 텔레포트 포인트
    private TeleportPoint _teleportPointAtDrill; // 도킹된 드릴의 텔레포트 포인트

    // 동기화 변수
    [SyncVar] private bool _isLaunching = false; // 드릴 발사 중인지 여부
    [SyncVar] private bool _isStucked = false; // 드릴이 적함에 도킹되었는지 여부
    [SyncVar] private bool _isPulling = false; // 끌어당기기 중인지 여부
    [SyncVar] private bool _isConnected = false; // 드릴이 모두 당겨져 두 객체가 연결되었는지 여부

    // 접근프로퍼티
    public bool IsStucked => _isStucked; // 외부에서 도킹 여부 확인 가능

    protected override void Awake()
    {
        base.Awake();

        _moduleType = ModuleType.드릴;
        _drillInitialLocalPos = _drillObject.transform.localPosition;
        _aimInitialLocalPos = _aimPoint.transform.localPosition;
        _aimPoint.gameObject.SetActive(false); // 처음에 에임점 비활성화
    }

    void Update()
    {
        LinkDrillWithLine();

        if (!isServer)
        {
            return;
        }

        ChargeDrill(); // 드릴 충전 (내부에서 입력 조건 처리)

        if (_isStucked) // 드릴이 고정된 상태일때
        {
            if (_isAbility1Activated) // 끌어당김
            {
                StartPullDockingTarget();
            }
            else
            {
                StopPullingDockingTarget(); // 끌어당기기 중지
            }

            if (_isAbility2Activated) // 도킹 해제
            {
                Undock();
            }
        }
    }

    void FixedUpdate()
    {
        if (isServer)
        {
            if (_isPulling)
            {
                PullDockingTarget(); // 도킹 대상 끌어당기기
            }

            if (_isConnected)
            {
                ConnectDockingTarget();
            }

            //드릴 위치 동기화 (물리적으로 동작하는 오브젝트이기에 FixedUpdate에서 적용)
            if (_isStucked)
            {
                if (_drilledWall != null)
                {
                    // 도킹된 벽의 위치로 드릴 위치 업데이트
                    _drillObject.transform.position = _drilledWall.DrillPivot.position;
                    _drillObject.transform.rotation = _drilledWall.DrillPivot.rotation;


                    // 사정거리 밖이면 도킹해제
                    if (Vector2.Distance(transform.position, _drillObject.transform.position) > _maxDockingDistance)
                    {
                        Undock(); // 도킹 해제 명령
                    }
                }
                else
                {
                    Debug.LogWarning("Drill: 도킹된 벽이 null입니다.");
                }
            }
        }
    }

    void LateUpdate()
    {
        if (!isServer)
        {
            return; // 서버에서만 위치 동기화 처리
        }
        ////드릴 위치 동기화
        //if (_isStucked)
        //{
        //    if (_drilledWall != null)
        //    {
        //        // 도킹된 벽의 위치로 드릴 위치 업데이트
        //        _drillObject.transform.position = _drilledWall.DrillPivot.position;
        //        _drillObject.transform.rotation = _drilledWall.DrillPivot.rotation;


        //        // 사정거리 밖이면 도킹해제
        //        if (Vector2.Distance(transform.position, _drillObject.transform.position) > _maxDockingDistance)
        //        {
        //            CmdUndock(); // 도킹 해제 명령
        //        }
        //    }
        //    else
        //    {
        //        Debug.LogWarning("Drill: 도킹된 벽이 null입니다.");
        //    }
        //}
    }

    #region 드릴충전, 발사

    /// <summary>
    /// 드릴과 모듈을 연결하는 선을 그리는 함수 - 나중에 사슬로 표현할것
    /// </summary>
    private void LinkDrillWithLine()
    {
        if (_lineRenderer == null || _drillObject == null)
        {
            return;
        }

        // 드릴과 도킹된 벽의 위치를 연결
        _lineRenderer.SetPosition(0, (Vector2)_drillObject.transform.position);
        _lineRenderer.SetPosition(1, (Vector2)transform.position);
    }

    /// <summary>
    /// 드릴을 발사하기전에 충전하는 함수, 충전 후 놓을때 발사
    /// </summary>
    private void ChargeDrill()
    {
        if (!isServer || _isLaunching || _isStucked)
        {
            return; // 권한이 없거나 이미 발사 중거나 도킹된 상태인 경우
        }
        if (_isAbility1Activated)
        {
            if (!_isCharging)
            {
                _isCharging = true; // 충전 시작
                RpcToggleAimPoint(_playerModuleController.connectionToClient, true); // 에임점 토글
            }
            if (_currentRange < _drillRange)
            {
                _currentRange += _chargeSpeed * Time.deltaTime; // 충전 속도에 따라 사거리 증가
                RpcShowAimPoint(_playerModuleController.connectionToClient, _aimInitialLocalPos.y + _currentRange);
            }
        }
        else
        {
            if (_isCharging)
            {
                // 충전이 완료되면 드릴 발사
                LaunchDrill(_currentRange);
                _isCharging = false; // 충전 상태 초기화
                RpcToggleAimPoint(_playerModuleController.connectionToClient, false); // 에임점 토글
                RpcShowAimPoint(_playerModuleController.connectionToClient, _aimInitialLocalPos.y);
            }
            // 충전 중이 아닐 때 사거리 초기화
            _currentRange = 0f;
        }
    }
    /// <summary>
    /// 드릴을 발사하는 함수 - 서버에서 발사 실행
    /// </summary>
    private void LaunchDrill(float range)
    {
        Debug.Log($"Drill: 발사 요청, 사거리: {range}");
        if (_isLaunching || range <= 0f)
        {
            return; // 이미 발사 중이거나 충전이 완료되지 않은 경우
        }

        _isLaunching = true;
        _drillObject.GetComponent<DrillPoint>().ToggleCollider(true); // 드릴 발사시 콜라이더 활성화
        StartDrillAnimation(range);
    }

    /// <summary>
    /// 모든 클라이언트에서 드릴 애니메이션 실행
    /// </summary>
    private void StartDrillAnimation(float range)
    {
        // 드릴을 앞으로 이동시키는 애니메이션
        _drillObject.transform.DOLocalMoveY(_drillInitialLocalPos.y + range, 0.2f)
            .OnComplete(() =>
            {
                // 0.5초 대기 후 복귀
                DOVirtual.DelayedCall(0.5f, () =>
                {
                    _drillObject.transform.DOLocalMoveY(_drillInitialLocalPos.y, 1f)
                        .OnComplete(() =>
                        {
                            if (isServer)
                            {
                                ServerResetDrill();
                            }
                        });
                });
            });
    }

    #endregion

    #region 도킹, 해제

    /// <summary>
    /// 도킹하라!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
    /// </summary>
    /// <param name="targetWall">도킹할 대상 그 자체 GameObject</param>
    [Server]
    public void DockToTarget(DockableWall targetWall)
    {
        if (targetWall == null || _isStucked)
        {
            Debug.Log("Drill: 도킹 대상이 없거나 이미 도킹된 상태입니다.");
            return; // 도킹 대상이 없거나 이미 도킹된 경우
        }
        var EnterableTarget = targetWall.ParentNetworkObject;
        if (EnterableTarget == null)
        {
            Debug.Log("Drill: 도킹 대상이 TempEnemyController를 가지고 있지 않습니다.");
            return; // 도킹 대상이 TempEnemyController를 가지고 있지 않은 경우
        }
        _drilledWall = targetWall; // 도킹 대상 오브젝트 저장
        _dockingTargetNetId = EnterableTarget.netId; // 도킹 대상의 네트워크 ID 저장
        _dockingTarget = GameNetworkManager.GetNetworkComponent<NetworkIdentity>(_dockingTargetNetId)?.gameObject;
        _isStucked = true; // 도킹 상태로 변경
        _drillObject.GetComponent<DrillPoint>().ToggleCollider(false); // 도킹 해제시 콜라이더 비활성화
        StopDrillAnimation(_drilledWall.DrillPivot.position, _drilledWall.DrillPivot.rotation.eulerAngles.z); // 로컬에서 애니메이션 중지
        _targetRigidbodyMass = EnterableTarget.GetComponent<Rigidbody2D>().mass; // 도킹 대상의 리지드바디 무게 저장
    }
    /// <summary>
    /// 실행중이던 드릴 애니메이션을 중지하는 함수 
    /// </summary>
    /// <param name="position">도킹된 벽에 박힐 위치</param>
    private void StopDrillAnimation(Vector2 position, float rotation)
    {
        // 드릴 애니메이션 중지
        _drillObject.transform.DOKill();
        _drillObject.transform.position = position; // 도킹된 벽의 위치로 드릴 위치 업데이트
        _drillObject.transform.rotation = Quaternion.Euler(0, 0, rotation); // 도킹된 벽의 회전으로 드릴 회전 업데이트
    }

    /// <summary>
    /// 도킹 해제
    /// </summary>
    private void Undock()
    {
        if (!_isStucked)
        {
            Debug.Log("Drill: 현재 도킹된 상태가 아닙니다.");
            return; // 현재 도킹된 상태가 아닌 경우
        }

        _isPulling = false; // 끌어당기기 상태 먼저 초기화
        _isConnected = false; // 연결 상태 먼저 초기화
        _isStucked = false; // 도킹 상태 해제

        _dockingTargetNetId = 0; // 도킹 대상의 네트워크 ID 초기화
        _dockingTarget = null; // 도킹 대상 오브젝트 초기화
        _drilledWall = null; // 도킹된 벽 초기화

        ServerResetDrill(); // 드릴 초기화
    }

    /// <summary>
    /// 드릴을 초기 상태로 되돌리는 함수
    /// </summary>
    [Server]
    private void ServerResetDrill()
    {
        Debug.Log("Drill: 도킹 해제 및 초기화");
        _isLaunching = false; // 발사 상태 초기화
        _isStucked = false; // 도킹 상태 해제
        _isPulling = false; // 끌어당기기 상태 초기화
        _isConnected = false; // 연결 상태 초기화
        _dockingTargetNetId = 0; // 도킹 대상 초기화
        _targetRigidbodyMass = 0; // 도킹 대상의 리지드바디 무게 초기화
        _drillObject.GetComponent<DrillPoint>().ToggleCollider(false); // 도킹 해제시 콜라이더 비활성화
        _shipController.SetDockedState(false); // 함선 도킹 상태 해제
        //_shipController.ResetRigidbodyMass(); // 원래 질량으로 복구
        DestroyTeleportPoint(); // 텔레포트 포인트 파괴
        ResetDrill(); // 초기화
    }

    /// <summary>
    /// 로컬에서 드릴을 초기 상태로 되돌리는 함수
    /// </summary>
    private void ResetDrill()
    {
        _currentRange = 0f; // 사거리 초기화
        _isAbility1Activated = false; // 능력 비활성화
        _isCharging = false; // 충전 상태 초기화
        _drillObject.transform.localPosition = _drillInitialLocalPos; // 드릴 초기 위치로 되돌리기
        _drillObject.transform.localRotation = Quaternion.identity; // 드릴 초기 회전으로 되돌리기
        _drillObject.transform.DOKill(); // 드릴 애니메이션 중지
    }

    #endregion

    #region 텔레포트

    /// <summary>
    /// 양쪽에 텔레포트 포인트 활성화 요청
    /// </summary>
    private void CreateTeleportPoint()
    {
        if (_teleportPointPrefab == null)
        {
            return;
        }
        _teleportPointAtDrill = Instantiate(_teleportPointPrefab, (Vector2)_drilledWall.TeleportPivot.position + ShipSpawner.Instance.OriginOffset, quaternion.identity).GetComponent<TeleportPoint>();
        _teleportPointAtController = Instantiate(_teleportPointPrefab, _moduleController.InteractionPosition.position, quaternion.identity).GetComponent<TeleportPoint>();
        _teleportPointAtController.InitTeleportPoint(_teleportPointAtDrill.transform); // 모듈 컨트롤러의 텔레포트 포인트와 연결
        _teleportPointAtDrill.InitTeleportPoint(_teleportPointAtController.transform); // 드릴의 텔레포트 포인트와 연결

        NetworkServer.Spawn(_teleportPointAtDrill.gameObject);
        NetworkServer.Spawn(_teleportPointAtController.gameObject);
        // 부모설정
        _teleportPointAtDrill.GetComponent<NetworkParentSync>().SetNetworkParent(_drilledWall.transform);
        _teleportPointAtController.GetComponent<NetworkParentSync>().SetNetworkParent(_moduleController.transform);
    }

    private void DestroyTeleportPoint()
    {
        if (_teleportPointAtDrill != null)
        {
            NetworkServer.Destroy(_teleportPointAtDrill.gameObject);
            _teleportPointAtDrill = null;
        }
        if (_teleportPointAtController != null)
        {
            NetworkServer.Destroy(_teleportPointAtController.gameObject);
            _teleportPointAtController = null;
        }
    }

    private void OnDockingTargetChanged(uint oldValue, uint newValue)
    {
        // 도킹 대상이 변경되었을 때 호출되는 함수
        if (newValue != 0)
        {
            _dockingTarget = GameNetworkManager.GetNetworkComponent<NetworkIdentity>(newValue)?.gameObject;
            Debug.Log($"Drill: 도킹 대상 변경됨, 새로운 대상: {_dockingTarget}");
        }
        else
        {
            _dockingTarget = null;
            Debug.Log("Drill: 도킹 대상이 해제됨");
        }
    }

    #endregion

    #region 인양
    /// <summary>
    /// 적 함선을 우리쪽으로 끌어당기기
    /// </summary>
    private void StartPullDockingTarget()
    {
        if (!_isStucked || _isConnected || _dockingTarget == null)
        {
            return; // 도킹된 상태가 아니거나 도킹 대상이 없는 경우
        }

        _isPulling = true; // 끌어당기기 시작
    }

    /// <summary>
    /// 끌어당기기 중지
    /// </summary>
    private void StopPullingDockingTarget()
    {
        if (!_isStucked || _isConnected || _dockingTarget == null)
        {
            return; // 도킹된 상태가 아니거나 도킹 대상이 없는 경우
        }

        if (!_isPulling)
        {
            return; // 끌어당기기 중이 아닌 경우
        }

        _isPulling = false; // 끌어당기기 중지
    }

    /// <summary>
    /// 도킹 대상 오브젝트를 끌어당기는 함수
    /// </summary>
    [Server]
    private void PullDockingTarget()
    {
        if (_dockingTarget == null || !_isStucked || !_isPulling)
        {
            return; // 도킹 대상이 없거나 도킹되지 않았거나 끌어당기기 중이 아닌 경우
        }

        if (_isHeavierThanMe == 0)
        {
            Debug.Log("Drill: 무게 설정에 오류가 있습니다.");
            return; // 무게가 같은 경우 끌어당기지 않음
        }

        // Rigidbody2D 컴포넌트 가져오기
        Rigidbody2D myRigidbody = GetComponentInParent<Rigidbody2D>();
        Rigidbody2D targetRigidbody = _dockingTarget.GetComponent<Rigidbody2D>();

        if (myRigidbody == null || targetRigidbody == null)
        {
            Debug.LogWarning("Drill: Rigidbody2D 컴포넌트를 찾을 수 없습니다.");
            return;
        }

        if (_isHeavierThanMe > 0) // 도킹 대상이 더 무거운 경우 - 내가 끌려간다
        {
            Debug.Log("끌어당겨짐");
            PullMyself(myRigidbody);
        }
        else // 도킹 대상이 더 가벼운 경우 - 내가 끌어당긴다
        {
            Debug.Log("끌어당김");
            PullTarget(targetRigidbody);
        }
    }

    /// <summary>
    /// 내가 더 가벼워서 도킹 대상쪽으로 끌려가는 함수
    /// </summary>
    [Server]
    private void PullMyself(Rigidbody2D myRigidbody)
    {
        // 목표 위치 계산
        Vector2 targetPosition = _drillObject.transform.position;

        // 목표 회전 계산 (위치 계산과 동일한 논리 적용)
        float drillRotationZ = _drillObject.transform.rotation.eulerAngles.z;
        float myRotationZ = _dockingPos.rotation.eulerAngles.z;
        float rigidbodyRotationZ = _dockingPos.rotation.eulerAngles.z;

        // 드릴에서 모듈까지의 회전 차이
        float rotationOffset = drillRotationZ - myRotationZ;
        // 목표 회전 = 드릴 회전 - (모듈 회전 - 리지드바디 회전)
        float finalTargetRotationZ = drillRotationZ - (myRotationZ - rigidbodyRotationZ);
        finalTargetRotationZ = NormalizeAngle(finalTargetRotationZ);

        // 위치로 이동하는 힘 계산
        Vector2 direction = (targetPosition - (Vector2)_dockingPos.position).normalized;
        float distance = Vector2.Distance(_dockingPos.position, targetPosition);

        if (distance > 0.1f) // 거리 임계값
        {
            float forceMultiplier = distance * _pullSpeed; // 거리 비례 힘, 최대값 제한
            Vector2 force = direction * Mathf.Max(forceMultiplier, _minPullSpeed);
            myRigidbody.AddForce(force, ForceMode2D.Force);
            Debug.Log("Drill: 도킹힘 : " + force);
        }

        // 회전으로 이동하는 토크 계산
        float currentRotationZ = NormalizeAngle(_dockingPos.rotation.eulerAngles.z);
        float angleDifference = Mathf.DeltaAngle(currentRotationZ, finalTargetRotationZ);

        if (Mathf.Abs(angleDifference) > 1f) // 각도 임계값
        {
            // 각도 차이의 방향을 유지하면서 최소 토크 적용
            float torqueDirection = Mathf.Sign(angleDifference);
            float torqueMagnitude = Mathf.Max(Mathf.Abs(angleDifference) * _pullRotationSpeed, _minPullRotationSpeed);
            float torque = torqueDirection * torqueMagnitude;

            myRigidbody.AddTorque(torque, ForceMode2D.Force);
            Debug.Log("Drill: 도킹회전힘 : " + torque);
        }

        // 연결 조건 확인
        CheckConnectionCondition(_dockingPos.position, targetPosition, currentRotationZ, finalTargetRotationZ);
    }

    /// <summary>
    /// 도킹 대상이 더 가벼워서 내쪽으로 끌어당기는 함수
    /// </summary>
    [Server]
    private void PullTarget(Rigidbody2D targetRigidbody)
    {
        // 목표 위치 계산 (도킹 대상이 나쪽으로 이동)
        Vector2 targetPosition = _dockingPos.position - (_drillObject.transform.position - _dockingTarget.transform.position);

        // 목표 회전 계산
        float myRotationZ = _dockingPos.rotation.eulerAngles.z;
        float drillRotationZ = _drillObject.transform.rotation.eulerAngles.z;
        float dockingTargetRotationZ = _dockingTarget.transform.rotation.eulerAngles.z;

        float rotationDifference = drillRotationZ - dockingTargetRotationZ;
        float finalTargetRotationZ = myRotationZ - rotationDifference;
        finalTargetRotationZ = NormalizeAngle(finalTargetRotationZ);

        // 위치로 이동하는 힘 계산
        Vector2 direction = (targetPosition - targetRigidbody.position).normalized;
        float distance = Vector2.Distance(targetRigidbody.position, targetPosition);

        if (distance > 0.5f) // 거리 임계값
        {
            float forceMultiplier = distance * _pullSpeed; // 거리 비례 힘, 최대값 제한
            Vector2 force = direction * Mathf.Max(forceMultiplier, _minPullSpeed);
            Debug.Log("도킹힘 : " + force);
            targetRigidbody.AddForce(force, ForceMode2D.Force);
        }

        // 회전으로 이동하는 토크 계산
        float currentRotationZ = NormalizeAngle(targetRigidbody.rotation);
        float angleDifference = Mathf.DeltaAngle(currentRotationZ, finalTargetRotationZ);

        if (Mathf.Abs(angleDifference) > 5f) // 각도 임계값
        {
            // 각도 차이의 방향을 유지하면서 최소 토크 적용
            float torqueDirection = Mathf.Sign(angleDifference);
            float torqueMagnitude = Mathf.Max(Mathf.Abs(angleDifference) * _pullRotationSpeed, _minPullRotationSpeed);
            float torque = torqueDirection * torqueMagnitude;

            targetRigidbody.AddTorque(torque, ForceMode2D.Force);
            Debug.Log("도킹회전힘 : " + torque);
        }

        // 연결 조건 확인
        CheckConnectionCondition(targetRigidbody.position, targetPosition, currentRotationZ, finalTargetRotationZ);
    }

    /// <summary>
    /// 연결 조건 확인 함수
    /// </summary>
    [Server]
    private void CheckConnectionCondition(Vector2 currentPosition, Vector2 targetPosition, float currentRotation, float targetRotation)
    {
        float positionDistance = Vector2.Distance(currentPosition, targetPosition);
        float angleDifference = Mathf.Abs(Mathf.DeltaAngle(currentRotation, targetRotation));

        // 위치와 각도가 모두 임계값 내에 있으면 연결 상태로 변경
        if (positionDistance <= 0.5f && angleDifference <= 5f)
        {
            _isPulling = false; // 끌어당기기 중지
            _isConnected = true; // 연결 상태로 변경
            if (_shipController != null)
            {
                if (_isHeavierThanMe > 0)
                {
                    _shipController.SetDockedState(true); // 함선 도킹 상태 설정
                }
                else
                {
                    //_shipController.SetDockedRigidBodyMass(_targetRigidbodyMass); // 도킹 대상의 무게로 설정
                }
            }

            CreateTeleportPoint(); // 양쪽에 텔레포트 포인트 생성      

            Debug.Log("Drill: 끌어당기기 완료, 연결 상태로 전환");
        }
    }

    /// <summary>
    /// 도킹 타겟을 연결 상태로 위치 고정
    /// </summary>
    [Server]
    private void ConnectDockingTarget()
    {
        // 추가 안전 체크
        if (_dockingTarget == null || !_isStucked || !_isConnected)
        {
            return;
        }

        if (_isHeavierThanMe > 0) // 도킹 대상이 더 무거운 경우 - 내가 따라간다
        {
            // PullMyself와 동일한 계산 방식 사용
            Vector2 targetPosition = _drillObject.transform.position - (_dockingPos.position - _agentShip.transform.position);
            float targetRotationZ = NormalizeAngle(_drillObject.transform.rotation.eulerAngles.z -
                NormalizeAngle(_dockingPos.rotation.eulerAngles.z - _agentShip.transform.rotation.eulerAngles.z));

            // Transform으로 직접 위치 고정
            _agentShip.transform.position = targetPosition;
            _agentShip.transform.rotation = Quaternion.Euler(0, 0, targetRotationZ);
        }
        else // 도킹 대상이 더 가벼운 경우 - 도킹 대상이 따라온다
        {
            Vector2 targetPosition = _dockingPos.position - (_drillObject.transform.position - _dockingTarget.transform.position);
            float targetRotationZ = NormalizeAngle(_dockingPos.rotation.eulerAngles.z -
                NormalizeAngle(_drillObject.transform.rotation.eulerAngles.z - _dockingTarget.transform.rotation.eulerAngles.z));

            // Transform으로 직접 위치 고정
            _dockingTarget.transform.position = targetPosition;
            _dockingTarget.transform.rotation = Quaternion.Euler(0, 0, targetRotationZ);
        }
    }
    /// <summary>
    /// 각도를 -180 ~ 180 범위로 정규화하는 함수
    /// </summary>
    private float NormalizeAngle(float angle)
    {
        while (angle > 180f)
        {
            angle -= 360f;
        }
        while (angle < -180f)
        {
            angle += 360f;
        }
        return angle;
    }

    void OnDestroy()
    {
        // 오브젝트가 파괴될 때 텔레포트 포인트도 함께 파괴
        //DestroyTeleportPoint();
    }
    #endregion

    #region 클라이언트 동기화

    /// <summary>
    /// 에임점 토글
    /// </summary>
    [TargetRpc]
    public void RpcToggleAimPoint(NetworkConnection target, bool isActive)
    {
        _aimPoint.gameObject.SetActive(isActive);
    }

    /// <summary>
    /// 에임점 위치 설정
    /// </summary>
    [TargetRpc]
    public void RpcShowAimPoint(NetworkConnection target, float range)
    {
        _aimPoint.localPosition = new Vector3(0f, range, 0f);
    }

    #endregion
}