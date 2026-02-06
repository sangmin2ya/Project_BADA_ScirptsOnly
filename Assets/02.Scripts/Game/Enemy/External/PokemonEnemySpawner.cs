using UnityEngine;
using Mirror;
using System.Collections.Generic;
using System;
using System.Collections;
using Unity.Mathematics;

/// <summary>
/// 포켓몬처럼 돌다다니다가 조우하는 스타일의 적 스포너
/// [박상민 - 2025.11.10]
/// </summary>
public class PokemonEnemySpawner : NetworkBehaviour
{
    [Header("스크립터블 오브젝트 참조")]
    [Tooltip("맵 구역별 적 스폰 정보")]
    [SerializeField] private AreaSpawnSettings _areaSpawnSettings;
    [Tooltip("적 프리팹 가중치 정보")]
    [SerializeField] private EnemySpawnWeightSettings _spawnWeightSettings;

    [Header("적 인카운터 설정")]
    [Tooltip("다음 인카운터까지 최소 이동 시간")]
    [SerializeField] private const float _minDurationForNextEncounter = 7f;
    [Tooltip("플레이어 뷰포트 반경 (스폰 위치 산출용)")]
    [SerializeField] private const float _playerViewportRadius = 80f;
    [Tooltip("클리어 조건 포인트 비율 (기본값 0.8 = 80%)")]
    [SerializeField] private float _clearConditionPointRatio = 0.8f;

    /*
    private 필드
    */
    private AgentController _agentController;
    private List<AreaType> _currentAreaType = new List<AreaType>(); // 겹쳐있으면 우선순위에 따라 판단
    private List<ExternalEnemyController> _spawnedEnemies = new List<ExternalEnemyController>();
    private bool _isEncounterActive = true; // 인카운터 활성화 여부
    private bool _isWaveTransitioning = false; // 웨이브 전환 중인지 여부

    // 마지막 인카운터 수치 저장    
    private Vector2 _lastEncounterCheckPosition = Vector2.zero;
    private float _lastEncounterTime = 0f;
    private int _currentEncounterWave = 0;
    private float _currentWaveStartTime = 0f;
    private float _currentWaveDifficultyPoints = 0f;
    private Vector2 _currentWaveStartPosition = Vector2.zero;


    /* 
    접근 프로퍼티
    */
    public AreaType CurrentAreaType => _currentAreaType.Count > 0 ? _currentAreaType[0] : AreaType.일반구역;

    void Awake()
    {
        _agentController = GetComponent<AgentController>();
        // 지역별 스폰정보 index재정렬 (AreaType 순서대로)
        Array.Sort(_areaSpawnSettings.AreaSpawnInfos, (a, b) => a.AreaType.CompareTo(b.AreaType));
        Array.Sort(_spawnWeightSettings.TimeRangeProfiles, (a, b) => a.TimeType.CompareTo(b.TimeType));
        foreach (var profile in _spawnWeightSettings.TimeRangeProfiles)
        {
            Array.Sort(profile.WeightProfile, (a, b) => a.EnemyShipType.CompareTo(b.EnemyShipType));
        }
        _lastEncounterCheckPosition = _agentController.transform.position;
        _lastEncounterTime = Time.time;
    }

    void Update()
    {
        if (!isServer)
        {
            return;
        }
        // 플레이어 위치 기준 인카운터 판정
        CanTriggerEncounter(_agentController.transform.position);
        // 웨이브 조건 체크
        CheckNextWaveCondition();
    }

    #region 적 인카운터 판정

    private void CanTriggerEncounter(Vector2 playerPosition)
    {
        // 이미 인카운터 중
        if (!_isEncounterActive)
        {
            return;
        }
        // 마지막 인카운터 위치에서 아직 멀리 떨어지지 않음
        if (Time.time - _lastEncounterTime >= _minDurationForNextEncounter && Vector2.Distance(playerPosition, _lastEncounterCheckPosition) >= CalculatePlayerViewportRadius())
        {
            if (CheckEncounterTrigger())
            {
                StartEncounter(playerPosition);
            }
            _lastEncounterCheckPosition = playerPosition;
            _lastEncounterTime = Time.time;
        }
    }

