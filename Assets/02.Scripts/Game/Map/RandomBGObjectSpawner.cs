using System.Collections.Generic;
using UnityEngine;
using Mirror;

public class RandomBGObjectSpawner : NetworkBehaviour
{
    #region 스폰 설정
    [Header("스폰 설정")]
    [SerializeField] private GameObject[] spawnableObjects = new GameObject[10];
    [SerializeField] private int gridSize = 100;
    [SerializeField] private int objectsPerGrid = 4;
    [SerializeField] private float minimumDistance = 20f;
    [SerializeField] private int maxSpawnAttempts = 50;

    [Header("월드 크기 설정")]
    [SerializeField] private int worldWidth = 3000;
    [SerializeField] private int worldHeight = 3000;

    [Header("스폰 금지 지역설정")]
    [SerializeField] private List<ForbiddenSpawnZone> forbiddenSpawnZones = new List<ForbiddenSpawnZone>();
    #endregion

    #region 그리드 관리
    private Dictionary<Vector2Int, List<Vector2>> spawnedPositions = new Dictionary<Vector2Int, List<Vector2>>();
    #endregion

    #region Unity 생명주기
    private void Start()
    {
        if (!isServer)
        {
            return; // 서버에서만 실행
        }
        SpawnObjectsInAllGrids();
    }
    #endregion

