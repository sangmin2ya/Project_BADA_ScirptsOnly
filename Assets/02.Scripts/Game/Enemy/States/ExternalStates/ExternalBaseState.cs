using Mirror;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 외부 적의 상태 머신 AI에서 베이스가 될 State
/// [조우석 - 2025.07.07]
/// </summary>
public abstract class ExternalBaseState : EnemyBaseState
{
    // 자식 상태들이 ExternalEnemyController의 고유 기능에 쉽게 접근할 수 있도록 프로퍼티 제공
    protected ExternalEnemyController _externalController => (ExternalEnemyController)_controller;
}