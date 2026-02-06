using UnityEngine;

/// <summary>
/// 데미지를 받을 수 있는 객체를 위한 인터페이스
/// [박상민 - 2025.07.01]
/// </summary>
public interface IDamageable
{
    /// <summary>
    /// 데미지를 받는 함수
    /// </summary>
    void TakeDamage(float damage, Vector2 hitPoint);

    void DestroySelf();
}