using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 내부 적의 애니메이션을 컨트롤하는 클래스
/// [조우석 - 2025.08.14.]
/// </summary>
public class InternalEnemyAnimationController : MonoBehaviour
{
    [SerializeField] private Animator _animator;

    private static readonly int IsWalkingHash = Animator.StringToHash("IsWalking");
    private static readonly int IsAttackingHash = Animator.StringToHash("IsAttacking");
    private static readonly int AttackIntroHash = Animator.StringToHash("AttackIntro");
    private static readonly int IsStunnedHash = Animator.StringToHash("IsStunned");
    private static readonly int DeadHash = Animator.StringToHash("Dead");

    /// <summary>
    /// 걷기 상태에 진입하거나 빠져나옴
    /// </summary>
    /// <param name="isWalking"></param>
    public void SetWalk(bool isWalking)
    {
        _animator?.SetBool(IsWalkingHash, isWalking);
        Debug.Log($"Set bool: walk {isWalking}");
    }

    /// <summary>
    /// 공격 상태에 진입하거나 빠져나옴
    /// </summary>
    public void SetAttacking(bool isAttacking)
    {
        _animator?.SetBool(IsAttackingHash, isAttacking);
        Debug.Log($"Set bool: attack {isAttacking}");
    }

    /// <summary>
    /// 공격 인트로 애니메이션을 트리거
    /// </summary>
    public void TriggerAttackIntro()
    {
        _animator?.SetTrigger(AttackIntroHash);
        Debug.Log("Set trigger: attack");
    }

    /// <summary>
    /// 스턴 상태에 진입하거나 빠져나옴
    /// </summary>
    /// <param name="isStunned"></param>
    public void SetStun(bool isStunned)
    {
        _animator?.SetBool(IsStunnedHash, isStunned);
        Debug.Log($"Set bool: stun {isStunned}");
    }

    /// <summary>
    /// 죽음 상태를 트리거
    /// </summary>
    public void TriggerDead()
    {
        _animator?.SetTrigger(DeadHash);
        Debug.Log("Set trigger: dead");
    }
}
