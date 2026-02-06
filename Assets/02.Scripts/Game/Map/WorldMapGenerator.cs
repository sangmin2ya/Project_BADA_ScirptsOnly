using System.Collections.Generic;
using UnityEngine;

public class WorldMapGenerator : MonoBehaviour
{
    [Header("Map Settings")] public int mapSize = 2000;
    public Vector2 mapCenter = Vector2.zero;

    [Header("Cluster Settings")] public GameObject objectPrefab; // 클러스터 내 오브젝트 프리팹

    [Header("Object Generation")] public int minObjectsPerCluster = 5;
    public int maxObjectsPerCluster = 15;

    [Header("Outer Area Objects")] public GameObject outerAreaObjectPrefab; // 외곽 영역 오브젝트 프리팹
    public int outerAreaObjectDensity = 100; // 외곽 영역 오브젝트 개수

    private List<Cluster> allClusters = new List<Cluster>();
    private List<Vector2> outerAreaObjects = new List<Vector2>();
    private List<float> outerAreaObjectRotations = new List<float>();

    [System.Serializable]
    public class Cluster
    {
        public Vector2 center;
        public float radius;
        public int tier;
        public List<Vector2> objectPositions = new List<Vector2>();
        public List<float> objectRotations = new List<float>();
        public List<Vector2> boundaryPoints = new List<Vector2>(); // 외곽 클러스터용

        public Cluster(Vector2 center, float radius, int tier)
        {
            this.center = center;
            this.radius = radius;
            this.tier = tier;
        }
    }

    void Start()
    {
        GenerateWorldMap();
    }

    public void GenerateWorldMap()
    {
        allClusters.Clear();

        // 기존 오브젝트들 정리
        foreach (Transform child in transform)
        {
            DestroyImmediate(child.gameObject);
        }

        // 1. 내부 클러스터들 생성
        GenerateTier1Cluster();
        GenerateTier2Clusters();
        GenerateTier3Clusters();

        // 2. 외곽 클러스터 생성
        GenerateOuterCluster();

        // 3. 클러스터 겹침 해결
        ResolveClusterOverlaps();

        // 4. 각 클러스터 내 오브젝트 생성
        GenerateObjectsInClusters();

        // 5. 외곽 영역 오브젝트 생성
        GenerateOuterAreaObjects();

        // 6. 시각화
        VisualizeMap();
    }

    void GenerateTier1Cluster()
    {
        // 맵 중심에서 거리 300 안에, 반지름 200
        Vector2 center = GetRandomPositionInRing(mapCenter, 0, 200);
        Cluster tier1 = new Cluster(center, 200, 1);
        allClusters.Add(tier1);
    }

    void GenerateTier2Clusters()
    {
        // 맵 중심에서 거리 300~700, 반지름 200~300, 3개
        for (int i = 0; i < 3; i++)
        {
            Vector2 center = GetRandomPositionInRing(mapCenter, 400, 600);
            float radius = Random.Range(150f, 250f);
            Cluster tier2 = new Cluster(center, radius, 2);
            allClusters.Add(tier2);
        }
    }

    void GenerateTier3Clusters()
    {
        // 맵 중심에서 거리 700 밖, 반지름 200~400, 5개
        for (int i = 0; i < 5; i++)
        {
            Vector2 center = GetRandomPositionInRing(mapCenter, 800, 1000);
            float radius = Random.Range(100f, 300f);
            Cluster tier3 = new Cluster(center, radius, 3);
            allClusters.Add(tier3);
        }
    }

