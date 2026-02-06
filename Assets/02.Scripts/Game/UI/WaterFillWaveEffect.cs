using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UI Image의 FillAmount에 따라 최상단 경계선이 물결치는 효과
/// </summary>
public class WaterFillWaveEffect : MonoBehaviour
{
    #region Fields
    
    [Header("대상 이미지")]
    [SerializeField] private Image waterImage;
    
    [Header("물결 설정")]
    [SerializeField] private float waveAmplitude = 10.0f;
    [SerializeField] private float waveFrequency = 2.0f;
    [SerializeField] private float waveSpeed = 1.0f;
    [SerializeField] private int waveResolution = 512;
    
    private RawImage _waveTopImage;
    private Texture2D _waveTexture;
    private RectTransform _waterRect;
    private RectTransform _waveRect;
    private float _waveTime;
    private int _textureHeight = 128;
    
    #endregion
    
    #region Unity Lifecycle
    
    private void Start()
    {
        SetupWaterWave();
    }
    
    private void Update()
    {
        UpdateWave();
    }
    
    private void OnDestroy()
    {
        if (_waveTexture != null)
        {
            DestroyImmediate(_waveTexture);
        }
    }
    
    #endregion
    
    #region Setup
    
    /// <summary>
    /// 물결 효과를 설정합니다
    /// </summary>
    private void SetupWaterWave()
    {
        // 기본 이미지 설정
        if (waterImage == null)
        {
            waterImage = GetComponent<Image>();
        }
        
        if (waterImage == null)
        {
            Debug.LogError("Water Image가 없습니다!");
            return;
        }
        
        _waterRect = waterImage.GetComponent<RectTransform>();
        
        // 물 이미지를 Vertical Fill로 설정
        waterImage.type = Image.Type.Filled;
        waterImage.fillMethod = Image.FillMethod.Vertical;
        waterImage.fillOrigin = 0; // 하단부터 채우기
        
        CreateWaveTopImage();
        CreateWaveTexture();
        
        Debug.Log("물결 효과 설정 완료");
    }
    
    /// <summary>
    /// 물결 상단 이미지를 생성합니다
    /// </summary>
    private void CreateWaveTopImage()
    {
        GameObject waveObject = new GameObject("WaveTop");
        waveObject.transform.SetParent(_waterRect.parent, false);
        waveObject.transform.SetSiblingIndex(_waterRect.GetSiblingIndex() + 1);
        
        _waveTopImage = waveObject.AddComponent<RawImage>();
        _waveRect = waveObject.GetComponent<RectTransform>();
        
        // 위치와 크기 설정
        _waveRect.anchorMin = _waterRect.anchorMin;
        _waveRect.anchorMax = _waterRect.anchorMax;
        _waveRect.anchoredPosition = _waterRect.anchoredPosition;
        _waveRect.sizeDelta = new Vector2(_waterRect.sizeDelta.x, _textureHeight);
        
        // 색상을 물 이미지와 동일하게
        _waveTopImage.color = waterImage.color;
    }
    
    /// <summary>
    /// 물결 텍스처를 생성합니다
    /// </summary>
    private void CreateWaveTexture()
    {
        _waveTexture = new Texture2D(waveResolution, _textureHeight, TextureFormat.RGBA32, true)
        {
            filterMode = FilterMode.Trilinear,
            wrapMode = TextureWrapMode.Clamp,
            anisoLevel = 16
        };
        
        _waveTopImage.texture = _waveTexture;
        
        UpdateWaveTexture();
    }
    
    #endregion
    
    #region Wave Update
    
    /// <summary>
    /// 물결을 업데이트합니다
    /// </summary>
    private void UpdateWave()
    {
        _waveTime += Time.deltaTime * waveSpeed;
        
        UpdateWaveTexture();
        UpdateWavePosition();
    }
    
