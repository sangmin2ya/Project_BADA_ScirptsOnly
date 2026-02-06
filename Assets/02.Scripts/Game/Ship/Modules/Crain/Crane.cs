using System.Collections.Generic;
using DG.Tweening;
using Mirror;
using UnityEngine;

/// <summary>
/// 배 외부 크레인 모듈
/// [박상민 - 2025.10.03]
/// </summary>
public class Crane : Module
{
    [Header("크레인 설정")]
    [Tooltip("크레인 후크")]
    [SerializeField] private Transform _craneHook;
    [Tooltip("후크 발사 속도")]
    [SerializeField] private float _hookFireDuration = 0.5f;
    [Tooltip("후크 당기는 속도")]
    [SerializeField] private float _hookPullSpeed = 2f;
    [Tooltip("그물 접는 속도")]
    [SerializeField] private float _hookFoldDuration = 0.1f;
    [Tooltip("후크 최대 거리")]
    [SerializeField] private float _hookMaxDistance = 5f;
    [Tooltip("그물망 크기")]
    [SerializeField] private float _fishingNetSize = 1f;
    [Tooltip("충전 속도")]
    [SerializeField] private float _chargeSpeed = 10f;

    [Header("크레인 회전 설정")]
    [Tooltip("크레인 회전 속도")]
    [SerializeField] private float _rotationSpeed = 100f;
    [Tooltip("크레인 회전 감속 각도")]
    [SerializeField] private float _rotationDampingAngle = 5f;
    [Tooltip("크레인 회전 제한 각도")]
    [SerializeField] private float _rotationLimit = 45f;

    [Header("크레인 참조")]
    [Tooltip("크레인 머리 부분")]
    [SerializeField] private Transform _craneHead;
    [Tooltip("에임점")]
    [SerializeField] private Transform _aimPoint;

    [Header("크레인 상태")]
    [SerializeField] private List<ItemData> _capturedItems = new List<ItemData>();

    private bool _isLaunching = false;
    private bool _isCharging = false;
    private float _currentRange = 0f;

    protected override void Start()
    {
        base.Start();

        _moduleType = ModuleType.크레인;
        _craneHook.localPosition = Vector3.zero;
        _craneHook.localScale = Vector3.zero;
        _aimPoint.gameObject.SetActive(false);
    }

    void Update()
    {
        if (!isServer)
        {
            return;
        }

        if (_isLaunching || !IsOccupied)
        {
            return; // 발사 중일 때는 회전 및 충전 불가
        }
        RotateCrane();
        LimitRotation();
        ChargeCrane();
    }

    private void RotateCrane()
    {
        if (_craneHead == null)
        {
            return;
        }

        // 마우스를 이용하여 조준하고 있는 경우
        if (_aimInput != Vector2.zero)
        {
            // 크레인 헤드에서 목표 지점으로의 직접적인 방향 계산
            Vector2 localAimDirection = _aimInput - ShipSpawner.Instance.OriginOffset;
            Vector2 agentAimPos = _agentShip.transform.TransformPoint((Vector3)localAimDirection);
            Vector2 aimDirection = agentAimPos - (Vector2)_craneHead.position;

            if (aimDirection.sqrMagnitude > 0.05f)
            {
                float targetAngle = Mathf.Atan2(aimDirection.y, aimDirection.x) * Mathf.Rad2Deg;
                float parentRotation = _craneHead.parent.eulerAngles.z;
                float localTargetAngle = targetAngle - parentRotation - 90f;

                ApplyRotationWithEasing(localTargetAngle);
            }
        }
        // 패드를 사용하여 조준하고 있는 경우
        else if (_rotationInput != Vector2.zero)
        {
            float targetAngle = Mathf.Atan2(_rotationInput.y, _rotationInput.x) * Mathf.Rad2Deg;
            float parentRotation = _craneHead.parent.eulerAngles.z;
            float localTargetAngle = targetAngle - parentRotation - 90f;

            ApplyRotationWithEasing(localTargetAngle);
        }
    }

    /// <summary>
    /// 이징을 적용한 회전 처리
    /// </summary>
    private void ApplyRotationWithEasing(float localTargetAngle)
    {
        float currentAngle = _craneHead.localEulerAngles.z;
        float angleDifference = Mathf.DeltaAngle(currentAngle, localTargetAngle);

        // OutQuad 이징 적용
        float normalizedDistance = Mathf.Abs(angleDifference) / 180f; // 0~1로 정규화
        float easingFactor = OutQuadEasing(normalizedDistance);

        float angleToRotate = Mathf.Sign(angleDifference) * easingFactor * _rotationDampingAngle;
        _craneHead.localEulerAngles = new Vector3(0, 0, currentAngle + angleToRotate * _rotationSpeed * Time.deltaTime);
    }