    private void StartEncounter(Vector2 encounterPosition)
    {
        Debug.Log("<color='red'>적 인카운터 발생!</color>");
        _isEncounterActive = false; // 인카운터 중복 방지
        _lastEncounterCheckPosition = encounterPosition;
        _lastEncounterTime = Time.time;
        // 적 스폰 로직 구현
        _currentEncounterWave = DetermineWaveCount();
        StartWave();
    }

    /// <summary>
    /// 인카운터 실행 조건 체크하고 적 조우여부 결정하여 반환
    /// </summary>
    private bool CheckEncounterTrigger()
    {
        float encounterChance = GetEncounterChance(CurrentAreaType);
        float randomValue = UnityEngine.Random.Range(0f, 1f);
        return randomValue <= encounterChance;
    }

    /// <summary>
    /// 지난 시간만큼 인카운터 확률 계산
    /// </summary>
    private float GetEncounterChance(AreaType areaType)
    {
        // 해당 구역의 스폰 정보 찾기 (정렬해놔서 index로 접근 가능)
        var spawnInfo = _areaSpawnSettings.AreaSpawnInfos[areaType.GetHashCode()];

        float encounterChance = spawnInfo.BaseSpawnRate + (spawnInfo.SpawnRatePerMinute * SessionManager.Instance.SessionElapsedTimePerTimeUnit / 60f);

        return Mathf.Min(encounterChance, 1f); // 최대 100% 확률
    }

    #endregion

    #region 적 웨이브 관리

    /// <summary>
    /// 웨이브 수 결정
    /// </summary>
    /// <returns></returns>
    private int DetermineWaveCount()
    {
        // 지역별 기본 웨이브 수
        // float baseWeight = _areaSpawnSettings.AreaSpawnInfos[CurrentAreaType.GetHashCode()].EnemyWaveCount;

        // float weight1Wave = Mathf.Max(baseWeight - 1f, 1f);
        // float weight2Wave = Mathf.Max(baseWeight, 1f);
        // float weight3Wave = Mathf.Max(baseWeight + 1f, 1f);

        // // 세션 진행도 영향 (0.0 ~ 1.0)
        // // 힌트 수집 개수로 결정: 0개 = 0.0, 1개 = 0.33, 2개 = 0.67, 3개 = 1.0
        // float sessionProgress = SessionManager.Instance.RadarActivationCount / 3.0f;
        // weight2Wave += sessionProgress * 1.5f;
        // weight3Wave += sessionProgress * 2.0f;

        // // 세션 내 경과 시간 영향 (시간이 바뀔때만 증가)
        // float elapsedMinutes = SessionManager.Instance.SessionElapsedTimePerTimeUnit / 60f;
        // weight2Wave += (elapsedMinutes / 5f) * 0.5f;
        // weight3Wave += (elapsedMinutes / 5f) * 1.0f;

        // // 가중치 기반 랜덤 선택
        // return WeightedRandomSelect(weight1Wave, weight2Wave, weight3Wave);
        return 1; // 우선 1웨이브 고정
    }

    /// <summary>
    /// 가중치 기반 랜덤 선택
    /// </summary>
    private int WeightedRandomSelect(float weight1, float weight2, float weight3)
    {
        float totalWeight = weight1 + weight2 + weight3;
        float randomValue = UnityEngine.Random.Range(0f, totalWeight);

        if (randomValue < weight1)
        {
            return 1;
        }
        else if (randomValue < weight1 + weight2)
        {
            return 2;
        }
        else
        {
            return 3;
        }
    }

