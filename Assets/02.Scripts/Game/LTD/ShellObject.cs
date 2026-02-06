using Mirror;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 함포에 장전할 탄환 오브젝트
/// [조우석 - 2025.07.30.]
/// </summary>
public class ShellObject : LTDObject, ISaleable
{
    [Header("포탄 관련")]
    [Tooltip("디스폰 대기 시간")]
    [SerializeField] private float _despawnTime = 5;
    private float _despawnTimer = 0;
    [Tooltip("오브젝트 스폰 후 오브젝트에 아무런 상호작용도 없었는가")]
    private bool _isFreshlySpawned = true;
    private bool _checkDespawnTimer => !_isFreshlySpawned && !IsBeingLifted;    // 오브젝트에 상호작용을 한 적 있고, 손에 들려있지 않으면 디스폰 타이머를 체크

    public event Action<uint> OnDirtyObject;        // 오브젝트 첫 상호작용 후 호출되는 메서드

    public bool IsFreshlySpawned { get { return _isFreshlySpawned; } }
    public uint Price => ItemManager.Instance.GetItemPrice(new ItemData { itemType = _ltdObjectType });

    protected override void Awake()
    {
        base.Awake();
        _ltdObjectType = LTDObjectType.포탄; // LTD 오브젝트 타입을 포탄으로 설정
        _ltdObjectSize = LTDObjectSize.소; // 크기는 임시로
    }

    void Start()
    {
        if (!isServer)
        {
            return;
        }
    }

    protected override void Update()
    {
        base.Update();
        //일정 시간 동안 손에 들려있지 않으면 오브젝트 삭제
        // if (_checkDespawnTimer)
        // {
        //     _despawnTimer += Time.deltaTime;
        //     if (_despawnTimer > _despawnTime)
        //     {
        //         NetworkServer.Destroy(gameObject);
        //     }
        // }
        // else
        // {
        //     _despawnTimer = 0;
        // }
    }

    protected override void OnTriggerEnter2D(Collider2D other)
    {
        base.OnTriggerEnter2D(other);
        if (!isServer)
        {
            return;
        }
        if (other.CompareTag("CannonReloadSpot"))
        {
            if (other.TryGetComponent<CannonReloadSpot>(out var reloadSpot))
            {
                if (reloadSpot.CheckCanReload())
                {
                    reloadSpot.TryReload();
                    NetworkServer.Destroy(gameObject);
                }
            }
        }
    }

    public override bool Lifted(PlayerLTDController player)
    {
        bool result = base.Lifted(player);

        // 첫 상호작용 시 활성화
        if (_isFreshlySpawned)
        {
            SetFreshSpawned(false);
        }

        return result;
    }

    public override void Dropped()
    {
        base.Dropped();

        // 첫 상호작용 시 활성화
        if (_isFreshlySpawned)
        {
            SetFreshSpawned(false);
        }
    }

    public override void Thrown(ThrowData throwData, uint throwerNetId)
    {
        base.Thrown(throwData, throwerNetId);

        // 첫 상호작용 시 활성화
        if (_isFreshlySpawned)
        {
            _isFreshlySpawned = false;
        }
    }

    /// <summary>
    /// 최초 생성 후 상호작용 여부에 대해서 적용하는 메서드
    /// </summary>
    private void SetFreshSpawned(bool isFresh)
    {
        _isFreshlySpawned = isFresh;
        if (!isFresh)
        {
            OnDirtyObject?.Invoke(this.netId);
        }
    }
}
