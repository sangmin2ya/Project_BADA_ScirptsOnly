using System.Collections.Generic;
using Mirror;
using UnityEngine;

public class WorldBorderWallGenerator : NetworkBehaviour
{
    [Header("World Settings")]
    [SerializeField] private float worldSize = 3000f;
    [SerializeField] private float wallThickness = 75f; // 벽의 두께
    
    [Header("Wall Objects")]
    [SerializeField] private List<GameObject> wallPrefabs = new List<GameObject>();
    [SerializeField] private float objectSize = 25f; // 각 오브젝트의 크기
    
    [Header("Wall Generation Settings")]
    [SerializeField] private float clusterDensity = 1f; // 0~1, 높을수록 조밀
    [SerializeField] private float irregularityStrength = 8f; // 불규칙성 강도
    [SerializeField] private int noiseOctaves = 3; // 노이즈 옥타브 수
    [SerializeField] private float noiseScale = 0.1f; // 노이즈 스케일
    
    [Header("Generation Options")]
    [SerializeField] private bool generateOnStart = true;
    [SerializeField] private Transform wallParent; // 벽 오브젝트들의 부모 (선택사항)
    
    void Start()
    {
        if (isServer)
        {
            GenerateWorldBorderWalls();
        }
    }
    
    [ContextMenu("Generate Walls")]
    public void GenerateWorldBorderWalls()
    {
        if (wallPrefabs.Count == 0)
        {
            Debug.LogError("Wall prefabs list is empty!");
            return;
        }
        
        // 기존 벽 제거 (선택적)
        ClearExistingWalls();
        
        // 벽 생성
        GenerateWallSide(WallSide.Top);
        GenerateWallSide(WallSide.Bottom);
        GenerateWallSide(WallSide.Left);
        GenerateWallSide(WallSide.Right);
        
        Debug.Log("World border walls generated successfully!");
    }
    
    private void ClearExistingWalls()
    {
        if (wallParent != null)
        {
            for (int i = wallParent.childCount - 1; i >= 0; i--)
            {
                DestroyImmediate(wallParent.GetChild(i).gameObject);
            }
        }
    }
    
    private void GenerateWallSide(WallSide side)
    {
        Vector2 startPos, endPos, inwardDirection;
        
        // 각 면의 시작점, 끝점, 안쪽 방향 설정
        switch (side)
        {
            case WallSide.Top:
                startPos = new Vector2(-worldSize / 2, worldSize / 2);
                endPos = new Vector2(worldSize / 2, worldSize / 2);
                inwardDirection = Vector2.down;
                break;
            case WallSide.Bottom:
                startPos = new Vector2(-worldSize / 2, -worldSize / 2);
                endPos = new Vector2(worldSize / 2, -worldSize / 2);
                inwardDirection = Vector2.up;
                break;
            case WallSide.Left:
                startPos = new Vector2(-worldSize / 2, -worldSize / 2);
                endPos = new Vector2(-worldSize / 2, worldSize / 2);
                inwardDirection = Vector2.right;
                break;
            case WallSide.Right:
                startPos = new Vector2(worldSize / 2, -worldSize / 2);
                endPos = new Vector2(worldSize / 2, worldSize / 2);
                inwardDirection = Vector2.left;
                break;
            default:
                return;
        }
        
        // 해당 면을 따라 오브젝트 배치
        GenerateWallObjects(startPos, endPos, inwardDirection);
    }
    
    private void GenerateWallObjects(Vector2 startPos, Vector2 endPos, Vector2 inwardDirection)
    {
        Vector2 direction = (endPos - startPos).normalized;
        float sideLength = Vector2.Distance(startPos, endPos);
        
        // 오브젝트를 배치할 격자 계산
        int objectsAlongSide = Mathf.RoundToInt(sideLength / objectSize);
        int objectsInward = Mathf.RoundToInt(wallThickness / objectSize);
        
        for (int i = 0; i < objectsAlongSide; i++)
        {
            for (int j = 0; j < objectsInward; j++)
            {
                // 기본 위치 계산
                Vector2 basePos = startPos + direction * (i * objectSize) + inwardDirection * (j * objectSize);
                
                // 클러스터 밀도 체크
                if (Random.value > clusterDensity)
                    continue;
                
                // 불규칙성 추가 (여러 옥타브 노이즈 사용)
                Vector2 irregularOffset = GetIrregularOffset(basePos);
                Vector2 finalPos = basePos + irregularOffset;
                
                // 오브젝트 생성
                GameObject selectedPrefab = wallPrefabs[Random.Range(0, wallPrefabs.Count)];
                GameObject wallObject = Instantiate(selectedPrefab, finalPos, GetRandomRotation());
                NetworkServer.Spawn(wallObject); // 네트워크 오브젝트로 등록
                
                // 부모 설정
                if (wallParent != null)
                {
                    wallObject.transform.SetParent(wallParent);
                }
            }
        }
    }
    
    private Vector2 GetIrregularOffset(Vector2 position)
    {
        Vector2 totalOffset = Vector2.zero;
        float amplitude = irregularityStrength;
        float frequency = noiseScale;
        
        // 여러 옥타브의 노이즈를 합성하여 자연스러운 불규칙성 생성
        for (int octave = 0; octave < noiseOctaves; octave++)
        {
            float noiseX = Mathf.PerlinNoise(position.x * frequency, position.y * frequency) - 0.5f;
            float noiseY = Mathf.PerlinNoise((position.x + 1000) * frequency, (position.y + 1000) * frequency) - 0.5f;
            
            totalOffset += new Vector2(noiseX, noiseY) * amplitude;
            
            amplitude *= 0.5f; // 각 옥타브마다 진폭 감소
            frequency *= 2f;   // 각 옥타브마다 주파수 증가
        }
        
        return totalOffset;
    }
    
    private Quaternion GetRandomRotation()
    {
        // 2D에서는 Z축 회전만 사용
        float randomAngle = Random.Range(0f, 360f);
        return Quaternion.Euler(0f, 0f, randomAngle);
    }
    
    private enum WallSide
    {
        Top,
        Bottom,
        Left,
        Right
    }
}