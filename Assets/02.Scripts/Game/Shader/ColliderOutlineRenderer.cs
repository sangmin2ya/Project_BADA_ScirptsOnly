using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 콜라이더 외곽에 동적 테두리를 그리는 컴포넌트
/// [박상민 - 2025.08.07]
/// </summary>
public class ColliderOutlineRenderer : MonoBehaviour
{
    #region Serialized Fields
    [Header("Outline Settings")]
    [SerializeField] private Material _outlineMaterial;
    [SerializeField] private bool _autoCreateMaterial = true;

    [Header("Thickness Settings")]
    [SerializeField] private float _baseThickness = 0.2f;
    [SerializeField] private float _thicknessVariation = 0.1f;
    [SerializeField] private float _animationSpeed = 2.0f;
    [SerializeField, Range(0f, 1f)] private float _innerOffsetRatio = 0.5f; // 안쪽으로 들어가는 비율

    [Header("Smoothing")]
    [SerializeField] private bool _enableSmoothing = true;
    [SerializeField] private int _smoothingPasses = 2;
    [SerializeField] private float _smoothingStrength = 0.5f;

    [Header("Appearance")]
    [SerializeField] private Color _outlineColor = Color.white;
    [SerializeField] private float _alpha = 1.0f;

    [Header("Rendering")]
    [SerializeField] private int _sortingOrder = 100;
    [SerializeField] private string _sortingLayerName = "Floor"; // 기본값을 "Floor"로 설정

    [Header("Animation")]
    [SerializeField] private bool _enableAnimation = true;
    [SerializeField] private float _noiseScale = 5.0f;
    [SerializeField] private float _waveFrequency = 3.0f;

    [Header("Performance")]
    [SerializeField] private int _segmentCount = 100;
    [SerializeField] private bool _autoUpdate = true;
    [SerializeField] private float _updateInterval = 0.02f;

