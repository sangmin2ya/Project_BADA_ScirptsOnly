using UnityEngine;

[CreateAssetMenu(fileName = "New Enemy Data", menuName = "ScriptableObjects/Enemy Data")]
public class EnemyStatData : ScriptableObject
{
    [Header("Health")]
    //[Tooltip("최대 체력")]
    //public float MaxHealth;

    [Header("Move")]
    //[Tooltip("기본 이동 속도")]
    //public float MoveSpeed;
    //[Tooltip("추격 속도")]
    //public float ChaseSpeed;
    //[Tooltip("조향 회전 속도")]
    //public float RotationSpeed;
    [Tooltip("도망치는 거리")]
    public float RunawayDistance;

    [Header("Attack")]
    [Tooltip("공격 최소 범위")]
    public float AttackRangeMin;
    [Tooltip("공격 최대 범위")]
    public float AttackRangeMax;

    [Header("Detection")]
    //[Tooltip("플레이어 감지 범위")]
    //public float DetectionRadius;
    //[Tooltip("타겟 감지 레이어")]
    //public LayerMask TargetLayer;
    //public float LoseTargetRadius = 50f; // 타겟 상실 허용 거리
    
    [Header("Scanning Performance")]
    public float ScanInterval = 1.0f;
    public float ValidationInterval = 0.2f;

    [Header("Internal Enemy Targeting")]
    [Tooltip("타겟을 잡는 우선순위 기준")]
    public TargetingPriority Priority = TargetingPriority.가까운대상;
    [Tooltip("더 나은 타겟이 있어도 변경할 수 있는가")]
    public bool CanSwitchToBetterTarget = false;

    
}
