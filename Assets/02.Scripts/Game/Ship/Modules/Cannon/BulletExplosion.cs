using Mirror;
using UnityEngine;

/// <summary>
/// bullet이 배와 충돌할 경우 폭발 콜라이더를 생성하고 플레이어와 충돌시 아이템 드롭 및 넉백을 발생시키는 스크립트
/// [박상민 - 2025.09.25]
/// </summary>
public class BulletExplosion : NetworkBehaviour
{
    [SerializeField] private float _explosionDuration = 0.1f; // 폭발 지속 시간
    [SerializeField] private float _knockbackForce = 5f; // 넉백 힘

    private void Awake()
    {
        Invoke(nameof(DestroyBulletAfterLifeTime), _explosionDuration);
    }

    private void DestroyBulletAfterLifeTime()
    {
        NetworkServer.Destroy(gameObject);
    }

    void OnTriggerEnter2D(Collider2D collision)
    {
        if (!isServer) return;
        if (collision.TryGetComponent<PlayerLTDController>(out PlayerLTDController ltdController))
        {
            Vector2 direction = (ltdController.transform.position - transform.position).normalized;
            ltdController.CallForceDropItemAndKnocked(direction, _knockbackForce);
        }
    }
}