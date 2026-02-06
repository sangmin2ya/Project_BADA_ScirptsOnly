using System.Collections.Generic;
using UnityEngine;
/// <summary>
/// 물이 깊어질수록 흐려지도록 만드는 컨트롤러
/// [박상민 - 2025.08.20]
/// </summary>
public class WaterDepthController : MonoBehaviour
{
    [Header("물 깊이 설정")]
    [Tooltip("물이 흐려지기 시작하는 깊이")]
    [SerializeField] private float _startDepth = 30f;
    [Tooltip("물의 최대깊이")]
    [SerializeField] private float _endDepth = 50f;
    [Tooltip("물의 깊이 간격")]
    [SerializeField] private float _waterDepth = 1f; // 물의 깊이 간격
    [Tooltip("깊이 조절용 스프라이트")]
    [SerializeField] private Sprite _deepWaterSprite; // 물의 스프라이트
    [Tooltip("투명도")]
    [SerializeField][Range(0f, 1f)] private float _spriteAlpha = 0.5f; // 스프라이트 투명도
    [Tooltip("수직 미터당 레이어 offset")]
    [SerializeField] private int _layerOffsetPerMeter = 50;
    private List<SpriteRenderer> _spriteRenderers;

    void Start()
    {
        InitWaterLayers();
        UpdateWaterDepth();
    }

    void Update()
    {
        UpdateWaterDepth();
    }
    private void InitWaterLayers()
    {
        _spriteRenderers = new List<SpriteRenderer>();
        int depthCount = Mathf.CeilToInt((_endDepth - _startDepth) / _waterDepth);
        for (int i = 0; depthCount > i; i++)
        {
            GameObject waterLayer = new GameObject($"WaterLayer_{i}");
            waterLayer.transform.parent = transform;
            waterLayer.transform.localPosition = Vector3.zero;
            waterLayer.transform.localRotation = Quaternion.identity;

            SpriteRenderer spriteRenderer = waterLayer.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = _deepWaterSprite;
            spriteRenderer.color = new Color(1f, 1f, 1f, _spriteAlpha);
            spriteRenderer.drawMode = SpriteDrawMode.Sliced;
            spriteRenderer.size = new Vector2(3000f, 3000f); // 스프라이트 크기 조정
            spriteRenderer.sortingLayerName = "Floor";
            spriteRenderer.sortingOrder = -(_layerOffsetPerMeter * Mathf.FloorToInt(_startDepth + (i * _waterDepth))); // 깊이에 따라 정렬 순서 조정

            _spriteRenderers.Add(spriteRenderer);
        }
    }
    private void UpdateWaterDepth()
    {
        for (int i = 0; i < _spriteRenderers.Count; i++)
        {
            _spriteRenderers[i].sortingOrder = _layerOffsetPerMeter * Mathf.FloorToInt(_startDepth + (i * _waterDepth));
        }
    }
}