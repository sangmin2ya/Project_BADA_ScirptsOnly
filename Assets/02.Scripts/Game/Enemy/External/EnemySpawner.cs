// using UnityEngine;

// /// <summary>
// /// 플레이어 미니맵에 들어왔을때 적을 스폰해주는 메소드 (최적화용) 
// /// </summary>
// public class EnemySpawner : MonoBehaviour
// {
//     [Header("적 스폰 설정")]
//     [SerializeField] private EnemyShipType _enemyShipType; // 스폰할 적 함선 타입
//     [SerializeField] private float _spawnCooldown = 60f; // 스폰 쿨타임
//     [SerializeField] private string _detectTag = "EnemyDetector";
//     [SerializeField] private bool _isOnlySpawnOnNight = false; // 밤에만 스폰할지 여부
//     [SerializeField] private bool _isOnlySpawnOnMidNight = false; // 한밤중에만 스폰할지 여부

//     private float _spawnCooldownTimer = 0f; // 스폰 쿨타임 타이머

//     [Header("디버그용")]
//     public bool _showGizmos = true; // 기즈모 표시 여부
//     public float _gizmoSize = 50f; // 기즈모 크기

//     void Update()
//     {
//         if (_spawnCooldownTimer > 0)
//         {
//             _spawnCooldownTimer -= Time.deltaTime;
//         }
//     }

//     void SpawnEnemy()
//     {
//         if (_isOnlySpawnOnNight && !SessionManager.Instance.IsNight)
//         {
//             return;
//         }
//         if (_isOnlySpawnOnMidNight && !SessionManager.Instance.IsMidNight)
//         {
//             return;
//         }
//         if (_spawnCooldownTimer <= 0)
//         {
//             // 호스트라서 스폰에 성공하면 초기화
//             if (NetworkObjectSpawner.Instance.RequestSpawnEnemy(_enemyShipType, transform.position, Quaternion.identity))
//             {
//                 _spawnCooldownTimer = _spawnCooldown;
//             }
//         }
//     }

//     void OnTriggerEnter2D(Collider2D collision)
//     {
//         if (collision.CompareTag(_detectTag))
//         {
//             SpawnEnemy();
//         }
//     }

//     private void OnDrawGizmos()
//     {
//         if (!_showGizmos) return;

//         // 타입에 따른 색상 설정
//         switch (_enemyShipType)
//         {
//             case EnemyShipType.전투함선:
//                 Gizmos.color = Color.red; // 빨강
//                 break;
//             case EnemyShipType.도주함선:
//                 Gizmos.color = Color.blue; // 파랑
//                 break;
//             case EnemyShipType.자코함선:
//                 Gizmos.color = Color.yellow; // 노랑
//                 break;
//             case EnemyShipType.잠수함:
//                 Gizmos.color = Color.green; // 초록
//                 break;
//             case EnemyShipType.미니함선:
//                 Gizmos.color = new Color(1f, 0.5f, 0f); // 주황 (RGBA 직접 지정)
//                 break;
//             default:
//                 Gizmos.color = Color.white; // 기본값
//                 break;
//         }

//         Gizmos.DrawSphere(transform.position, _gizmoSize);
//     }
// }
