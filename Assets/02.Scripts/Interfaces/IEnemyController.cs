using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 적들을 컨트롤하는 책임을 가짐
/// [조우석 - 2025.08.12.]
/// </summary>
public interface IEnemyController : ITargetDetector
{
    EnemyStateMachine StateMachine { get; }
    Rigidbody2D Rigidbody { get; }
    GameObject gameObject { get; } // TryGetComponent를 위해 필요
    Transform transform { get; }   // 위치/회전 접근을 위해 필요
    bool HasCollidedWithWallThisFrame { get; }
}
