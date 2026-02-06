using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 일정 시간 동안 배회하다가 폭파하여 배에 데미지를 주는 상태
/// [조우석 - 2025.10.22]
/// </summary>
public class InternalExplodeAfterWanderState : InternalWanderState
{
    private KogmawController _kogmawController;

    public override void Enter()
    {
        base.Enter();

        if (_kogmawController == null)
        {
            _kogmawController = _internalController as KogmawController;
        }

        // 자폭 카운트다운 시작
        if (_kogmawController != null && _kogmawController.isServer)
        {
            _kogmawController.StartExplosionCountdown();
        }
    }
}