    [Header("Debug")]
    [SerializeField] private bool _showDebugNormals = false;
    [SerializeField] private bool _forceOutward = true;
    /// <summary>
    /// 아웃라인을 즉시 업데이트합니다
    /// </summary>
    public void UpdateOutlineImmediate()
    {
        if (!_isInitialized || _polygonCollider == null)
        {
            return;
        }

        try
        {
            CalculateOutlineVertices();
            BuildMesh();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"ColliderOutlineRenderer: 즉시 업데이트 중 오류 발생 - {e.Message}");
        }
    }

    /// <summary>
    /// 콜라이더 유효성을 확인합니다
    /// </summary>
    /// <returns>콜라이더가 유효한지 여부</returns>
    public bool IsColliderValid()
    {
        return _polygonCollider != null &&
               _allColliderPaths != null &&
               _allColliderPaths.Length > 0 &&
               _isInitialized;
    }

    /// <summary>
    /// 아웃라인 렌더링 상태를 확인합니다
    /// </summary>
    /// <returns>렌더링 가능한지 여부</returns>
    public bool IsRenderingEnabled()
    {
        return _meshRenderer != null && _meshRenderer.enabled && _outlineMesh != null;
    }
    #endregion

    #region Private Fields
    private PolygonCollider2D _polygonCollider;
    private Vector2[][] _allColliderPaths; // 모든 path의 점들
    private List<PathOutlineData> _pathOutlineDataList; // 각 path별 아웃라인 데이터
    private Vector3[] _outlineVertices;
    private Vector3[] _innerVertices;
    private Vector3[] _outerVertices;
    private Mesh _outlineMesh;
    private MeshRenderer _meshRenderer;
    private MeshFilter _meshFilter;
    private float _updateTimer;
    private bool _isInitialized = false;
    private float[] _segmentSeeds; // 각 세그먼트별 랜덤 시드
    private GameObject _outlineObject;

    /// <summary>
    /// 각 path별 아웃라인 데이터를 저장하는 구조체
    /// </summary>
    private struct PathOutlineData
    {
        public Vector3[] innerVertices;
        public Vector3[] outerVertices;
        public int segmentCount;
        public int startIndex; // 전체 vertex 배열에서의 시작 인덱스
    }
    #endregion

    #region Unity Lifecycle

    void Awake()
    {
        enabled = false; // 초기에는 비활성화
    }
    private void Start()
    {
        InitializeComponents();
        SetupOutlineMaterial();
        CreateOutlineMesh();
    }

    private void Update()
    {
        if (_autoUpdate && _enableAnimation && _isInitialized)
        {
            UpdateOutlineAnimation();
        }
    }

    private void OnDestroy()
    {
        CleanupResources();
    }
    #endregion

    #region Initialization Methods
    /// <summary>
    /// 컴포넌트들을 초기화합니다
    /// </summary>
    private void InitializeComponents()
    {
        _polygonCollider = GetComponent<PolygonCollider2D>();

        if (_polygonCollider == null)
        {
            return;
        }

        ExtractAllPaths();

        // 폴리곤의 winding order 확인 및 수정
        EnsureCounterClockwiseWinding();

        GenerateSegmentSeeds();

        // MeshRenderer와 MeshFilter 생성
        CreateMeshComponents();

        _isInitialized = true;
    }

    /// <summary>
    /// 폴리곤 콜라이더의 모든 path를 추출합니다
    /// </summary>
    private void ExtractAllPaths()
    {
        if (_polygonCollider == null) return;

        int pathCount = _polygonCollider.pathCount;
        _allColliderPaths = new Vector2[pathCount][];

        for (int pathIndex = 0; pathIndex < pathCount; pathIndex++)
        {
            Vector2[] pathPoints = _polygonCollider.GetPath(pathIndex);
            _allColliderPaths[pathIndex] = new Vector2[pathPoints.Length];
            System.Array.Copy(pathPoints, _allColliderPaths[pathIndex], pathPoints.Length);
        }
    }

    /// <summary>
    /// 폴리곤이 반시계방향으로 정렬되도록 보장합니다
    /// </summary>
    private void EnsureCounterClockwiseWinding()
    {
        if (_allColliderPaths == null) return;

        for (int pathIndex = 0; pathIndex < _allColliderPaths.Length; pathIndex++)
        {
            Vector2[] pathPoints = _allColliderPaths[pathIndex];
            if (pathPoints.Length < 3) continue;

            // 폴리곤 면적을 계산하여 winding order 확인
            float _area = 0f;
            for (int i = 0; i < pathPoints.Length; i++)
            {
                Vector2 _current = pathPoints[i];
                Vector2 _next = pathPoints[(i + 1) % pathPoints.Length];
                _area += (_current.x * _next.y - _next.x * _current.y);
            }

            // 면적이 음수면 시계방향이므로 뒤집기
            if (_area < 0)
            {
                System.Array.Reverse(pathPoints);
            }
        }
    }

    /// <summary>
    /// 메시 컴포넌트들을 생성합니다
    /// </summary>
    private void CreateMeshComponents()
    {
        if (_outlineObject == null)
        {
            // 새 게임오브젝트 생성
            _outlineObject = new GameObject("ColliderOutline");
        }
        _outlineObject.transform.SetParent(transform);
        _outlineObject.transform.localPosition = Vector3.zero;
        _outlineObject.transform.localRotation = Quaternion.identity;
        _outlineObject.transform.localScale = Vector3.one;

        // 컴포넌트 추가
        _outlineObject.AddComponent<MeshFilter>();
        _outlineObject.AddComponent<MeshRenderer>();
        _meshFilter = _outlineObject.GetComponent<MeshFilter>();
        _meshRenderer = _outlineObject.GetComponent<MeshRenderer>();

        // 메시 생성
        _outlineMesh = new Mesh();
        _outlineMesh.name = "ColliderOutline";
        _meshFilter.mesh = _outlineMesh;
    }

    /// <summary>
    /// 세그먼트별 랜덤 시드를 생성합니다
    /// </summary>
    private void GenerateSegmentSeeds()
    {
        _segmentSeeds = new float[_segmentCount];
        for (int i = 0; i < _segmentCount; i++)
        {
            _segmentSeeds[i] = Random.Range(0f, 1000f);
        }
    }

    /// <summary>
    /// 아웃라인 머티리얼을 설정합니다
    /// </summary>
    private void SetupOutlineMaterial()
    {
        if (_outlineMaterial == null && _autoCreateMaterial)
        {
            CreateDefaultMaterial();
        }

        if (_meshRenderer != null && _outlineMaterial != null)
        {
            _meshRenderer.material = _outlineMaterial;

            // 렌더링 우선순위 설정
            _meshRenderer.sortingOrder = _sortingOrder;
            _meshRenderer.sortingLayerName = _sortingLayerName;

            UpdateMaterialProperties();
        }
    }

    /// <summary>
    /// 기본 머티리얼을 생성합니다
    /// </summary>
    private void CreateDefaultMaterial()
    {
        Shader _outlineShader = Shader.Find("Sprites/Default");
        if (_outlineShader == null)
        {
            _outlineShader = Shader.Find("Universal Render Pipeline/2D/Sprite-Lit-Default");
        }
        if (_outlineShader == null)
        {
            _outlineShader = Shader.Find("Standard");
        }

        _outlineMaterial = new Material(_outlineShader);
        _outlineMaterial.name = "ColliderOutline_Material";
    }

    /// <summary>
    /// 머티리얼 속성을 업데이트합니다
    /// </summary>
    private void UpdateMaterialProperties()
    {
        if (_outlineMaterial == null) return;

        _outlineMaterial.color = new Color(_outlineColor.r, _outlineColor.g, _outlineColor.b, _alpha);

        // Inner Offset 값을 셰이더에 전달 (음수로 설정하여 안쪽 확장 효과)
        if (_outlineMaterial.HasProperty("_InnerOffset"))
        {
            _outlineMaterial.SetFloat("_InnerOffset", _innerOffsetRatio * _baseThickness);
        }

        // Transparent 모드 설정
        if (_outlineMaterial.HasProperty("_Mode"))
        {
            _outlineMaterial.SetFloat("_Mode", 3); // Transparent
        }

        if (_outlineMaterial.HasProperty("_SrcBlend"))
        {
            _outlineMaterial.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        }

        if (_outlineMaterial.HasProperty("_DstBlend"))
        {
            _outlineMaterial.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        }
    }

    /// <summary>
    /// 외부에서 콜라이더를 변경할때 호출하는 메서드
    /// </summary>
    public void ChangeCollider(PolygonCollider2D newCollider)
    {
        if (_polygonCollider == newCollider)
        {
            return;
        }
        _isInitialized = false;

        if (newCollider == null)
        {
            if (_polygonCollider.enabled)
            {
                _isInitialized = true;
                return;
            }
            ResetData();
            DisableOutline();
            return;
        }
        ResetData();

        _polygonCollider = newCollider;

        // 좌표계 변환 처리 - 모든 path에 대해
        if (newCollider.transform != this.transform)
        {
            int pathCount = newCollider.pathCount;
            _allColliderPaths = new Vector2[pathCount][];

            for (int pathIndex = 0; pathIndex < pathCount; pathIndex++)
            {
                Vector2[] originalPath = newCollider.GetPath(pathIndex);
                Vector2[] transformedPath = new Vector2[originalPath.Length];

                for (int i = 0; i < originalPath.Length; i++)
                {
                    Vector3 worldPoint = newCollider.transform.TransformPoint(originalPath[i]);
                    transformedPath[i] = this.transform.InverseTransformPoint(worldPoint);
                }

                _allColliderPaths[pathIndex] = transformedPath;
            }
        }
        else
        {
            // 같은 GameObject의 콜라이더인 경우
            ExtractAllPaths();
        }

        // 폴리곤의 winding order 확인 및 수정
        EnsureCounterClockwiseWinding();

        GenerateSegmentSeeds();

        // MeshRenderer와 MeshFilter 생성
        CreateMeshComponents();

        SetupOutlineMaterial();
        CreateOutlineMesh();
        EnableOutline();

        _isInitialized = true;
    }

    /// <summary>
    /// 디버깅용: 점 배열의 경계 크기를 계산합니다
    /// </summary>
    private Vector2 GetArrayBounds(Vector2[] points)
    {
        if (points == null || points.Length == 0) return Vector2.zero;

        float minX = points[0].x, maxX = points[0].x;
        float minY = points[0].y, maxY = points[0].y;

        for (int i = 1; i < points.Length; i++)
        {
            if (points[i].x < minX) minX = points[i].x;
            if (points[i].x > maxX) maxX = points[i].x;
            if (points[i].y < minY) minY = points[i].y;
            if (points[i].y > maxY) maxY = points[i].y;
        }

        return new Vector2(maxX - minX, maxY - minY);
    }

    private void ResetData()
    {
        _polygonCollider = null;
        _allColliderPaths = null;
        _pathOutlineDataList = null;
        _outlineVertices = null;
        _innerVertices = null;
        _outerVertices = null;
        _isInitialized = false;
        if (_outlineObject != null)
        {
            DestroyImmediate(_outlineObject);
            _outlineObject = null;
        }
        if (_outlineMesh != null)
        {
            DestroyImmediate(_outlineMesh);
            _outlineMesh = null;
        }
    }
    #endregion

    #region Mesh Generation
    /// <summary>
    /// 아웃라인 메시를 생성합니다
    /// </summary>
    private void CreateOutlineMesh()
    {
        if (_allColliderPaths == null || _allColliderPaths.Length == 0)
        {
            return;
        }

        CalculateOutlineVertices();
        BuildMesh();
    }

    /// <summary>
    /// 아웃라인 버텍스들을 계산합니다 (모든 path 처리)
    /// </summary>
    private void CalculateOutlineVertices()
    {
        List<Vector3> _allInnerVerts = new List<Vector3>();
        List<Vector3> _allOuterVerts = new List<Vector3>();
        _pathOutlineDataList = new List<PathOutlineData>();

        int totalSegments = 0;

        // 각 path별로 독립적으로 처리
        for (int pathIndex = 0; pathIndex < _allColliderPaths.Length; pathIndex++)
        {
            Vector2[] pathPoints = _allColliderPaths[pathIndex];
            if (pathPoints.Length < 3) continue;

            int pathSegmentCount = Mathf.Max(10, _segmentCount / _allColliderPaths.Length);

            // 해당 path의 둘레 계산
            float pathPerimeter = CalculatePathPerimeter(pathPoints);
            if (pathPerimeter <= 0) continue;

            // 해당 path의 독립적인 vertex 리스트
            List<Vector3> _pathInnerVerts = new List<Vector3>();
            List<Vector3> _pathOuterVerts = new List<Vector3>();

            // 미리 해당 path의 세그먼트 두께를 계산
            float[] _thicknesses = new float[pathSegmentCount];
            for (int i = 0; i < pathSegmentCount; i++)
            {
                int globalSegmentIndex = totalSegments + i;
                _thicknesses[i] = CalculateDynamicThickness(globalSegmentIndex, Time.time);
            }

            // 부드러운 스무딩 적용
            if (_enableSmoothing)
            {
                for (int pass = 0; pass < _smoothingPasses; pass++)
                {
                    float[] _tempThicknesses = new float[pathSegmentCount];

                    for (int i = 0; i < pathSegmentCount; i++)
                    {
                        int _prev = (i - 1 + pathSegmentCount) % pathSegmentCount;
                        int _next = (i + 1) % pathSegmentCount;

                        float _smoothed = (_thicknesses[_prev] * 0.2f + _thicknesses[i] * 0.6f + _thicknesses[_next] * 0.2f);
                        _tempThicknesses[i] = Mathf.Lerp(_thicknesses[i], _smoothed, _smoothingStrength);
                    }

                    _thicknesses = _tempThicknesses;
                }
            }

            // 해당 path의 버텍스 생성
            for (int i = 0; i < pathSegmentCount; i++)
            {
                float _t = (float)i / pathSegmentCount;

                Vector2 _pointOnEdge = GetPointOnPathEdge(pathPoints, _t);
                Vector2 _normal = GetNormalAtPathPoint(pathPoints, _t);

                float _dynamicThickness = _thicknesses[i];

                // 콜라이더 경계에서 안쪽과 바깥쪽으로 확장
                float _innerOffset = _dynamicThickness * _innerOffsetRatio; // 안쪽으로 들어가는 양
                float _outerOffset = _dynamicThickness * (1f - _innerOffsetRatio); // 바깥쪽으로 나가는 양

                Vector3 _innerPoint = transform.TransformPoint(_pointOnEdge - _normal * _innerOffset);
                Vector3 _outerPoint = transform.TransformPoint(_pointOnEdge + _normal * _outerOffset);

                _pathInnerVerts.Add(_innerPoint);
                _pathOuterVerts.Add(_outerPoint);
            }

            // Path 데이터 저장
            PathOutlineData pathData = new PathOutlineData
            {
                innerVertices = _pathInnerVerts.ToArray(),
                outerVertices = _pathOuterVerts.ToArray(),
                segmentCount = pathSegmentCount,
                startIndex = _allInnerVerts.Count
            };
            _pathOutlineDataList.Add(pathData);

            // 전체 리스트에 추가
            _allInnerVerts.AddRange(_pathInnerVerts);
            _allOuterVerts.AddRange(_pathOuterVerts);

            totalSegments += pathSegmentCount;
        }

        _innerVertices = _allInnerVerts.ToArray();
        _outerVertices = _allOuterVerts.ToArray();
    }

    /// <summary>
    /// 특정 path의 둘레를 계산합니다
    /// </summary>
    private float CalculatePathPerimeter(Vector2[] pathPoints)
    {
        float _perimeter = 0f;
        for (int i = 0; i < pathPoints.Length; i++)
        {
            Vector2 _current = pathPoints[i];
            Vector2 _next = pathPoints[(i + 1) % pathPoints.Length];
            _perimeter += Vector2.Distance(_current, _next);
        }
        return _perimeter;
    }

    /// <summary>
    /// 특정 path의 경계선 상의 점을 가져옵니다
    /// </summary>
    private Vector2 GetPointOnPathEdge(Vector2[] pathPoints, float t)
    {
        if (pathPoints.Length < 2) return Vector2.zero;

        float _totalLength = CalculatePathPerimeter(pathPoints);
        float _targetLength = t * _totalLength;
        float _currentLength = 0f;

        for (int i = 0; i < pathPoints.Length; i++)
        {
            Vector2 _current = pathPoints[i];
            Vector2 _next = pathPoints[(i + 1) % pathPoints.Length];
            float _segmentLength = Vector2.Distance(_current, _next);

            if (_currentLength + _segmentLength >= _targetLength)
            {
                float _localT = (_targetLength - _currentLength) / _segmentLength;
                return Vector2.Lerp(_current, _next, _localT);
            }

            _currentLength += _segmentLength;
        }

        return pathPoints[0];
    }

    /// <summary>
    /// 특정 path의 특정 지점에서의 법선 벡터를 계산합니다
    /// </summary>
    private Vector2 GetNormalAtPathPoint(Vector2[] pathPoints, float t)
    {
        float _totalLength = CalculatePathPerimeter(pathPoints);
        float _targetLength = t * _totalLength;
        float _currentLength = 0f;

        for (int i = 0; i < pathPoints.Length; i++)
        {
            Vector2 _current = pathPoints[i];
            Vector2 _next = pathPoints[(i + 1) % pathPoints.Length];
            float _segmentLength = Vector2.Distance(_current, _next);

            if (_currentLength + _segmentLength >= _targetLength)
            {
                Vector2 _direction = (_next - _current).normalized;
                Vector2 _outwardNormal = new Vector2(_direction.y, -_direction.x);

                if (_forceOutward)
                {
                    Vector2 _center = GetPathCenter(pathPoints);
                    Vector2 _pointOnEdge = GetPointOnPathEdge(pathPoints, t);
                    Vector2 _centerToPoint = (_pointOnEdge - _center).normalized;

                    if (Vector2.Dot(_outwardNormal, _centerToPoint) < 0)
                    {
                        _outwardNormal = -_outwardNormal;
                    }
                }

                return _outwardNormal;
            }

            _currentLength += _segmentLength;
        }

        return Vector2.up;
    }

    /// <summary>
    /// 특정 path의 중심점을 계산합니다
    /// </summary>
    private Vector2 GetPathCenter(Vector2[] pathPoints)
    {
        Vector2 _center = Vector2.zero;
        for (int i = 0; i < pathPoints.Length; i++)
        {
            _center += pathPoints[i];
        }
        return _center / pathPoints.Length;
    }

    /// <summary>
    /// 동적 두께를 계산합니다 (부드러운 변화)
    /// </summary>
    /// <param name="segmentIndex">세그먼트 인덱스</param>
    /// <param name="time">현재 시간</param>
    /// <returns>계산된 두께</returns>
    private float CalculateDynamicThickness(int segmentIndex, float time)
    {
        if (!_enableAnimation)
        {
            return _baseThickness;
        }

        float _seed = _segmentSeeds[segmentIndex];
        float _normalizedIndex = (float)segmentIndex / _segmentCount;

        // 부드러운 노이즈 레이어들 (낮은 주파수 = 부드러운 변화)
        float _noise1 = Mathf.PerlinNoise(_normalizedIndex * 2.0f + time * _animationSpeed * 0.3f, _seed);
        float _noise2 = Mathf.PerlinNoise(_normalizedIndex * 4.0f + time * _animationSpeed * 0.5f, _seed + 100f);
        float _noise3 = Mathf.PerlinNoise(_normalizedIndex * 1.0f + time * _animationSpeed * 0.2f, _seed + 200f);

        // 부드러운 사인파들
        float _wave1 = Mathf.Sin(_normalizedIndex * Mathf.PI * _waveFrequency + time * _animationSpeed) * 0.5f + 0.5f;
        float _wave2 = Mathf.Sin(_normalizedIndex * Mathf.PI * _waveFrequency * 0.7f + time * _animationSpeed * 1.3f) * 0.5f + 0.5f;

        // 부드러운 가중치로 합성
        float _combinedNoise = _noise1 * 0.4f + _noise2 * 0.25f + _noise3 * 0.15f + _wave1 * 0.1f + _wave2 * 0.1f;

        // 스무스스텝 적용으로 더욱 부드럽게
        _combinedNoise = Mathf.SmoothStep(0.2f, 0.8f, _combinedNoise);

        // 두께 계산 (변화폭을 줄여서 더 자연스럽게)
        float _thickness = _baseThickness + (_combinedNoise - 0.5f) * _thicknessVariation * 0.8f;

        return Mathf.Max(_thickness, _baseThickness * 0.3f); // 최소 두께 보장
    }

    /// <summary>
    /// 메시를 구축합니다 (각 path를 독립적으로 처리)
    /// </summary>
    private void BuildMesh()
    {
        if (_innerVertices == null || _outerVertices == null || _pathOutlineDataList == null)
        {
            return;
        }

        if (_innerVertices.Length != _outerVertices.Length)
        {
            return;
        }

        int _vertexCount = _innerVertices.Length * 2;

        // 각 path별로 독립적인 삼각형 생성을 위해 삼각형 수 계산
        int _triangleCount = 0;
        foreach (var pathData in _pathOutlineDataList)
        {
            _triangleCount += pathData.segmentCount * 6; // 각 세그먼트마다 2개 삼각형 = 6개 인덱스
        }

        Vector3[] _vertices = new Vector3[_vertexCount];
        Vector2[] _uvs = new Vector2[_vertexCount];
        int[] _triangles = new int[_triangleCount];

        // 버텍스와 UV 설정
        for (int i = 0; i < _innerVertices.Length; i++)
        {
            _vertices[i * 2] = transform.InverseTransformPoint(_innerVertices[i]);
            _vertices[i * 2 + 1] = transform.InverseTransformPoint(_outerVertices[i]);

            float _u = (float)i / (_innerVertices.Length - 1);
            _uvs[i * 2] = new Vector2(_u, 0f);
            _uvs[i * 2 + 1] = new Vector2(_u, 1f);
        }

        // 각 path별로 독립적인 삼각형 생성
        int triangleIndex = 0;

        foreach (var pathData in _pathOutlineDataList)
        {
            int pathStartVertex = pathData.startIndex;
            int pathSegmentCount = pathData.segmentCount;

            // 해당 path 내에서만 삼각형 생성
            for (int i = 0; i < pathSegmentCount - 1; i++)
            {
                int _vertIndex = (pathStartVertex + i) * 2;

                // 첫 번째 삼각형
                _triangles[triangleIndex++] = _vertIndex;
                _triangles[triangleIndex++] = _vertIndex + 2;
                _triangles[triangleIndex++] = _vertIndex + 1;

                // 두 번째 삼각형
                _triangles[triangleIndex++] = _vertIndex + 1;
                _triangles[triangleIndex++] = _vertIndex + 2;
                _triangles[triangleIndex++] = _vertIndex + 3;
            }

            // 해당 path의 마지막 세그먼트 (path 내에서만 루프 닫기)
            int _lastVertIndex = (pathStartVertex + pathSegmentCount - 1) * 2;
            int _firstVertIndex = pathStartVertex * 2;

            _triangles[triangleIndex++] = _lastVertIndex;
            _triangles[triangleIndex++] = _firstVertIndex;
            _triangles[triangleIndex++] = _lastVertIndex + 1;

            _triangles[triangleIndex++] = _lastVertIndex + 1;
            _triangles[triangleIndex++] = _firstVertIndex;
            _triangles[triangleIndex++] = _firstVertIndex + 1;
        }

        // 메시 업데이트
        _outlineMesh.Clear();
        _outlineMesh.vertices = _vertices;
        _outlineMesh.uv = _uvs;
        _outlineMesh.triangles = _triangles;
        _outlineMesh.RecalculateNormals();
        _outlineMesh.RecalculateBounds();

        //Debug.Log($"ColliderOutlineRenderer: 메시 생성 완료 - {gameObject.name} 에 {_pathOutlineDataList.Count}개 path, {_vertexCount}개 vertex, {_triangleCount/3}개 삼각형");
    }
    #endregion

    #region Update Methods
    /// <summary>
    /// 아웃라인 애니메이션을 업데이트합니다
    /// </summary>
    private void UpdateOutlineAnimation()
    {
        _updateTimer += Time.deltaTime;

        if (_updateTimer >= _updateInterval)
        {
            CalculateOutlineVertices();
            BuildMesh();
            _updateTimer = 0f;
        }
    }
    #endregion

    #region Public Methods
    /// <summary>
    /// 기본 두께를 설정합니다
    /// </summary>
    /// <param name="thickness">기본 두께</param>
    public void SetBaseThickness(float thickness)
    {
        _baseThickness = thickness;
    }

    /// <summary>
    /// 두께 변화량을 설정합니다
    /// </summary>
    /// <param name="variation">변화량</param>
    public void SetThicknessVariation(float variation)
    {
        _thicknessVariation = variation;
    }

    /// <summary>
    /// 애니메이션 속도를 설정합니다
    /// </summary>
    /// <param name="speed">애니메이션 속도</param>
    public void SetAnimationSpeed(float speed)
    {
        _animationSpeed = speed;
    }

    /// <summary>
    /// 아웃라인 색상을 설정합니다
    /// </summary>
    /// <param name="color">아웃라인 색상</param>
    public void SetOutlineColor(Color color)
    {
        _outlineColor = color;
        UpdateMaterialProperties();
    }

    /// <summary>
    /// 애니메이션을 활성화/비활성화합니다
    /// </summary>
    /// <param name="enable">활성화 여부</param>
    public void SetAnimationEnabled(bool enable)
    {
        _enableAnimation = enable;
    }

    /// <summary>
    /// 콜라이더가 변경되었을 때 아웃라인을 재생성합니다
    /// </summary>
    public void RefreshOutline()
    {
        if (!_isInitialized || _polygonCollider == null)
        {
            Debug.LogWarning("ColliderOutlineRenderer: 초기화되지 않았거나 콜라이더가 없습니다.");
            return;
        }

        try
        {
            ExtractAllPaths();

            if (_allColliderPaths == null || _allColliderPaths.Length == 0)
            {
                Debug.LogWarning("ColliderOutlineRenderer: 유효한 path가 없습니다.");
                DisableOutline();
                return;
            }

            EnsureCounterClockwiseWinding();
            GenerateSegmentSeeds();
            CreateOutlineMesh();
            EnableOutline();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"ColliderOutlineRenderer: 아웃라인 새로고침 중 오류 발생 - {e.Message}");
            DisableOutline();
        }
    }

    public void ToggleOutline(bool enable)
    {
        if (enable)
        {
            EnableOutline();
        }
        else
        {
            DisableOutline();
        }
    }

    private void EnableOutline()
    {
        if (_meshRenderer != null)
        {
            _meshRenderer.enabled = true;
        }
    }

    private void DisableOutline()
    {
        if (_meshRenderer != null)
        {
            _meshRenderer.enabled = false;
        }
    }

    /// <summary>
    /// 렌더링 우선순위를 설정합니다
    /// </summary>
    /// <param name="sortingOrder">소팅 오더</param>
    /// <param name="sortingLayerName">소팅 레이어 이름</param>
    public void SetRenderingOrder(int sortingOrder, string sortingLayerName = "Default")
    {
        _sortingOrder = sortingOrder;
        _sortingLayerName = sortingLayerName;

        if (_meshRenderer != null)
        {
            _meshRenderer.sortingOrder = _sortingOrder;
            _meshRenderer.sortingLayerName = _sortingLayerName;
        }
    }
    #endregion

    #region Cleanup
    /// <summary>
    /// 리소스를 정리합니다
    /// </summary>
    private void CleanupResources()
    {
        if (_outlineMesh != null)
        {
            DestroyImmediate(_outlineMesh);
        }

        if (_meshRenderer != null)
        {
            DestroyImmediate(_meshRenderer.gameObject);
        }
    }
    #endregion

    #region Gizmos
    private void OnDrawGizmos()
    {
        if (_polygonCollider != null && _allColliderPaths != null)
        {
            // 모든 path의 콜라이더 외곽선 그리기 (녹색)
            Gizmos.color = Color.green;
            for (int pathIndex = 0; pathIndex < _allColliderPaths.Length; pathIndex++)
            {
                Vector2[] pathPoints = _allColliderPaths[pathIndex];
                for (int i = 0; i < pathPoints.Length; i++)
                {
                    Vector3 _worldPos1 = transform.TransformPoint(pathPoints[i]);
                    Vector3 _worldPos2 = transform.TransformPoint(pathPoints[(i + 1) % pathPoints.Length]);
                    Gizmos.DrawLine(_worldPos1, _worldPos2);
                }
            }

            // 아웃라인 버텍스 그리기 (흰색)
            if (_outerVertices != null)
            {
                Gizmos.color = Color.white;
                for (int i = 0; i < _outerVertices.Length; i++)
                {
                    Gizmos.DrawWireSphere(_outerVertices[i], 0.02f);
                }
            }

            // 내부 버텍스 그리기 (파란색)
            if (_innerVertices != null)
            {
                Gizmos.color = Color.blue;
                for (int i = 0; i < _innerVertices.Length; i++)
                {
                    Gizmos.DrawWireSphere(_innerVertices[i], 0.015f);
                }
            }

            // 법선 벡터 그리기 (디버그용) - 첫 번째 path만
            if (_showDebugNormals && _isInitialized && _allColliderPaths.Length > 0)
            {
                Gizmos.color = Color.red;
                Vector2[] firstPath = _allColliderPaths[0];

                for (int i = 0; i < 20; i++) // 20개만 그리기
                {
                    float _t = (float)i / 20f;
                    Vector2 _pointOnEdge = GetPointOnPathEdge(firstPath, _t);
                    Vector2 _normal = GetNormalAtPathPoint(firstPath, _t);

                    Vector3 _worldPoint = transform.TransformPoint(_pointOnEdge);
                    Vector3 _worldNormal = transform.TransformDirection(_normal);

                    Gizmos.DrawRay(_worldPoint, _worldNormal * _baseThickness);
                }
            }

            // 각 path의 중심점 그리기 (노란색)
            if (_allColliderPaths != null)
            {
                Gizmos.color = Color.yellow;
                for (int pathIndex = 0; pathIndex < _allColliderPaths.Length; pathIndex++)
                {
                    Vector3 _worldCenter = transform.TransformPoint(GetPathCenter(_allColliderPaths[pathIndex]));
                    Gizmos.DrawWireSphere(_worldCenter, 0.05f);
                }
            }
        }
    }
    #endregion
}