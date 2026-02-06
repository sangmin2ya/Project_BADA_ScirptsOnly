using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 내부 적의 상태 머신 AI에서 베이스가 될 State
/// [조우석 - 2025.08.07.]
/// </summary>
public abstract class InternalBaseState : EnemyBaseState
{
    // 자식 상태들이 InternalEnemyController의 고유 기능에 쉽게 접근할 수 있도록 프로퍼티 제공
    protected InternalEnemyController _internalController => (InternalEnemyController)_controller;
}