    /// <summary>
    /// 다음 웨이브 조건 체크
    /// </summary>
    private void CheckNextWaveCondition()
    {
        // 현재 웨이브가 없거나 전환 중이면 종료
        if (_currentEncounterWave <= 0 || _isWaveTransitioning)
        {
            return;
        }
        // 조건 1: 난이도 포인트 기준 80% 처치
        float remainingDifficultyPoints = CalculateRemainingDifficultyPoints();
        float clearanceRate = 1.0f - (remainingDifficultyPoints / _currentWaveDifficultyPoints);
        bool isClearedByKills = clearanceRate >= _clearConditionPointRatio;

        // 조건 2: 타임아웃 경과
        float waveElapsedTime = Time.time - _currentWaveStartTime;
        bool isTimeout = waveElapsedTime >= GetWaveTimeout(_currentWaveDifficultyPoints);

        // 둘 중 하나라도 만족하면 다음 웨이브 준비
        if (isClearedByKills || isTimeout)
        {
            _currentEncounterWave--;
            if (_currentEncounterWave <= 0) // 모든 웨이브 클리어했다면
            {
                // 모든 웨이브 클리어
                _isEncounterActive = true; // 인카운터 재활성화
                _lastEncounterTime = Time.time; // 마지막 인카운터 시간 갱신
                Debug.Log("<color='red'>적 인카운터가 종료되었습니다.</color>");
                return;
            }
            // 쉬는 시간 대기 후 다음 웨이브 시작
            float restTime = GetWaveRestTime(_currentWaveDifficultyPoints);
            StartCoroutine(WaitAndStartNextWave(restTime));
        }
    }

    /// <summary>
    /// 남은 적들의 난이도 포인트 합산 계산
    /// </summary>
    float CalculateRemainingDifficultyPoints()
    {
        float totalPoints = 0f;
        //null 삭제
        _spawnedEnemies.RemoveAll(enemy => enemy == null);

        foreach (var enemy in _spawnedEnemies)
        {
            if (enemy != null)
            {
                EnemyWeightProfile weightInfo = _spawnWeightSettings.TimeRangeProfiles[SessionManager.Instance.CurrentTimeType.GetHashCode()].WeightProfile[enemy.EnemyShipType.GetHashCode()];
                totalPoints += weightInfo.Point;
            }
        }
        return totalPoints;
    }

    /// <summary>
    /// 웨이브 타임아웃 시간 계산 (난이도에 따라 가변)
    /// </summary>
    float GetWaveTimeout(float waveDifficultyPoints)
    {
        // 기본값: 등록해둔 지역 쿨타임
        // 난이도가 높을수록 시간 증가 (예: 난이도 포인트 10당 +5초)
        float baseTimeout = _areaSpawnSettings.AreaSpawnInfos[CurrentAreaType.GetHashCode()].WaveTimeOut;
        float additionalTime = (waveDifficultyPoints / 10f) * 5f;

        return baseTimeout + additionalTime;
    }

    /// <summary>
    /// 웨이브 간 쉬는 시간 계산 (난이도에 따라 가변)
    /// </summary>
    float GetWaveRestTime(float waveDifficultyPoints)
    {
        // 기본값: 등록해둔 지역 쉬는시간
        // 난이도가 높을수록 시간 증가 (예: 난이도 포인트 20당 +1초)
        float baseRestTime = _areaSpawnSettings.AreaSpawnInfos[CurrentAreaType.GetHashCode()].WaveCooldownTime;
        float additionalTime = (waveDifficultyPoints / 20f) * 1f;

        return baseRestTime + additionalTime;
    }
    /// <summary>
    /// 기다렸다가 다음 웨이브 시작
    /// </summary>
    IEnumerator WaitAndStartNextWave(float restTime)
    {
        _isWaveTransitioning = true; // 웨이브 전환 중 플래그
        yield return new WaitForSeconds(restTime);
        _isWaveTransitioning = false;
        StartWave();
    }

