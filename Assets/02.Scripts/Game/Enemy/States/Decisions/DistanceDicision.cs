using UnityEngine;

/// <summary>
/// 
/// </summary>
[CreateAssetMenu(menuName = "ScriptableObjects/Enemy/Decisions/Distance Decision", fileName = "DistanceDecision_")]
public class DistanceDecision : Decision
{
    public enum ComparisonType { LessThan, GreaterThan }

    [Tooltip("비교할 거리 값")]
    [SerializeField] private float _distance = 5f;
    [Tooltip("현재 거리가 설정된 값보다 '작을 때' 참인지, '클 때' 참인지 결정")]
    [SerializeField] private ComparisonType _comparison = ComparisonType.LessThan;

    public override bool Decide(IEnemyController controller)
    {
        if (!controller.HasTarget)
        {
            return false; // 타겟이 없으면 거리 비교는 의미 없음
        }
        
        float distanceToTarget = Vector2.Distance(controller.transform.position, controller.CurrentTarget.position);

        switch (_comparison)
        {
            case ComparisonType.LessThan:
                return distanceToTarget < _distance;
            case ComparisonType.GreaterThan:
                return distanceToTarget > _distance;
            default:
                return false;
        }
    }
}