using UnityEngine;

/// <summary>
/// 
/// </summary>
[CreateAssetMenu(menuName = "ScriptableObjects/Enemy/Decisions/Has Target Decision", fileName = "HasTargetDecision_")]
public class HasTargetDecision : Decision
{
    [Tooltip("true: 타겟이 있을 때 참, false: 타겟이 없을 때 참")]
    [SerializeField] private bool _targetRequired = true;

    public override bool Decide(IEnemyController controller)
    {
        return controller.HasTarget == _targetRequired;
    }
}