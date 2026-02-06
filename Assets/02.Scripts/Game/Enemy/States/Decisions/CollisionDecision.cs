using UnityEngine;

/// <summary>
/// 특정 오브젝트와 충돌했을 때 true를 반환하는 Decision
/// [조우석 - 2025.08.13.]
/// </summary>
[CreateAssetMenu(menuName = "ScriptableObjects/Enemy/Decisions/Collision Decision", fileName = "Decision_")]
public class CollisionDecision : Decision
{
    [Tooltip("감지할 충돌의 종류")]
    [SerializeField] private CollisionType _collisionType;

    public enum CollisionType
    {
        // 충돌로 감지해야하는 오브젝트 종류가 추가될 때마다 enum을 추가하고 로직 추가 필요
        벽
    }

    public override bool Decide(IEnemyController controller)
    {
        bool result = false;
        switch (_collisionType)
        {
            case CollisionType.벽:
                result = controller.HasCollidedWithWallThisFrame;
                break;
        }
        return result;
    }
}