    private void StartWave()
    {
        Debug.Log($"<color='red'>적 인카운터 웨이브 시작! 남은 웨이브: {_currentEncounterWave}</color>");
        _currentWaveDifficultyPoints = _areaSpawnSettings.AreaSpawnInfos[CurrentAreaType.GetHashCode()].DifficultyPoints;
        _currentWaveStartTime = Time.time;
        _currentWaveStartPosition = _agentController.transform.position;
        SpawnEnemiesForWave();
    }

    #endregion

    #region 적 스폰

    /// <summary>
    /// 웨이브 내 적 스폰
    /// </summary>
    private void SpawnEnemiesForWave()
    {
        float remainingDifficultyPoints = _currentWaveDifficultyPoints;
        int maxSpawnAttempts = 100;
        int spawnAttempts = 0;

        while (remainingDifficultyPoints > 0f && spawnAttempts < maxSpawnAttempts)
        {
            spawnAttempts++;

            // 남은 포인트 이내로 스폰 가능한 적 타입 선택
            EnemyShipType? selectedType = SelectEnemyTypeByWeight(remainingDifficultyPoints);

            if (selectedType == null)
            {
                // 스폰 가능한 적이 없으면 종료
                Debug.Log($"남은 포인트로 스폰 가능한 적이 없습니다. 남은 포인트: {remainingDifficultyPoints}");
                break;
            }

            EnemyWeightProfile weightInfo = _spawnWeightSettings.TimeRangeProfiles[SessionManager.Instance.CurrentTimeType.GetHashCode()].WeightProfile[selectedType.Value.GetHashCode()];

            // 적 스폰
            Vector2 spawnPosition = GetValidSpawnPosition();
            SpawnEnemyAtPosition(selectedType.Value, spawnPosition);

            remainingDifficultyPoints -= weightInfo.Point;
        }

        if (spawnAttempts >= maxSpawnAttempts)
        {
            Debug.LogWarning($"적 스폰 최대 시도 횟수 도달. 남은 난이도 포인트: {remainingDifficultyPoints}");
        }
    }

    /// <summary>
    /// 남은 포인트 내에서 가중치 기반으로 적 타입 선택
    /// </summary>
    private EnemyShipType? SelectEnemyTypeByWeight(float maxPoints)
    {
        TimeRangeProfile currentTimeProfile = _spawnWeightSettings.TimeRangeProfiles[SessionManager.Instance.CurrentTimeType.GetHashCode()];

        // 스폰 가능한 적들만 필터링 (Point가 maxPoints 이하인 것들)
        List<EnemyWeightProfile> spawnableEnemies = new List<EnemyWeightProfile>();
        float totalWeight = 0f;

        foreach (var info in currentTimeProfile.WeightProfile)
        {
            if (info.Point <= maxPoints)
            {
                spawnableEnemies.Add(info);
                totalWeight += info.Weight;
            }
        }

        // 스폰 가능한 적이 없으면 null 반환
        if (spawnableEnemies.Count == 0 || totalWeight <= 0f)
        {
            return null;
        }

        // 가중치 기반 랜덤 선택
        float randomValue = UnityEngine.Random.Range(0f, totalWeight);
        float cumulativeWeight = 0f;

        foreach (var info in spawnableEnemies)
        {
            cumulativeWeight += info.Weight;
            if (randomValue <= cumulativeWeight)
            {
                return info.EnemyShipType;
            }
        }

        return spawnableEnemies[0].EnemyShipType; // 기본값 반환
    }

    /// <summary>
    /// 특정 위치에 적 스폰
    /// </summary>
    private ExternalEnemyController SpawnEnemyAtPosition(EnemyShipType shipType, Vector2 position)
    {
        EnemyWeightProfile weightInfo = _spawnWeightSettings.TimeRangeProfiles[SessionManager.Instance.CurrentTimeType.GetHashCode()].WeightProfile[shipType.GetHashCode()];
        if (weightInfo.EnemyPrefab != null)
        {
            // 배를 바라보도록 스폰 (2D)
            Vector2 direction = (Vector2)_agentController.transform.position - position;
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            Quaternion rotation = Quaternion.Euler(0, 0, angle);

            GameObject enemyObj = Instantiate(weightInfo.EnemyPrefab.gameObject, position, rotation);
            NetworkServer.Spawn(enemyObj);
            ExternalEnemyController enemyController = enemyObj.GetComponent<ExternalEnemyController>();
            _spawnedEnemies.Add(enemyController);
            return enemyController;
        }
        return null;
    }

