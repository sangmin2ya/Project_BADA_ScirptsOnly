using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using static DistanceDecision;

/// <summary>
/// 항상 True를 반환하여 무조건 다음 상태로 넘어가도록 하는 Decision
/// [조우석 - 2025.08.13.]
/// </summary>
[CreateAssetMenu(menuName = "ScriptableObjects/Enemy/Decisions/True Decision", fileName = "Decision_")]
public class TrueDicision : Decision
{
    public override bool Decide(IEnemyController controller)
    {
        return true;
    }
}