    /// <summary>
    /// OutQuad 이징 함수 (목표에 멀수록 빠르고, 가까워질수록 천천히)
    /// </summary>
    private float OutQuadEasing(float t)
    {
        // 거리가 멀 때(0.3 이상)는 최대 속도로 움직임
        if (t > 0.3f)
        {
            return 1f;
        }

        // 가까워졌을 때만 감속 적용 (0~0.3 구간을 0~1로 재매핑)
        float remappedT = t / 0.3f;
        return 1f - (1f - remappedT) * (1f - remappedT);
    }

    /// <summary>
    /// 회전 각도를 제한하는 함수
    /// </summary>
    private void LimitRotation()
    {
        if (_craneHead == null)
        {
            return;
        }
        float currentAngle = _craneHead.localEulerAngles.z;
        if (currentAngle > 180f)
            currentAngle -= 360f;

        if (currentAngle > _rotationLimit)
            _craneHead.localEulerAngles = new Vector3(0, 0, _rotationLimit);
        else if (currentAngle < -_rotationLimit)
            _craneHead.localEulerAngles = new Vector3(0, 0, -_rotationLimit);
    }

    /// <summary>
    /// 발사하기전에 충전하는 함수, 충전 후 놓을때 발사
    /// </summary>
    private void ChargeCrane()
    {
        if (!isServer || _isLaunching)
        {
            return; // 권한이 없거나 이미 발사 중거나 도킹된 상태인 경우
        }
        if (_isAbility1Activated)
        {
            if (_isCharging == false)
            {
                _isCharging = true; // 충전 시작
                RpcToggleAimPoint(_playerModuleController.connectionToClient, true); // 에임점 토글
            }

            if (_currentRange < _hookMaxDistance)
            {
                _currentRange += _chargeSpeed * Time.deltaTime; // 충전 속도에 따라 사거리 증가
                RpcShowAimPoint(_playerModuleController.connectionToClient, _currentRange);
            }
        }
        else
        {
            if (_isCharging)
            {
                // 충전이 완료되면 드릴 발사
                CastHook(_currentRange);
                _isCharging = false; // 충전 상태 초기화
                RpcShowAimPoint(_playerModuleController.connectionToClient, 0f);
                RpcToggleAimPoint(_playerModuleController.connectionToClient, false); // 에임점 토글
            }
        }
    }

    private void CastHook(float range)
    {
        _isLaunching = true;
        _craneHook.DOScale(Vector3.one * _fishingNetSize, _hookFoldDuration).SetEase(Ease.OutQuart);
        //크레인 발사
        _craneHook.DOLocalMoveY(range, _hookFireDuration).SetEase(Ease.OutQuad).OnComplete(() =>
        {
            _craneHook.GetComponent<Collider2D>().enabled = true;
            //크레인 당기기
            _craneHook.DOLocalMoveY(0f, _currentRange / _hookPullSpeed).SetEase(Ease.OutQuad).OnComplete(() =>
            {
                _craneHook.GetComponent<Collider2D>().enabled = false;
                _craneHook.DOScale(Vector3.zero, _hookFoldDuration).SetEase(Ease.InQuart).OnComplete(() =>
                {
                    //그물망 생성 및 스폰
                    SpawnFishingNet();
                    _isLaunching = false;
                    _currentRange = 0f;
                });
            });
        });
    }

    /// <summary>
    /// 그물에 아이템이 담길때마다 호출
    /// </summary>
    /// <param name="item"></param>
    public void CaptureItem(DummyLTDObject item)
    {
        if (item != null)
        {
            List<ItemData> itemDatas = item.GetRandomItemDatas();
            _capturedItems.AddRange(itemDatas);
            NetworkServer.Destroy(item.gameObject);
        }
    }

    /// <summary>
    /// 그물망 LTD오브젝트 생성 및 스폰
    /// </summary>
    private void SpawnFishingNet()
    {
        if (_capturedItems.Count == 0)
        {
            return;
        }
        ItemData netData = new ItemData();
        netData.itemType = LTDObjectType.그물망;

        FishingNetObject fishingNet = Instantiate(ItemManager.Instance.GetItemPrefab(netData), ModuleController.transform.position, Quaternion.identity).GetComponent<FishingNetObject>();

        foreach (var itemData in _capturedItems)
        {
            fishingNet.AddCaughtObject(itemData);
        }
        NetworkServer.Spawn(fishingNet.gameObject);
        _capturedItems.Clear();
    }

    override public void PlayerExited()
    {
        if (!isServer)
        {
            return;
        }
        //내리면 현재 충전중인거 취소
        _isAbility1Activated = false;
        _isCharging = false;
        _currentRange = 0f;
        RpcToggleAimPoint(_playerModuleController.connectionToClient, false); // 에임점 토글
        RpcShowAimPoint(_playerModuleController.connectionToClient, 0f);

        base.PlayerExited();
    }

    #region 시각 동기화

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