    /// <summary>
    /// 물결 텍스처를 업데이트합니다
    /// </summary>
    private void UpdateWaveTexture()
    {
        if (_waveTexture == null)
        {
            return;
        }
        
        Color[] pixels = new Color[waveResolution * _textureHeight];
        
        for (int x = 0; x < waveResolution; x++)
        {
            float normalizedX = (float)x / waveResolution;
            
            // 물결 높이 계산 (부드러운 곡선)
            float wave = Mathf.Sin((normalizedX * waveFrequency + _waveTime) * Mathf.PI * 2) * waveAmplitude;
            float waveHeight = (_textureHeight * 0.7f) + wave;
            
            for (int y = 0; y < _textureHeight; y++)
            {
                float normalizedY = (float)y;
                Color pixelColor;
                
                if (normalizedY <= waveHeight)
                {
                    // 물 부분 - 불투명
                    pixelColor = Color.white;
                }
                else
                {
                    // 공기 부분 - 투명
                    pixelColor = Color.clear;
                }
                
                // 부드러운 안티앨리어싱 처리
                float distance = normalizedY - waveHeight;
                if (distance > -2.0f && distance < 2.0f)
                {
                    float alpha = Mathf.SmoothStep(1.0f, 0.0f, (distance + 2.0f) / 4.0f);
                    pixelColor.a = alpha;
                }
                
                pixels[y * waveResolution + x] = pixelColor;
            }
        }
        
        _waveTexture.SetPixels(pixels);
        _waveTexture.Apply();
    }
    
    /// <summary>
    /// FillAmount에 따라 물결 위치를 업데이트합니다
    /// </summary>
    private void UpdateWavePosition()
    {
        if (_waveRect == null || _waterRect == null)
        {
            return;
        }
        
        float fillAmount = waterImage.fillAmount;
        
        // FillAmount에 따른 물 높이 계산
        float imageHeight = _waterRect.rect.height;
        float waterTopY = imageHeight * fillAmount;
        
        // 물결 이미지를 물 표면 바로 위에 위치시키기
        Vector2 wavePosition = _waterRect.anchoredPosition;
        // 물 이미지의 하단에서 시작해서 물 높이만큼 올라간 위치
        float bottomY = _waterRect.anchoredPosition.y - imageHeight * 0.5f;
        wavePosition.y = bottomY + waterTopY;
        _waveRect.anchoredPosition = wavePosition;
        
        // FillAmount가 거의 0이거나 1일 때는 물결 숨기기
        // bool showWave = fillAmount > 0.02f && fillAmount < 0.98f;
        // _waveTopImage.gameObject.SetActive(showWave);
    }
    
    #endregion
    
    #region Public Methods
    
    /// <summary>
    /// 물결 파라미터를 설정합니다
    /// </summary>
    /// <param name="amplitude">물결 진폭 (픽셀)</param>
    /// <param name="frequency">물결 주파수</param>
    /// <param name="speed">물결 속도</param>
    public void SetWaveParameters(float amplitude, float frequency, float speed)
    {
        waveAmplitude = amplitude;
        waveFrequency = frequency;
        waveSpeed = speed;
    }
    
    /// <summary>
    /// 물결 해상도를 변경합니다
    /// </summary>
    /// <param name="resolution">물결 해상도</param>
    public void SetWaveResolution(int resolution)
    {
        waveResolution = Mathf.Max(256, resolution);
        
        if (_waveTexture != null)
        {
            DestroyImmediate(_waveTexture);
            CreateWaveTexture();
        }
    }
    
    /// <summary>
    /// 물결 높이를 설정합니다
    /// </summary>
    /// <param name="height">물결 영역 높이 (픽셀)</param>
    public void SetWaveHeight(int height)
    {
        _textureHeight = Mathf.Max(64, height);
        
        if (_waveRect != null)
        {
            _waveRect.sizeDelta = new Vector2(_waveRect.sizeDelta.x, _textureHeight);
        }
        
        if (_waveTexture != null)
        {
            DestroyImmediate(_waveTexture);
            CreateWaveTexture();
        }
    }
    
    #endregion
}