using UnityEngine;

[CreateAssetMenu(menuName = "ScriptableObjects/Enemy/Decisions/Stun Status Decision", fileName = "StunStatusDecision_")]
public class StunStatusDecision : Decision
{
    [Tooltip("true: 스턴 상태일 때 참, false: 스턴 상태가 아닐 때 참")]
    [SerializeField] private bool _isStunned = true;

    public override bool Decide(IEnemyController controller)
    {
        // 컨트롤러의 타입을 묻지 않고, StunHandler 컴포넌트가 있는지 확인합니다.
        if (controller.gameObject.TryGetComponent<InternalEnemyStunHandler>(out var stunHandler))
        {
            // 컴포넌트가 있다면, 스턴 상태를 비교하여 반환합니다.
            return stunHandler.IsStun == _isStunned;
        }

        // StunHandler 컴포넌트가 없는 적(예: ExternalEnemy)은 스턴될 수 없으므로 항상 false를 반환합니다.
        return false;
    }
}