using UnityEngine;
using Mirror;
using System.Collections;
using System.Collections.Generic;

public class ManualEnemySpawner : NetworkBehaviour
{
    [Header("Ship Reference")]
    [SerializeField] private Transform shipAgent;
    [SerializeField] private string shipAgentName = "ShipAgent";
    [SerializeField] private float searchRetryDelay = 0.5f;
    [SerializeField] private int maxSearchAttempts = 120; // 최대 시도 횟수
    
    [Header("Enemy Prefabs")]
    [SerializeField] private GameObject enemyPrefab1;
    [SerializeField] private GameObject enemyPrefab2;
    [SerializeField] private GameObject enemyPrefab3;
    [SerializeField] private GameObject enemyPrefab4;
    [SerializeField] private GameObject enemyPrefab5;
    
    [Header("Spawn Settings")]
    [SerializeField] private float spawnRadius = 100f;
    
    // 프리팹을 Dictionary로 관리
    private Dictionary<KeyCode, GameObject> enemyPrefabs;
    private bool isSearchingForShip = false;
    private int currentSearchAttempts = 0;
    
    void Start()
    {
        // 키와 프리팹 매핑
        InitializeEnemyPrefabs();
        
        // Ship Agent 찾기 시작
        StartCoroutine(FindShipAgent());
    }
    
    IEnumerator FindShipAgent()
    {
        isSearchingForShip = true;
        currentSearchAttempts = 0;
        
        while (shipAgent == null && currentSearchAttempts < maxSearchAttempts)
        {
            currentSearchAttempts++;
            
            // 1. 이름으로 찾기
            var shipComponent = FindObjectOfType<AgentController>();
            if (shipComponent != null && shipComponent.name.Contains(shipAgentName))
            {
                shipAgent = shipComponent.transform;
                Debug.Log("Ship Agent를 AgentController 컴포넌트로 찾았습니다.");
                break;
            }
            
            Debug.Log($"Ship Agent 검색 시도 {currentSearchAttempts}/{maxSearchAttempts}... {searchRetryDelay}초 후 재시도");
            yield return new WaitForSeconds(searchRetryDelay);
        }
        
        isSearchingForShip = false;
        
        if (shipAgent != null)
        {
            Debug.Log($"Ship Agent 찾기 성공! (시도 횟수: {currentSearchAttempts})");
        }
        else
        {
            Debug.LogError($"Ship Agent를 찾을 수 없습니다! {maxSearchAttempts}번 시도했습니다.");
        }
    }
    
    void InitializeEnemyPrefabs()
    {
        enemyPrefabs = new Dictionary<KeyCode, GameObject>
        {
            { KeyCode.Alpha1, enemyPrefab1 },
            { KeyCode.Alpha2, enemyPrefab2 },
            { KeyCode.Alpha3, enemyPrefab3 },
            { KeyCode.Alpha4, enemyPrefab4 },
            { KeyCode.Alpha5, enemyPrefab5 }
        };
    }
    
    void Update()
    {
        if (!isServer)
        {
            return; // 서버에서만 실행
        }
        
        // 각 키 입력 체크
        CheckKeyInput(KeyCode.Alpha1);
        CheckKeyInput(KeyCode.Alpha2);
        CheckKeyInput(KeyCode.Alpha3);
        CheckKeyInput(KeyCode.Alpha4);
        CheckKeyInput(KeyCode.Alpha5);
    }
    
    void CheckKeyInput(KeyCode key)
    {
        if (Input.GetKeyDown(key))
        {
            if (enemyPrefabs.ContainsKey(key) && enemyPrefabs[key] != null)
            {
                SpawnEnemy(enemyPrefabs[key]);
            }
            else
            {
                Debug.LogWarning($"키 {key}에 할당된 프리팹이 없습니다!");
            }
        }
    }
    
    [Server]
    void SpawnEnemy(GameObject enemyPrefab)
    {
        if (shipAgent == null)
        {
            Debug.LogError("Ship Agent가 설정되지 않았습니다!");
            return;
        }
        
        // 원의 테두리에서 랜덤한 위치 계산
        Vector2 spawnPosition = GetRandomPositionOnCircle();
        
        // 적 생성
        GameObject spawnedEnemy = Instantiate(enemyPrefab, spawnPosition, Quaternion.identity);
        
        // 생성된 적이 Ship을 바라보도록 설정 (옵션)
        Vector2 direction = shipAgent.position - spawnedEnemy.transform.position;
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        spawnedEnemy.transform.rotation = Quaternion.Euler(0, 0, angle);

        NetworkServer.Spawn(spawnedEnemy);
        Debug.Log($"{enemyPrefab.name}이(가) {spawnPosition} 위치에 생성되었습니다.");
    }
    
    Vector2 GetRandomPositionOnCircle()
    {
        // 0도에서 360도 사이의 랜덤한 각도
        float randomAngle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
        
        // 원의 테두리 위 좌표 계산
        float x = shipAgent.position.x + spawnRadius * Mathf.Cos(randomAngle);
        float y = shipAgent.position.y + spawnRadius * Mathf.Sin(randomAngle);
        
        return new Vector2(x, y);
    }
    
    // 에디터에서 스폰 범위를 시각적으로 확인
    void OnDrawGizmosSelected()
    {
        if (shipAgent == null) return;
        
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(shipAgent.position, spawnRadius);
    }
}