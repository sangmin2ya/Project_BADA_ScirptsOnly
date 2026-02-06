using Mirror;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 내부 적이 죽었을 때의 상태
/// [조우석 - 2025.08.14.]
/// </summary>
public class InternalDieState : InternalBaseState
{
    [SerializeField] private float _destroyCooldown = 2;

    public override void Enter()
    {
        base.Enter();

        _internalController.RequestDieAndCleanUp(_destroyCooldown);
    }

    
}