    /// <summary>
    /// 플레이어 뷰포트 기반 랜덤 스폰 위치 계산(일단은 고정값 사용)
    /// </summary>
    private float CalculatePlayerViewportRadius()
    {
        return _playerViewportRadius;
    }

    private Vector2 GetValidSpawnPosition()
    {
        float viewportRadius = CalculatePlayerViewportRadius();
        float innerRadius = viewportRadius * 1.1f;
        float outerRadius = viewportRadius * 1.3f;

        int maxAttempts = 30; // 최대 시도 횟수

        for (int i = 0; i < maxAttempts; i++)
        {
            // 도넛 영역 내 랜덤 위치 생성
            Vector2 randomPosition = GetRandomPositionInDonut(_currentWaveStartPosition, innerRadius, outerRadius);

            // 장애물 체크
            if (!IsOverlappingObstacle(randomPosition))
            {
                return randomPosition;
            }
        }

        // 최대 시도 횟수 초과 시 증가된 범위에서 다시 한 번 시도
        for (int i = 0; i < maxAttempts; i++)
        {
            // 도넛 영역 내 랜덤 위치 생성
            Vector2 randomPosition = GetRandomPositionInDonut(_currentWaveStartPosition, outerRadius, outerRadius * 1.5f);

            // 장애물 체크
            if (!IsOverlappingObstacle(randomPosition))
            {
                return randomPosition;
            }
        }

        // 실패시 예외처리 생성하지 않도록
        Debug.LogWarning("적 스폰 위치 생성 실패: 유효한 위치를 찾지 못했습니다. 기본 위치 반환.");
        return _currentWaveStartPosition + UnityEngine.Random.insideUnitCircle.normalized * outerRadius;
    }

    /// <summary>
    /// 장애물 겹침 체크 - 일단 장애물들이 다 default에 있어서 그걸로 체크
    /// </summary>
    private bool IsOverlappingObstacle(Vector2 position)
    {
        LayerMask landLayer = LayerMask.GetMask("Default");

        // 반경 내 모든 콜라이더 체크
        Collider2D[] hits = Physics2D.OverlapCircleAll(position, 1f, landLayer);

        foreach (var hit in hits)
        {
            // Trigger가 아닌 실제 장애물만 체크
            if (hit != null && !hit.isTrigger)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 도넛 영역 내 랜덤 위치 반환
    /// </summary>
    private Vector2 GetRandomPositionInDonut(Vector2 center, float innerRadius, float outerRadius)
    {
        Vector2 direction = UnityEngine.Random.insideUnitCircle.normalized;
        float distance = UnityEngine.Random.Range(innerRadius, outerRadius);

        return center + direction * distance;
    }

    #endregion

    #region 충돌 감지

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (!isServer)
        {
            return;
        }
        var areaZone = collision.GetComponent<AreaZone>();
        if (areaZone != null)
        {
            if (!_currentAreaType.Contains(areaZone.ZoneAreaType))
            {
                _currentAreaType.Add(areaZone.ZoneAreaType);
                // 우선순위에 따라 정렬 (높은 위험도의 타입이 앞에 오도록)
                _currentAreaType.Sort();
                _currentAreaType.Reverse();
            }
        }
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        if (!isServer)
        {
            return;
        }
        var areaZone = collision.GetComponent<AreaZone>();
        if (areaZone != null)
        {
            if (_currentAreaType.Contains(areaZone.ZoneAreaType))
            {
                _currentAreaType.Remove(areaZone.ZoneAreaType);
            }
        }
    }

    #endregion
}