    #region 스폰 메소드
    /// <summary>
    /// 모든 그리드에서 오브젝트를 스폰합니다 (0,0 중심 기준)
    /// </summary>
    private void SpawnObjectsInAllGrids()
    {
        try
        {
            // 월드 크기 기준으로 그리드 개수 계산
            int gridCountX = Mathf.CeilToInt((float)worldWidth / gridSize);
            int gridCountY = Mathf.CeilToInt((float)worldHeight / gridSize);

            // 중심점 계산 (0,0을 중심으로)
            int halfGridCountX = gridCountX / 2;
            int halfGridCountY = gridCountY / 2;

            Debug.Log($"월드 크기: {worldWidth}x{worldHeight}, 그리드 개수: {gridCountX}x{gridCountY}");
            Debug.Log($"중심 기준 그리드 범위: X({-halfGridCountX} ~ {halfGridCountX}), Y({-halfGridCountY} ~ {halfGridCountY})");

            // 중심을 기준으로 모든 그리드에서 스폰 실행
            for (int gridX = -halfGridCountX; gridX < halfGridCountX; gridX++)
            {
                for (int gridY = -halfGridCountY; gridY < halfGridCountY; gridY++)
                {
                    SpawnObjectsInGrid(new Vector2Int(gridX, gridY));
                }
            }

            Debug.Log($"총 {gridCountX * gridCountY}개 그리드에서 스폰 완료");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"모든 그리드 스폰 중 오류 발생: {e.Message}");
        }
    }

    /// <summary>
    /// 특정 그리드 내에서 오브젝트를 스폰합니다 (0,0 중심 기준)
    /// </summary>
    /// <param name="gridCoordinate">그리드 좌표 (중심 기준)</param>
    public void SpawnObjectsInGrid(Vector2Int gridCoordinate)
    {
        try
        {
            if (spawnedPositions.ContainsKey(gridCoordinate))
            {
                return; // 이미 스폰된 그리드
            }

            spawnedPositions[gridCoordinate] = new List<Vector2>();

            // 그리드의 실제 월드 좌표 계산 (0,0 중심 기준)
            Vector2 gridWorldPosition = new Vector2(gridCoordinate.x * gridSize, gridCoordinate.y * gridSize);

            // 스폰할 오브젝트 선택
            List<GameObject> selectedObjects = SelectRandomObjects();

            // 각 오브젝트를 스폰
            foreach (GameObject objectToSpawn in selectedObjects)
            {
                Vector2 spawnPosition = FindValidSpawnPosition(gridWorldPosition, gridCoordinate);

                if (spawnPosition != Vector2.zero)
                {
                    SpawnSingleObject(objectToSpawn, spawnPosition);
                    spawnedPositions[gridCoordinate].Add(spawnPosition);
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"그리드 {gridCoordinate} 스폰 중 오류 발생: {e.Message}");
        }
    }

    /// <summary>
    /// 랜덤하게 4개의 오브젝트를 선택합니다
    /// </summary>
    /// <returns>선택된 오브젝트 리스트</returns>
    private List<GameObject> SelectRandomObjects()
    {
        List<GameObject> selectedObjects = new List<GameObject>();
        List<int> availableIndices = new List<int>();

        // 유효한 오브젝트 인덱스 수집
        for (int i = 0; i < spawnableObjects.Length; i++)
        {
            if (spawnableObjects[i] != null)
            {
                availableIndices.Add(i);
            }
        }

        if (availableIndices.Count == 0)
        {
            Debug.LogWarning("스폰 가능한 오브젝트가 없습니다.");
            return selectedObjects;
        }

        // 4개 선택 (중복 없이)
        int objectsToSelect = Mathf.Min(objectsPerGrid, availableIndices.Count);

        for (int i = 0; i < objectsToSelect; i++)
        {
            int randomIndex = Random.Range(0, availableIndices.Count);
            int selectedObjectIndex = availableIndices[randomIndex];
            selectedObjects.Add(spawnableObjects[selectedObjectIndex]);
            availableIndices.RemoveAt(randomIndex);
        }

        return selectedObjects;
    }

    /// <summary>
    /// 유효한 스폰 위치를 찾습니다 (0,0 중심 기준)
    /// </summary>
    /// <param name="gridWorldPosition">그리드의 월드 좌표</param>
    /// <param name="gridCoordinate">그리드 좌표</param>
    /// <returns>유효한 스폰 위치</returns>
    private Vector2 FindValidSpawnPosition(Vector2 gridWorldPosition, Vector2Int gridCoordinate)
    {
        for (int attempt = 0; attempt < maxSpawnAttempts; attempt++)
        {
            // 현재 그리드의 실제 크기 계산 (월드 경계 고려)
            float actualGridWidth = CalculateActualGridSizeCentered(gridCoordinate.x, worldWidth);
            float actualGridHeight = CalculateActualGridSizeCentered(gridCoordinate.y, worldHeight);

            // 그리드 내 랜덤 위치 생성
            float randomX = Random.Range(0f, actualGridWidth);
            float randomY = Random.Range(0f, actualGridHeight);
            Vector2 candidatePosition = gridWorldPosition + new Vector2(randomX, randomY);

            if (IsValidSpawnPosition(candidatePosition, gridCoordinate))
            {
                return candidatePosition;
            }
        }

        Debug.LogWarning($"그리드 {gridCoordinate}에서 유효한 스폰 위치를 찾지 못했습니다.");
        return Vector2.zero;
    }

    /// <summary>
    /// 중심 기준 월드 경계를 고려한 실제 그리드 크기를 계산합니다
    /// </summary>
    /// <param name="gridIndex">그리드 인덱스 (중심 기준)</param>
    /// <param name="worldSize">월드 크기</param>
    /// <returns>실제 그리드 크기</returns>
    private float CalculateActualGridSizeCentered(int gridIndex, int worldSize)
    {
        // 중심 기준 좌표를 실제 월드 좌표로 변환
        int startPos = gridIndex * gridSize;
        int endPos = startPos + gridSize;

        // 월드 경계 확인 (-worldSize/2 ~ worldSize/2)
        int worldHalf = worldSize / 2;
        startPos = Mathf.Max(startPos, -worldHalf);
        endPos = Mathf.Min(endPos, worldHalf);

        return endPos - startPos;
    }

    /// <summary>
    /// 스폰 위치가 유효한지 검사합니다
    /// </summary>
    /// <param name="position">검사할 위치</param>
    /// <param name="currentGrid">현재 그리드</param>
    /// <returns>유효 여부</returns>
    private bool IsValidSpawnPosition(Vector2 position, Vector2Int currentGrid)
    {
        // 금지된 스폰 위치 검사
        foreach (ForbiddenSpawnZone forbiddenZone in forbiddenSpawnZones)
        {
            if (Vector2.Distance(position, forbiddenZone.position) < forbiddenZone.radius)
            {
                return false; // 금지된 영역 내에 위치하면 무효
            }
        }

        // 현재 그리드 내 다른 오브젝트와의 거리 검사
        if (spawnedPositions.ContainsKey(currentGrid))
        {
            foreach (Vector2 existingPosition in spawnedPositions[currentGrid])
            {
                if (Vector2.Distance(position, existingPosition) < minimumDistance)
                {
                    return false;
                }
            }
        }

        // 인접 그리드와의 거리 검사
        for (int deltaX = -1; deltaX <= 1; deltaX++)
        {
            for (int deltaY = -1; deltaY <= 1; deltaY++)
            {
                if (deltaX == 0 && deltaY == 0)
                {
                    continue; // 현재 그리드는 이미 검사함
                }

                Vector2Int adjacentGrid = currentGrid + new Vector2Int(deltaX, deltaY);

                if (spawnedPositions.ContainsKey(adjacentGrid))
                {
                    foreach (Vector2 existingPosition in spawnedPositions[adjacentGrid])
                    {
                        if (Vector2.Distance(position, existingPosition) < minimumDistance)
                        {
                            return false;
                        }
                    }
                }
            }
        }

        return true;
    }

    /// <summary>
    /// 단일 오브젝트를 지정된 위치에 스폰합니다
    /// </summary>
    /// <param name="objectToSpawn">스폰할 오브젝트</param>
    /// <param name="spawnPosition">스폰 위치</param>
    private void SpawnSingleObject(GameObject objectToSpawn, Vector2 spawnPosition)
    {
        try
        {
            // 랜덤 회전값 생성 (0~360도)
            float randomRotation = Random.Range(0f, 360f);
            Quaternion rotation = Quaternion.Euler(0f, 0f, randomRotation);

            GameObject spawnedObject = Instantiate(objectToSpawn, spawnPosition, rotation);
            spawnedObject.name = $"{objectToSpawn.name}_Spawned";
            NetworkServer.Spawn(spawnedObject);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"오브젝트 스폰 중 오류 발생: {e.Message}");
        }
    }
    #endregion

    #region 디버그 메소드
    /// <summary>
    /// 그리드를 시각적으로 표시합니다 (디버그용)
    /// </summary>
    private void OnDrawGizmos()
    {
        // 월드 경계 표시 (0,0 중심)
        Gizmos.color = Color.blue;
        Vector2 worldCenter = Vector2.zero;
        Gizmos.DrawWireCube(worldCenter, new Vector2(worldWidth, worldHeight));

        if (!Application.isPlaying)
        {
            return;
        }

        Gizmos.color = Color.green;

        foreach (var gridPair in spawnedPositions)
        {
            Vector2Int gridCoord = gridPair.Key;
            Vector2 gridWorldPos = new Vector2(gridCoord.x * gridSize, gridCoord.y * gridSize);

            // 실제 그리드 크기 계산
            float actualWidth = CalculateActualGridSizeCentered(gridCoord.x, worldWidth);
            float actualHeight = CalculateActualGridSizeCentered(gridCoord.y, worldHeight);

            // 그리드 경계 그리기
            Vector2 gridCenter = gridWorldPos + new Vector2(actualWidth * 0.5f, actualHeight * 0.5f);
            Gizmos.DrawWireCube(gridCenter, new Vector2(actualWidth, actualHeight));

            // 스폰된 위치 표시
            Gizmos.color = Color.red;
            foreach (Vector2 spawnPos in gridPair.Value)
            {
                Gizmos.DrawWireSphere(spawnPos, minimumDistance * 0.5f);
            }
            Gizmos.color = Color.green;
        }
    }
    #endregion

    [System.Serializable]
    public struct ForbiddenSpawnZone
    {
        public Vector2 position;
        public float radius;
    }
}