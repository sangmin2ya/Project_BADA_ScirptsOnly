using UnityEngine;
using Mirror;
/// <summary>
/// 임시 건물 컨트롤러 - 고정스폰되는 파밍지
/// [박상민 - 2025.07.09]
/// </summary>
public class TempBuilding : NetworkBehaviour, IDamageable, IEnterable
{
    [Header("건물 설정")]
    [Tooltip("건물 최대체력")]
    [SyncVar]
    [SerializeField] private float _maxHealth = 100f;
    [Tooltip("건물의 체력")]
    [SyncVar]
    [SerializeField] private float _health;
    [Header("무게 설정")]
    [Tooltip("건물의 무게")]
    [SerializeField] private uint _mass = 1000; // 예시로

    public uint Mass => _mass;

    void Awake()
    {
        _health = _maxHealth;
    }
    public void TakeDamage(float damage, Vector2 hitPoint)
    {
        _health -= damage;
        if (_health <= 0)
        {
            NetworkServer.Destroy(gameObject); // 건물이 파괴되면 제거
        }
    }

    public void DestroySelf()
    {
        if (isServer)
        {
            NetworkServer.Destroy(gameObject);
        }
    }
}