using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

/// <summary>
/// FaceHugger가 플레이어에게 붙은 상태
/// [조우석 - 2025.11.20.]
/// </summary>
public class InternalHuggingState : InternalIdleState
{
    private FaceHuggerController _faceHuggerController;

    public override void Enter()
    {
        base.Enter();
        if (_faceHuggerController == null)
        {
            _faceHuggerController = _internalController as FaceHuggerController;
        }
    }

    public override void Exit()
    {
        base.Exit();
    }

    public override void StateUpdate()
    {
        transform.position = _faceHuggerController.PlayerHead.position;
    }
}