    void GenerateOuterCluster()
    {
        // 모든 클러스터를 감싸는 불규칙한 외곽 클러스터 생성
        float maxDistanceFromCenter = 0f;

        // 모든 클러스터의 최대 거리 계산
        foreach (var cluster in allClusters)
        {
            float distanceToEdge = Vector2.Distance(mapCenter, cluster.center) + cluster.radius;
            maxDistanceFromCenter = Mathf.Max(maxDistanceFromCenter, distanceToEdge);
        }

        // 여유 공간 추가
        maxDistanceFromCenter += 100f;

        Cluster outerCluster = new Cluster(mapCenter, maxDistanceFromCenter, 0);

        // 불규칙한 경계 생성
        int boundaryPoints = 32; // 경계점 개수
        for (int i = 0; i < boundaryPoints; i++)
        {
            float angle = (360f / boundaryPoints) * i * Mathf.Deg2Rad;
            float distanceVariation = Random.Range(1f, 1.5f); // 거리 변화
            float distance = maxDistanceFromCenter * distanceVariation;

            Vector2 point = mapCenter + new Vector2(
                Mathf.Clamp(Mathf.Cos(angle) * distance, -mapSize * 0.45f, mapSize * 0.45f),
                Mathf.Clamp(Mathf.Sin(angle) * distance, -mapSize * 0.45f, mapSize * 0.45f)
            );

            outerCluster.boundaryPoints.Add(point);
        }

        allClusters.Add(outerCluster);
    }

    void ResolveClusterOverlaps()
    {
        // 내부 클러스터들 간의 겹침 해결 (외곽 클러스터 제외)
        var innerClusters = allClusters.FindAll(c => c.tier > 0);

        bool hasOverlap = true;
        int maxIterations = 100;
        int iterations = 0;

        while (hasOverlap && iterations < maxIterations)
        {
            hasOverlap = false;
            iterations++;

            for (int i = 0; i < innerClusters.Count; i++)
            {
                for (int j = i + 1; j < innerClusters.Count; j++)
                {
                    if (ClustersOverlap(innerClusters[i], innerClusters[j]))
                    {
                        hasOverlap = true;
                        SeparateClusters(innerClusters[i], innerClusters[j]);
                    }
                }
            }
        }
    }

    bool ClustersOverlap(Cluster a, Cluster b)
    {
        float distance = Vector2.Distance(a.center, b.center);
        return distance < (a.radius + b.radius + 100f);
    }

    void SeparateClusters(Cluster a, Cluster b)
    {
        Vector2 direction = (b.center - a.center).normalized;
        float requiredDistance = a.radius + b.radius + 100f; // 10f 여유 공간
        float currentDistance = Vector2.Distance(a.center, b.center);
        float moveDistance = (requiredDistance - currentDistance) * 0.5f;

        a.center -= direction * moveDistance;
        b.center += direction * moveDistance;

        // 맵 경계 내에 유지
        a.center = ClampToMapBounds(a.center);
        b.center = ClampToMapBounds(b.center);
    }

    Vector2 ClampToMapBounds(Vector2 position)
    {
        float halfMap = mapSize * 0.5f;
        return new Vector2(
            Mathf.Clamp(position.x, mapCenter.x - halfMap + 400, mapCenter.x + halfMap - 400),
            Mathf.Clamp(position.y, mapCenter.y - halfMap + 400, mapCenter.y + halfMap - 400)
        );
    }

    void GenerateObjectsInClusters()
    {
        foreach (var cluster in allClusters)
        {
            if (cluster.tier == 0) continue; // 외곽 클러스터는 오브젝트 생성 안함

            int objectCount = Random.Range(minObjectsPerCluster, maxObjectsPerCluster + 1);

            for (int i = 0; i < objectCount; i++)
            {
                Vector2 objectPos;
                float objectRot;
                int attempts = 0;

                do
                {
                    // 클러스터 내 랜덤 위치
                    objectPos = GetRandomPositionInCircle(cluster.center, cluster.radius - 20f);
                    objectRot = Random.Range(0f, 360f);
                    attempts++;
                } while (IsPositionOccupied(cluster, objectPos) && attempts < 50);

                if (attempts < 50)
                {
                    cluster.objectPositions.Add(objectPos);
                    cluster.objectRotations.Add(objectRot);
                }
            }
        }
    }

    bool IsPositionOccupied(Cluster cluster, Vector2 position)
    {
        for (int i = 0; i < cluster.objectPositions.Count; i++)
        {
            float distance = Vector2.Distance(position, cluster.objectPositions[i]);
            if (distance < 15f)
            {
                return true;
            }
        }

        return false;
    }

