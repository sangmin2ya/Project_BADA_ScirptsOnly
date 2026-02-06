using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 특정한 종류의 타겟을 감지하는 객체를 위한 인터페이스
/// [조우석 - 2025.08.07.]
/// </summary>
public interface ITargetDetector
{
    /// <summary>
    /// 현재 감지된 타겟. 타겟이 없으면 null을 반환
    /// </summary>
    Transform CurrentTarget { get; }

    /// <summary>
    /// 현재 타겟을 감지하고 있는지 여부. CurrentTarget != null 보다 효율적일 수 있습니다.
    /// </summary>
    bool HasTarget { get; }

    /// <summary>
    /// 타겟을 감지하는 로직을 수행합니다. 
    /// (주기적으로 Update에서 호출되거나, 특정 상태에서 한 번 호출될 수 있습니다)
    /// </summary>
    void DetectTarget();
}
