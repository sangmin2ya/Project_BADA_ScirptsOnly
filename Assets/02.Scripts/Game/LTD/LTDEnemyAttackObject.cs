using Mirror;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 내부 적이 던지는 오브젝트에 대한 LTD
/// [조우석 - 2025.08.08]
/// </summary>
public class LTDEnemyAttackObject : LTDObject
{
    private EnemyAttackPhysics _enemyAttackPhysics;
    protected float _despawnTimer = 0;
    [SerializeField] protected float _despawnTime = 5;

    protected override void Awake()
    {
        base.Awake();
        if (!TryGetComponent<EnemyAttackPhysics>(out _enemyAttackPhysics))
        {
            Debug.LogWarning($"{gameObject.name}에 EnemyAttackPhysics 컴포넌트가 존재하지 않습니다.");
        }
    }

    protected override void Update()
    {
        base.Update();

        // 일정 시간 동안 손에 들려있지 않으면 오브젝트 삭제
        if (!IsBeingLifted)
        {
            _despawnTimer += Time.deltaTime;
            if (_despawnTimer > _despawnTime)
            {
                DoDestroy();
            }
        }
        else
        {
            _despawnTimer = 0;
        }
    }

    public override bool Lifted(PlayerLTDController player)
    {
        _enemyAttackPhysics.SetTarget(false, Vector2.zero);

        return base.Lifted(player);
    }

    public void ServerThrownToward(Vector2 thrownVector, uint throwerId, Vector2 targetPosition)
    {
        ThrowData throwData = new ThrowData
        {
            horizontalVelocity = thrownVector,
            verticalVelocity = 0,
            initialHeight = 1.5f    // 일단 임시로 넣은 값
        };
        ThrownWithHeightByEnemy(throwData, throwerId);
        _enemyAttackPhysics.SetTarget(true, targetPosition);
    }

    /// <summary>
    /// LTD 오브젝트 파괴
    /// </summary>
    protected virtual void DoDestroy()
    {
        NetworkServer.Destroy(gameObject); // 오브젝트 제거
    }
}