    Vector2 GetRandomPositionInRing(Vector2 center, float minRadius, float maxRadius)
    {
        float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
        float radius = Random.Range(minRadius, maxRadius);

        return center + new Vector2(
            Mathf.Cos(angle) * radius,
            Mathf.Sin(angle) * radius
        );
    }

    Vector2 GetRandomPositionInCircle(Vector2 center, float radius)
    {
        float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
        float distance = Random.Range(0f, radius);

        return center + new Vector2(
            Mathf.Cos(angle) * distance,
            Mathf.Sin(angle) * distance
        );
    }

    void VisualizeMap()
    {
        foreach (var cluster in allClusters)
        {
            // 클러스터 중심 표시
            GameObject clusterObj = new GameObject($"Cluster_Tier{cluster.tier}");
            clusterObj.transform.parent = transform;
            clusterObj.transform.position = cluster.center;

            // 클러스터 경계 시각화 (LineRenderer 사용)
            LineRenderer lr = clusterObj.AddComponent<LineRenderer>();
            lr.sharedMaterial = new Material(Shader.Find("Sprites/Default"));
            lr.startWidth = 2f;
            lr.endWidth = 2f;
            lr.useWorldSpace = false;

            if (cluster.tier == 0) // 외곽 클러스터
            {
                lr.startColor = Color.red;
                lr.endColor = Color.red;
                lr.positionCount = cluster.boundaryPoints.Count + 1;

                for (int i = 0; i < cluster.boundaryPoints.Count; i++)
                {
                    Vector3 localPos = cluster.boundaryPoints[i] - cluster.center;
                    lr.SetPosition(i, localPos);
                }

                lr.SetPosition(cluster.boundaryPoints.Count, cluster.boundaryPoints[0] - cluster.center);
            }
            else // 내부 클러스터들
            {
                lr.startColor = GetTierColor(cluster.tier);
                lr.endColor = GetTierColor(cluster.tier);
                lr.positionCount = 33; // 원을 그리기 위한 점 개수

                for (int i = 0; i < 33; i++)
                {
                    float angle = (360f / 32f) * i * Mathf.Deg2Rad;
                    Vector3 pos = new Vector3(
                        Mathf.Cos(angle) * cluster.radius,
                        Mathf.Sin(angle) * cluster.radius,
                        0
                    );
                    lr.SetPosition(i, pos);
                }
            }

            // 클러스터 내 오브젝트들 생성
            for (int i = 0; i < cluster.objectPositions.Count; i++)
            {
                if (objectPrefab != null)
                {
                    GameObject obj = Instantiate(objectPrefab);
                    obj.transform.parent = clusterObj.transform;
                    obj.transform.position = cluster.objectPositions[i];
                    obj.transform.rotation = Quaternion.Euler(0, 0, cluster.objectRotations[i]);
                }
                else
                {
                    // 기본 원형 오브젝트 생성
                    GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    obj.transform.parent = clusterObj.transform;
                    obj.transform.position = cluster.objectPositions[i];
                    obj.transform.rotation = Quaternion.Euler(0, 0, cluster.objectRotations[i]);

                    // 2D용 설정
                    if (obj.GetComponent<SphereCollider>())
                        DestroyImmediate(obj.GetComponent<SphereCollider>());
                    obj.AddComponent<CircleCollider2D>();
                }
            }
        }

        // 외곽 영역 오브젝트들 시각화
        VisualizeOuterAreaObjects();
    }
    
    void GenerateOuterAreaObjects()
    {
        // 외곽 클러스터 찾기
        Cluster outerCluster = allClusters.Find(c => c.tier == 0);
        if (outerCluster == null) return;

        int attempts = 0;
        int maxAttempts = outerAreaObjectDensity * 10; // 충분한 시도 횟수

        while (outerAreaObjects.Count < outerAreaObjectDensity && attempts < maxAttempts)
        {
            attempts++;

            // 맵 전체 영역에서 랜덤 위치 생성
            Vector2 randomPos = new Vector2(
                Random.Range(mapCenter.x - mapSize * 0.5f, mapCenter.x + mapSize * 0.5f),
                Random.Range(mapCenter.y - mapSize * 0.5f, mapCenter.y + mapSize * 0.5f)
            );
            
            Debug.Log(mapSize);

            float objectRot = Random.Range(0f, 360f);

            // 외곽 클러스터 바깥인지 확인
            if (IsOutsideOuterCluster(randomPos, outerCluster) &&
                !IsOuterObjectOverlapping(randomPos))
            {
                outerAreaObjects.Add(randomPos);
                outerAreaObjectRotations.Add(objectRot);
            }
        }
    }
    
