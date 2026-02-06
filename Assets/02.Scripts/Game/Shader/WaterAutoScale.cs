using UnityEngine;

/// <summary>
/// 카메라의 size에 따라 셰이더 값을 동일하게 보이도록 조정
/// [박상민 - 2025.08.07]
/// </summary>
public class WaterAutoScale : MonoBehaviour
{
    #region Serialized Fields
    [Header("Water Material")]
    [SerializeField] private Material _waterMaterial;

    [Header("Reference Camera Size")]
    [SerializeField] private float _referenceCameraSize = 40f;

    [Header("Base Edge Thickness Values (at reference size)")]
    [SerializeField] private float _baseEdgeThicknessMin;
    [SerializeField] private float _baseEdgeThicknessMax;

    [Header("UVScale")]
    [SerializeField] private Vector2 _uvScale;

    [Header("Auto Update")]
    [SerializeField] private bool _isAutoUpdateEnabled = true;

    [Header("Performance Settings")]
    [SerializeField] private float _sizeChangeThreshold = 0.001f;
    [SerializeField] private bool _updateThicknessOnChange = true;
    [SerializeField] private bool _updateUVScaleOnChange = false;
    #endregion

    #region Private Fields
    private Camera _mainCamera;
    private float _lastCameraSize;
    private float _cachedSizeRatio;
    private bool _isDirty = true;

    // 미리 계산된 값들 캐싱
    private float _cachedAdjustedMinThickness;
    private float _cachedAdjustedMaxThickness;
    private Vector2 _cachedUVScale;

    // 원본 값 백업용
    private bool _hasBackup = false;
    private float _originalEdgeThicknessMin;
    private float _originalEdgeThicknessMax;
    private Vector2 _originalUVScale;
    private Material _originalMaterial;
    #endregion

    #region Unity Lifecycle
    private void Start()
    {
        InitializeMainCamera();
        InitializeWaterMaterial();
        SetInitialValues();
    }

    private void Update()
    {
        if (!_isAutoUpdateEnabled || _mainCamera == null || _waterMaterial == null)
        {
            return;
        }

        CheckAndUpdateCameraSize();
    }

    private void OnDrawGizmosSelected()
    {
        DrawCameraGizmo();
    }

