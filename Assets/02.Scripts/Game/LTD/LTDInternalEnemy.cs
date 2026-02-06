using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 내부 적에 대한 LTD 오브젝트 클래스
/// [조우석 - 2025.08.14]
/// </summary>
public class LTDInternalEnemy : LTDObject
{
    [Header("Internal Enemy")]
    private InternalEnemyStunHandler _stunHandler;
    private Rigidbody2D _rigidbody2D;
    [SerializeField] private PhysicsMaterial2D _ltdPhysicsMaterial;

    protected override void Awake()
    {
        base.Awake();

        _rigidbody2D = GetComponent<Rigidbody2D>();

        if (!TryGetComponent<InternalEnemyStunHandler>(out _stunHandler))
        {
            Debug.LogError($"{gameObject.name}에 InternalEnemyStunHandler 컴포넌트가 없습니다.");
        }
    }

    void OnEnable()
    {
        _rigidbody2D.sharedMaterial = _ltdPhysicsMaterial;
    }

    void OnDisable()
    {
        _rigidbody2D.sharedMaterial = null;
    }

    public override bool Lifted(PlayerLTDController player)
    {
        // 누군가에게 들리면 타이머가 멈춤
        _stunHandler.SetTimerPause(true);

        return base.Lifted(player);
    }

    public override void Dropped()
    {
        base.Dropped();

        // 손에서 떨어지면 스턴 타이머가 흐름
        _stunHandler.SetTimerPause(false);
    }

    public override void Thrown(ThrowData throwData, uint throwerNetId)
    {
        base.Thrown(throwData, throwerNetId);

        // 손에서 떨어지면 스턴 타이머가 흐름
        _stunHandler.SetTimerPause(false);
    }
}
