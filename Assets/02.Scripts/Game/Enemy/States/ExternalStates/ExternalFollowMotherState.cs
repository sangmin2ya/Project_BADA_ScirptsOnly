using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 모선을 호위하는 호위선 움직임 State
/// [조우석 - 2025.07.15.]
/// </summary>
public class ExternalFollowMotherState : ExternalMoveState
{
    public override void StatePhysicsUpdate()
    {
        if (_controller.CurrentTarget == null || _rigidbody == null)
        {
            return;
        }

        
    }
}
