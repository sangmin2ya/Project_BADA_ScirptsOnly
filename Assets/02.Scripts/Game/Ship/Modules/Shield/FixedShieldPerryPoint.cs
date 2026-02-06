using Mirror;
using UnityEngine;

/// <summary>
/// 고정 방어막 패링 포인트
/// [박상민 - 2025.08.24]
/// </summary>
public class FixedShieldParryPoint : NetworkBehaviour
{
    [SerializeField] private FixedShield _parentShield;

    private SpriteRenderer _spriteRenderer;
    private CircleCollider2D _collider2D;

    void Awake()
    {
        _spriteRenderer = GetComponent<SpriteRenderer>();
        _collider2D = GetComponent<CircleCollider2D>();
        _spriteRenderer.enabled = false;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if(!isServer) return;
        // 총알 태그 확인
        if (other.gameObject.CompareTag("Bullet"))
        {
            _parentShield.OnShieldTriggerEnter2D(other);
        }
    }

    public void Initialize(FixedShield parentShield, float shieldRadius)
    {
        _parentShield = parentShield;
        _collider2D.radius = shieldRadius;
        _collider2D.isTrigger = true;
        _collider2D.enabled = false;
    }

    public void EnableCollider(bool enable)
    {
        _collider2D.enabled = enable;
    }

    [ClientRpc]
    public void RpcToggleShieldVisual(bool show, Color color)
    {
        _spriteRenderer.enabled = show;

        if (show)
        {
            _spriteRenderer.color = color;
        }
    }
}
