using UnityEngine;
using Mirror;
using System.Collections.Generic;

/// <summary>
/// 함선 주위에 작은 함선을 스폰하는 스크립트
/// [박상민 - 2025.07.18]
/// </summary>
public class MiniEnemySpawner : NetworkBehaviour
{
    [Header("MiniEnemy 스폰 설정")]
    [Tooltip("스폰할 MiniEnemy의 프리팹")]
    [SerializeField] private GameObject _miniEnemyPrefab;
    [Tooltip("스폰할 MiniEnemy의 수")]
    [SerializeField] private int _maxMiniEnemyCount = 3;
    [Tooltip("스폰 쿨타임")]
    [SerializeField] private float _spawnCooldown = 10f;
    [Tooltip("스폰 포인트")]
    [SerializeField] private Transform _spawnPoint;
    [Tooltip("최대 스폰 횟수")]
    [SerializeField] private int _maxSpawnCount = 4;

    private float _spawnTimer;
    private List<GameObject> _miniEnemies = new List<GameObject>();
    private EnemyControllerBase _enemyController;
    private int _currentSpawnCount = 0;
    private bool _isSpawnable => _miniEnemies.Count < _maxMiniEnemyCount && _spawnTimer >= _spawnCooldown && _enemyController.HasTarget && _currentSpawnCount < _maxSpawnCount;
    public override void OnStartServer()
    {
        base.OnStartServer();
        _enemyController = GetComponent<EnemyControllerBase>();
        _spawnTimer = 0f;
    }
    void Update()
    {
        if (!isServer)
        {
            return; // 서버에서만 실행
        }

        if (_enemyController.HasTarget)
        {
            if (_spawnTimer <= _spawnCooldown)
            {
                _spawnTimer += Time.deltaTime;
            }
        }
        else
        {
            _spawnTimer = 0f;
        }

        if (_isSpawnable)
        {
            SpawnMiniEnemies();
        }
    }

    /// <summary>
    /// MiniEnemy를 스폰하는 메서드
    /// </summary>
    [Server]
    private void SpawnMiniEnemies()
    {
        GameObject miniEnemy = Instantiate(_miniEnemyPrefab, _spawnPoint.position, Quaternion.identity);
        NetworkServer.Spawn(miniEnemy);
        _miniEnemies.Add(miniEnemy);
        _spawnTimer = 0f; // 쿨타임 초기화
        _currentSpawnCount++;
    }
}