    private void OnDestroy()
    {
        RestoreOriginalValues();
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus)
        {
            RestoreOriginalValues();
        }
    }

    private void OnDisable()
    {
        RestoreOriginalValues();
    }
    #endregion

    #region Initialization Methods
    /// <summary>
    /// 메인 카메라를 초기화합니다
    /// </summary>
    private void InitializeMainCamera()
    {
        _mainCamera = Camera.main;

        if (_mainCamera == null)
        {
            _mainCamera = FindObjectOfType<Camera>();

            if (_mainCamera == null)
            {
                Debug.LogError("WaterAutoScale: 카메라를 찾을 수 없습니다. 카메라를 수동으로 할당해주세요.");
                return;
            }
        }
    }

    /// <summary>
    /// 워터 머티리얼을 초기화합니다
    /// </summary>
    private void InitializeWaterMaterial()
    {
        if (_waterMaterial == null)
        {
            Renderer _materialRenderer = GetComponent<Renderer>();

            if (_materialRenderer != null)
            {
                _waterMaterial = _materialRenderer.material;
            }
            else
            {
                Debug.LogError("WaterAutoScale: 워터 머티리얼을 찾을 수 없습니다.");
                return;
            }
        }

        // 원본 값 백업 먼저 실행
        BackupOriginalValues();

        // 그 다음 기본값 설정
        _baseEdgeThicknessMin = _waterMaterial.GetFloat("_EdgeThicknessMin");
        _baseEdgeThicknessMax = _waterMaterial.GetFloat("_EdgeThicknessMax");
        _uvScale = _waterMaterial.GetVector("_UVScale");
    }

    /// <summary>
    /// 원본 머티리얼 값들을 백업합니다
    /// </summary>
    private void BackupOriginalValues()
    {
        if (_waterMaterial != null && !_hasBackup)
        {
            _originalMaterial = _waterMaterial;

            // EdgeThickness 속성이 존재하는지 확인하고 백업
            if (_waterMaterial.HasProperty("_EdgeThicknessMin"))
            {
                _originalEdgeThicknessMin = _waterMaterial.GetFloat("_EdgeThicknessMin");
            }

            if (_waterMaterial.HasProperty("_EdgeThicknessMax"))
            {
                _originalEdgeThicknessMax = _waterMaterial.GetFloat("_EdgeThicknessMax");
            }

            // UVScale 속성 백업
            if (_waterMaterial.HasProperty("_UVScale"))
            {
                _originalUVScale = _waterMaterial.GetVector("_UVScale");
            }

            _hasBackup = true;
            Debug.Log($"WaterAutoScale: 원본 값 백업 완료 - EdgeMin: {_originalEdgeThicknessMin:F4}, EdgeMax: {_originalEdgeThicknessMax:F4}, UVScale: {_originalUVScale}");
        }
    }

    /// <summary>
    /// 초기 값들을 설정합니다
    /// </summary>
    private void SetInitialValues()
    {
        if (_mainCamera != null)
        {
            _lastCameraSize = _mainCamera.orthographicSize;
            _cachedSizeRatio = CalculateSizeRatio(_lastCameraSize);
            UpdateCachedThicknessValues();
            ApplyThicknessToMaterial();
            _isDirty = false;
        }
    }
    #endregion

    #region Update Methods
    /// <summary>
    /// 카메라 사이즈 변경을 확인하고 업데이트합니다
    /// </summary>
    private void CheckAndUpdateCameraSize()
    {
        float _currentCameraSize = _mainCamera.orthographicSize;

        // 임계값보다 큰 변화가 있을 때만 업데이트
        if (HasSignificantSizeChange(_currentCameraSize))
        {
            UpdateCameraRelatedValues(_currentCameraSize);
            ApplyThicknessToMaterial();
            LogThicknessUpdate(_currentCameraSize);
        }
    }

    /// <summary>
    /// 카메라 사이즈가 유의미하게 변경되었는지 확인합니다
    /// </summary>
    /// <param name="currentSize">현재 카메라 사이즈</param>
    /// <returns>유의미한 변화가 있으면 true</returns>
    private bool HasSignificantSizeChange(float currentSize)
    {
        return Mathf.Abs(currentSize - _lastCameraSize) > _sizeChangeThreshold;
    }

    /// <summary>
    /// 카메라 관련 값들을 업데이트합니다
    /// </summary>
    /// <param name="currentCameraSize">현재 카메라 사이즈</param>
    private void UpdateCameraRelatedValues(float currentCameraSize)
    {
        _lastCameraSize = currentCameraSize;
        _cachedSizeRatio = CalculateSizeRatio(currentCameraSize);
        UpdateCachedThicknessValues();
        _isDirty = true;
    }

    /// <summary>
    /// 캐시된 두께 값들을 업데이트합니다
    /// </summary>
    private void UpdateCachedThicknessValues()
    {
        if (_updateThicknessOnChange)
        {
            _cachedAdjustedMinThickness = CalculateAdjustedThickness(_baseEdgeThicknessMin, _cachedSizeRatio);
            _cachedAdjustedMaxThickness = CalculateAdjustedThickness(_baseEdgeThicknessMax, _cachedSizeRatio);
        }

        if (_updateUVScaleOnChange)
        {
            _cachedUVScale = new Vector2(
                _uvScale.x / _cachedSizeRatio,
                _uvScale.y / _cachedSizeRatio
            );
        }
    }

    /// <summary>
    /// 에지 두께를 업데이트합니다 (레거시 메서드 - 호환성 유지)
    /// </summary>
    private void UpdateEdgeThickness()
    {
        if (_mainCamera == null || _waterMaterial == null)
        {
            return;
        }

        float _currentCameraSize = _mainCamera.orthographicSize;
        UpdateCameraRelatedValues(_currentCameraSize);
        ApplyThicknessToMaterial();
        LogThicknessUpdate(_currentCameraSize);
    }
    #endregion

    #region Calculation Methods
    /// <summary>
    /// 카메라 사이즈 비율을 계산합니다
    /// </summary>
    /// <param name="currentCameraSize">현재 카메라 사이즈</param>
    /// <returns>계산된 사이즈 비율</returns>
    private float CalculateSizeRatio(float currentCameraSize)
    {
        return currentCameraSize / _referenceCameraSize;
    }

    /// <summary>
    /// 조정된 두께 값을 계산합니다
    /// </summary>
    /// <param name="baseThickness">기준 두께 값</param>
    /// <param name="sizeRatio">사이즈 비율</param>
    /// <returns>조정된 두께 값</returns>
    private float CalculateAdjustedThickness(float baseThickness, float sizeRatio)
    {
        return baseThickness * sizeRatio;
    }
    #endregion

    #region Material Methods
    /// <summary>
    /// 머티리얼에 캐시된 두께 값을 적용합니다
    /// </summary>
    private void ApplyThicknessToMaterial()
    {
        if (_waterMaterial == null)
        {
            return;
        }

        if (_updateThicknessOnChange)
        {
            _waterMaterial.SetFloat("_EdgeThicknessMin", _cachedAdjustedMinThickness);
            _waterMaterial.SetFloat("_EdgeThicknessMax", _cachedAdjustedMaxThickness);
        }

        if (_updateUVScaleOnChange)
        {
            _waterMaterial.SetVector("_UVScale", _cachedUVScale);
        }
    }
    #endregion

    #region Debug Methods
    /// <summary>
    /// 두께 업데이트 정보를 로그로 출력합니다
    /// </summary>
    /// <param name="cameraSize">카메라 사이즈</param>
    private void LogThicknessUpdate(float cameraSize)
    {
        if (_isDirty)
        {
            _isDirty = false;
        }
    }

    /// <summary>
    /// 카메라 범위를 기즈모로 표시합니다
    /// </summary>
    private void DrawCameraGizmo()
    {
        if (_mainCamera != null)
        {
            Gizmos.color = Color.cyan;
            Vector3 _gizmoSize = new Vector3(
                _mainCamera.orthographicSize * 2 * _mainCamera.aspect,
                _mainCamera.orthographicSize * 2,
                0.1f
            );
            Gizmos.DrawWireCube(_mainCamera.transform.position, _gizmoSize);
        }
    }
    #endregion

    #region Public Methods
    /// <summary>
    /// Inspector에서 수동으로 에지 두께를 업데이트합니다
    /// </summary>
    [ContextMenu("Update Edge Thickness")]
    public void ManualUpdateEdgeThickness()
    {
        UpdateEdgeThickness();
    }

    /// <summary>
    /// 원본 값으로 복원합니다
    /// </summary>
    [ContextMenu("Restore Original Values")]
    public void RestoreOriginalValues()
    {
        if (_hasBackup && _originalMaterial != null)
        {
            if (_originalMaterial.HasProperty("_EdgeThicknessMin"))
            {
                _originalMaterial.SetFloat("_EdgeThicknessMin", _originalEdgeThicknessMin);
            }

            if (_originalMaterial.HasProperty("_EdgeThicknessMax"))
            {
                _originalMaterial.SetFloat("_EdgeThicknessMax", _originalEdgeThicknessMax);
            }

            if (_originalMaterial.HasProperty("_UVScale"))
            {
                _originalMaterial.SetVector("_UVScale", _originalUVScale);
            }

            Debug.Log($"WaterAutoScale: 원본 값으로 복원 완료 - EdgeMin: {_originalEdgeThicknessMin:F4}, EdgeMax: {_originalEdgeThicknessMax:F4}, UVScale: {_originalUVScale}");
        }
    }

    /// <summary>
    /// 현재 값을 새로운 원본 값으로 설정합니다
    /// </summary>
    [ContextMenu("Set Current As Original")]
    public void SetCurrentAsOriginal()
    {
        if (_waterMaterial != null)
        {
            _hasBackup = false;
            BackupOriginalValues();
            Debug.Log("WaterAutoScale: 현재 값을 새로운 원본 값으로 설정했습니다.");
        }
    }

    /// <summary>
    /// 런타임에서 기준 사이즈를 변경합니다
    /// </summary>
    /// <param name="newReferenceSize">새로운 기준 사이즈</param>
    public void SetReferenceSize(float newReferenceSize)
    {
        _referenceCameraSize = newReferenceSize;
        ForceUpdateThickness();
    }

    /// <summary>
    /// 기본 두께 값들을 설정합니다
    /// </summary>
    /// <param name="minThickness">최소 두께 값</param>
    /// <param name="maxThickness">최대 두께 값</param>
    public void SetBaseThickness(float minThickness, float maxThickness)
    {
        _baseEdgeThicknessMin = minThickness;
        _baseEdgeThicknessMax = maxThickness;
        ForceUpdateThickness();
    }

    /// <summary>
    /// 기본 UV 스케일을 설정합니다
    /// </summary>
    /// <param name="newUVScale">새로운 UV 스케일</param>
    public void SetBaseUVScale(Vector2 newUVScale)
    {
        _uvScale = newUVScale;
        ForceUpdateThickness();
    }

    /// <summary>
    /// 자동 업데이트 기능을 활성화/비활성화합니다
    /// </summary>
    /// <param name="enabled">활성화 여부</param>
    public void SetAutoUpdateEnabled(bool enabled)
    {
        _isAutoUpdateEnabled = enabled;

        if (!enabled)
        {
            RestoreOriginalValues();
        }
        else
        {
            ForceUpdateThickness();
        }
    }

    /// <summary>
    /// 두께 업데이트 기능을 활성화/비활성화합니다
    /// </summary>
    /// <param name="enabled">활성화 여부</param>
    public void SetThicknessUpdateEnabled(bool enabled)
    {
        _updateThicknessOnChange = enabled;
    }

    /// <summary>
    /// UV 스케일 업데이트 기능을 활성화/비활성화합니다
    /// </summary>
    /// <param name="enabled">활성화 여부</param>
    public void SetUVScaleUpdateEnabled(bool enabled)
    {
        _updateUVScaleOnChange = enabled;
    }

    /// <summary>
    /// 강제로 두께 값을 업데이트합니다
    /// </summary>
    private void ForceUpdateThickness()
    {
        if (_mainCamera != null)
        {
            float _currentCameraSize = _mainCamera.orthographicSize;
            UpdateCameraRelatedValues(_currentCameraSize);
            ApplyThicknessToMaterial();
            LogThicknessUpdate(_currentCameraSize);
        }
    }
    #endregion
}