    bool IsOutsideOuterCluster(Vector2 position, Cluster outerCluster)
    {
        // 점이 불규칙한 다각형(외곽 클러스터) 내부에 있는지 확인
        // Ray casting 알고리즘 사용
        int intersectCount = 0;

        for (int i = 0; i < outerCluster.boundaryPoints.Count; i++)
        {
            Vector2 p1 = outerCluster.boundaryPoints[i];
            Vector2 p2 = outerCluster.boundaryPoints[(i + 1) % outerCluster.boundaryPoints.Count];

            if (DoesRayIntersectSegment(position, p1, p2))
            {
                intersectCount++;
            }
        }

        // 홀수면 내부, 짝수면 외부
        return intersectCount % 2 == 0;
    }
    
    bool DoesRayIntersectSegment(Vector2 rayOrigin, Vector2 segmentP1, Vector2 segmentP2)
    {
        // 오른쪽으로 향하는 ray와 선분의 교차점 확인
        float rayY = rayOrigin.y;

        // 선분이 ray와 교차할 가능성 확인
        if ((segmentP1.y > rayY) == (segmentP2.y > rayY)) return false;

        // 교차점의 x 좌표 계산
        float intersectX =
            segmentP1.x + (rayY - segmentP1.y) * (segmentP2.x - segmentP1.x) / (segmentP2.y - segmentP1.y);

        // ray 원점보다 오른쪽에 있는지 확인
        return intersectX > rayOrigin.x;
    }
    
    bool IsOuterObjectOverlapping(Vector2 position)
    {
        // 다른 외곽 오브젝트들과 겹치는지 확인
        for (int i = 0; i < outerAreaObjects.Count; i++)
        {
            float distance = Vector2.Distance(position, outerAreaObjects[i]);
            if (distance < 1f) // 1f 여유 공간
            {
                return true;
            }
        }

        return false;
    }
    
    void VisualizeOuterAreaObjects()
    {
        GameObject outerAreaParent = new GameObject("OuterAreaObjects");
        outerAreaParent.transform.parent = transform;

        for (int i = 0; i < outerAreaObjects.Count; i++)
        {
            if (outerAreaObjectPrefab != null)
            {
                GameObject obj = Instantiate(outerAreaObjectPrefab);
                obj.transform.parent = outerAreaParent.transform;
                obj.transform.position = outerAreaObjects[i];
                obj.transform.rotation = Quaternion.Euler(0f, 0f, outerAreaObjectRotations[i]);
            }
            else
            {
                // 기본 사각형 오브젝트 생성 (외곽 영역임을 구분하기 위해)
                GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                obj.transform.parent = outerAreaParent.transform;
                obj.transform.position = outerAreaObjects[i];
                obj.transform.rotation = Quaternion.Euler(0f, 0f, outerAreaObjectRotations[i]);

                // 2D용 설정
                if (obj.GetComponent<BoxCollider>())
                    DestroyImmediate(obj.GetComponent<BoxCollider>());
                obj.AddComponent<BoxCollider2D>();

                // 외곽 영역 오브젝트 색상 설정 (회색)
                Renderer renderer = obj.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.sharedMaterial.color = Color.gray;
                }
            }
        }
    }

    Color GetTierColor(int tier)
    {
        switch (tier)
        {
            case 1: return Color.yellow;
            case 2: return Color.green;
            case 3: return Color.blue;
            default: return Color.white;
        }
    }

    // 에디터에서 테스트용
    [ContextMenu("Generate New Map")]
    public void GenerateNewMap()
    {
        GenerateWorldMap();
    }
}