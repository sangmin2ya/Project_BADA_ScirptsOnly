using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FishSchoolSpawner : MonoBehaviour
{
    #region Serialized Fields
    [SerializeField] private GameObject[] _fishSchoolPrefabs;
    [SerializeField] private float _spawnInterval = 10f;
    [SerializeField] private float _swimSpeed = 3f;
    [SerializeField] private float _despawnDistance = 15f;
    [SerializeField] private float _verticalSpawnRange = 5f;
    [SerializeField] private Camera _Camera;
    #endregion

    #region Private Fields
    private List<GameObject> _activeFishSchools;
    private List<Vector3> _spawnDirections;
    #endregion

    #region Unity Methods
    private void Start()
    {
        InitializeSpawner();
        StartCoroutine(SpawnFishSchoolsPeriodically());
    }

    private void Update()
    {
        CheckAndDestroyOutOfBoundsFish();
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// 스포너를 초기화합니다
    /// </summary>
    private void InitializeSpawner()
    {
        if (_Camera == null)
        {
            _Camera = GameObject.Find("AgentCamera")?.GetComponent<Camera>();
        }

        if (_fishSchoolPrefabs == null || _fishSchoolPrefabs.Length == 0)
        {
            Debug.LogError("FishSchoolSpawner: fishSchoolPrefabs가 할당되지 않았습니다.");
            return;
        }

        _activeFishSchools = new List<GameObject>();

        // 스폰 방향들 (좌→우, 우→좌, 위→아래, 아래→위, 대각선)
        _spawnDirections = new List<Vector3>
        {
            Vector3.right,  // 좌에서 우로
            Vector3.left,   // 우에서 좌로
            Vector3.down,   // 위에서 아래로
            Vector3.up      // 아래에서 위로
            
        };
    }

    /// <summary>
    /// 주기적으로 물고기 무리를 스폰하는 코루틴
    /// </summary>
    private IEnumerator SpawnFishSchoolsPeriodically()
    {
        while (true)
        {
            yield return new WaitForSeconds(_spawnInterval);
            if (_Camera == null)
            {
                _Camera = GameObject.Find("AgentCamera")?.GetComponent<Camera>();
                continue;
            }
            SpawnFishSchool();
        }
    }

    /// <summary>
    /// 물고기 무리를 스폰합니다
    /// </summary>
    private void SpawnFishSchool()
    {
        if (_fishSchoolPrefabs == null || _fishSchoolPrefabs.Length == 0 || _Camera == null)
        {
            return;
        }

        // 랜덤 방향 선택
        Vector2 swimDirection = _spawnDirections[Random.Range(0, _spawnDirections.Count)];

        // 스폰 위치 계산 (화면 밖)
        Vector2 spawnPosition = CalculateOffScreenSpawnPosition(swimDirection);

        // 약간의 랜덤 변동 추가
        Vector2 randomSwimDirection = swimDirection + new Vector2(Random.Range(-0.3f, 0.3f), Random.Range(-0.3f, 0.3f));

        // 진행방향으로 회전
        Quaternion swimRotation = Quaternion.LookRotation(Vector3.forward, randomSwimDirection);

        // 물고기 무리 생성
        GameObject fishSchool = Instantiate(_fishSchoolPrefabs[Random.Range(0, _fishSchoolPrefabs.Length)], spawnPosition, swimRotation);

        // 물고기 무리 방향 설정
        SetFishSchoolDirection(fishSchool, randomSwimDirection);

        // FishSchoolMover 컴포넌트 추가 및 설정
        FishSchoolMover mover = fishSchool.GetComponent<FishSchoolMover>();
        if (mover == null)
        {
            mover = fishSchool.AddComponent<FishSchoolMover>();
        }
        mover.SetMovement(randomSwimDirection, _swimSpeed);

        _activeFishSchools.Add(fishSchool);
    }

    /// <summary>
    /// 화면 밖 스폰 위치를 계산합니다
    /// </summary>
    private Vector3 CalculateOffScreenSpawnPosition(Vector3 direction)
    {
        Vector3 cameraPosition = _Camera.transform.position;
        float cameraHeight = _Camera.orthographicSize * 2f;
        float cameraWidth = cameraHeight * _Camera.aspect;

        Vector3 spawnPosition = cameraPosition;

        if (direction == Vector3.right) // 좌에서 우로
        {
            spawnPosition.x = cameraPosition.x - cameraWidth * 0.6f;
            spawnPosition.y = cameraPosition.y + Random.Range(-_verticalSpawnRange, _verticalSpawnRange);
        }
        else if (direction == Vector3.left) // 우에서 좌로
        {
            spawnPosition.x = cameraPosition.x + cameraWidth * 0.6f;
            spawnPosition.y = cameraPosition.y + Random.Range(-_verticalSpawnRange, _verticalSpawnRange);
        }
        else if (direction == Vector3.down) // 위에서 아래로
        {
            spawnPosition.y = cameraPosition.y + cameraHeight * 0.6f;
            spawnPosition.x = cameraPosition.x + Random.Range(-cameraWidth * 0.3f, cameraWidth * 0.3f);
        }
        else if (direction == Vector3.up) // 아래에서 위로
        {
            spawnPosition.y = cameraPosition.y - cameraHeight * 0.6f;
            spawnPosition.x = cameraPosition.x + Random.Range(-cameraWidth * 0.3f, cameraWidth * 0.3f);
        }

        return spawnPosition;
    }

    /// <summary>
    /// 물고기 무리의 방향을 설정합니다
    /// </summary>
    private void SetFishSchoolDirection(GameObject fishSchool, Vector3 direction)
    {
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        fishSchool.transform.rotation = Quaternion.Euler(0, 0, angle);
    }

    /// <summary>
    /// 화면 밖으로 나간 물고기들을 확인하고 제거합니다
    /// </summary>
    private void CheckAndDestroyOutOfBoundsFish()
    {
        if (_Camera == null)
        {
            return;
        }

        Vector3 cameraPosition = _Camera.transform.position;

        for (int i = _activeFishSchools.Count - 1; i >= 0; i--)
        {
            if (_activeFishSchools[i] == null)
            {
                _activeFishSchools.RemoveAt(i);
                continue;
            }

            float distance = Vector3.Distance(_activeFishSchools[i].transform.position, cameraPosition);

            if (distance > _despawnDistance)
            {
                DestroyFishSchool(_activeFishSchools[i]);
                _activeFishSchools.RemoveAt(i);
            }
        }
    }

    /// <summary>
    /// 물고기 무리를 제거합니다
    /// </summary>
    private void DestroyFishSchool(GameObject school)
    {
        if (school != null)
        {
            Destroy(school);
        }
    }
    #endregion
}