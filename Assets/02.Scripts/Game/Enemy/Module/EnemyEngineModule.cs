using Mirror;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 적 엔진 모듈의 동작을 관리
/// [조우석 - 2025.07.11.]
/// </summary>
public class EnemyEngineModule : EnemyModuleController
{
    [Server]
    public override void SetModuleDestroy()
    {
        _enemyController.TakeDamage(MaxHealth * 2, Vector2.zero);
        _moduleHolder.OnEngineDestroyed(_index);
    }
}
