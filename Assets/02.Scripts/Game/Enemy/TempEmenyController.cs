using UnityEngine;
using Mirror;
using System.Collections;
/// <summary>
/// TempEnemyController - 임시 적 컨트롤러
/// [박상민 - 2025.07.01]
/// </summary>
public class TempEnemyController : NetworkBehaviour, IDamageable, IEnterable
{
    [Header("적 설정")]
    [Tooltip("적의 체력")]
    [SyncVar]
    [SerializeField] private float _health = 100f;
    [Tooltip("적의 무게")]
    [SerializeField] private uint _mass = 50; // 예시로

    [Header("총알 설정")]
    [Tooltip("총알 발사 주기 (초)")]
    [SerializeField] private float _bulletSpawnInterval = 2f; // 총알 발사 주기
    [Tooltip("총알 속도")]
    [SerializeField] private float _bulletSpeed = 10f; // 총알 속도
    [Tooltip("발사할 총알 프리팹")]
    [SerializeField] private GameObject _bulletPrefab;

    private Transform _shipTransform;
    public uint Mass => _mass; // IEnterable 인터페이스 구현

    public override void OnStartClient()
    {
        base.OnStartClient();

        _shipTransform = ShipSpawner.Instance?.AgentShip?.transform;

        if (isServer)
        {
            // 서버에서만 총알 발사 코루틴 시작
            StartCoroutine(SpawnBulletTowardShip());
        }
    }
    
    public void TakeDamage(float damage, Vector2 hitPoint)
    {
        _health -= damage;
        if (_health <= 0)
        {
            NetworkServer.Destroy(gameObject); // 적이 죽으면 제거
        }
    }

    public void DestroySelf()
    {
        if (isServer)
        {
            NetworkServer.Destroy(gameObject);
        }
    }


    private IEnumerator SpawnBulletTowardShip()
    {
        while (true)
        {
            yield return new WaitForSeconds(_bulletSpawnInterval); // 2초마다 발사

            if (!isServer)
            {
                continue; // 서버에서만 실행
            }

            Vector3 spawnPosition = transform.position;
            Quaternion spawnRotation = Quaternion.LookRotation(Vector3.forward, _shipTransform.position - transform.position);

            // 총알 생성
            GameObject bullet = Instantiate(_bulletPrefab, spawnPosition, spawnRotation);
            Bullet bulletComponent = bullet.GetComponent<Bullet>();
            if (bulletComponent != null)
            {
                NetworkServer.Spawn(bullet);
            }
            else
            {
                Destroy(bullet); // 총알 컴포넌트가 없으면 제거
                continue;
            }
            // 총알이 함선을 향하도록 힘 적용
            Vector2 direction = (_shipTransform.position - transform.position).normalized;
            Vector2 force = direction * _bulletSpeed; // 총알 속도에 따라 힘 계산
            bulletComponent.ApplyForceToProjectile(force);
        }
    }
}