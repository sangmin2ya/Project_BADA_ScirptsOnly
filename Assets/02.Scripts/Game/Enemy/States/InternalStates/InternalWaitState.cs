using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class InternalWaitState : InternalIdleState, ITimeTrackableState
{
    public float _timer;

    public float Timer => _timer;

    public override void Enter()
    {
        base.Enter();
        _timer = 0;
    }

    public override void StateUpdate()
    {
        base.StateUpdate();
        _timer += Time.deltaTime;
    }
}
