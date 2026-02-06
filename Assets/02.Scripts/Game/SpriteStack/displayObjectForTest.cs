using System.Collections.Generic;
using System.Linq;
using UnityEngine;
/// <summary>
/// 테스트용 스프라이트 스택
/// [박상민 - 2025.08.14]
/// </summary>
public class displayObjectForTest : MonoBehaviour
{
    [Header("Stack Object Settings")]
    [SerializeField] private string stackName;
    [SerializeField] private Vector3 _rotation;
    [SerializeField] private float _objectHeight = 1; // 오브젝트 높이
    [SerializeField] private float _waterDepth = 0; // 해수면 깊이 (떠있는 오브젝트면 0, 수면 아래면 -값)
    [SerializeField] private bool _hasShadow = false; // 그림자 여부
    [SerializeField] private Material _stackMaterialNormal;
    [SerializeField] private Material _stackMaterialOverlay;

    [Header("플레이어 세팅")]
    [SerializeField] private int _playerSortingOrder = 75;
    // private 
    [SerializeField] private Camera _mainCamera;
    [SerializeField] private List<Sprite> _stackSprites = new List<Sprite>();
    private List<GameObject> _partList = new List<GameObject>();
    private int _offsetMultiflier = 1; // 오프셋 배율
    private bool _initialized = false;

    void LoadStack()
    {
        _stackSprites = Resources.LoadAll<Sprite>($"SpriteStack/{stackName}").ToList();
    }
    void GenerateStack()
    {
        GameObject parts = new GameObject("Parts");
        parts.transform.parent = transform;
        parts.transform.localPosition = Vector3.zero;
        parts.transform.localRotation = Quaternion.Euler(Vector3.zero);
        for (int i = 0; i < _stackSprites.Count; i++)
        {
            GameObject stackPart = new GameObject("part" + i);
            SpriteRenderer sp = stackPart.AddComponent<SpriteRenderer>();
            sp.sprite = _stackSprites[i];
            stackPart.transform.parent = parts.transform;
            stackPart.transform.position = Vector3.zero;
            stackPart.transform.localRotation = Quaternion.Euler(Vector3.zero);

            if (_hasShadow && i == 0)
            {
                Color color = sp.color;
                color.a = 0.7f; // 그림자 투명도 조절
                sp.color = color;
            }
            _partList.Add(stackPart);
        }
    }

    void Start()
    {
        if (TryGetComponent<SpriteRenderer>(out SpriteRenderer sr))
        {
            sr.enabled = false; // 기본 스프라이트 렌더러 비활성화
        }
        LoadStack();
        GenerateStack();
        if (_mainCamera == null)
        {
            _mainCamera = Camera.main;
        }
        draw_stack(); // 초기화 시 스택 그리기
    }

    void Update()
    {
        draw_stack();
    }

    void draw_stack()
    {
        if (_mainCamera == null)
        {
            _mainCamera = Camera.main;
        }
        if (_mainCamera == null)
        {
            return;
        }

        int s = Mathf.FloorToInt(_waterDepth * 50); // 스프라이트의 정렬 순서 (해수면 아래는 음수, 해수면 위는 양수)
        // 오브젝트 높이에 따른 offset조절
        _offsetMultiflier = Mathf.CeilToInt(_objectHeight / _stackSprites.Count * 0.5f / 0.01f);  // 0.01f 단위로 오프셋이 증가하는 단위
                                                                                                  // offset이 더해지면서 정확히 해수면 높이에서 0이 되도록
        int worldOffsetMultiplier = Mathf.CeilToInt(_waterDepth * 0.5f / 0.01f); // 해수면 깊이에 따른 월드 오프셋 조정
        Vector3 worldOffset = _mainCamera.transform.up * (worldOffsetMultiplier * 0.01f);
        foreach (GameObject part in _partList)
        {
            // 월드 좌표로 위치 설정
            part.transform.position = part.transform.parent.position + worldOffset;

            // 카메라의 up 방향으로 offset 누적
            // 카메라와의 거리와 비례해서 증가
            // Vector3 direction = ((Vector2)transform.position - (Vector2)mainCamera.transform.position).normalized;
            // float distance = Vector2.Distance((Vector2)mainCamera.transform.position, (Vector2)transform.position) / 2; // 거리 조정
            // worldOffset += direction * (offset * distance);

            // 카메라 하단 기준 offset설정
            // float distance = Vector2.Distance((Vector2)(mainCamera.transform.position - (mainCamera.transform.up * screenOffset)), (Vector2)transform.position) / 2; // 거리 조정
            // worldOffset += mainCamera.transform.up * (offset * distance);

            // 월드 전체고정
            worldOffset += _mainCamera.transform.up * _offsetMultiflier * 0.01f;

            // 이후에 콜라이더 설정과 레이어 설정은 Start에서만 한번만 실행하도록 뺄것.!!!!!!!!!!!!!!!!!!
            // 높이 0 아래는 무조건 Floor 레이어, 0은 Shadow 레이어, 그 이상은 default 레이어
            if (_initialized)
            {
                return;
            }

            SpriteRenderer sr = part.GetComponent<SpriteRenderer>();
            
            if (s == 0 && _hasShadow)
            {
                sr.sortingLayerName = "Shadow";
            }
            else if (s <= 0)
            {
                sr.sortingLayerName = "Floor";
            }
            else
            {
                sr.sortingLayerName = "Default";
            }
            sr.sortingOrder = s; //레이어 적용
            if (s > 0)
            {
                if (_stackMaterialNormal != null && _stackMaterialOverlay != null)
                {
                    sr.material = s >= _playerSortingOrder ? _stackMaterialOverlay : _stackMaterialNormal; // 플레이어 오버레이 정렬순서 이상이면 오버레이 머티리얼 적용
                }
            }
            s += _offsetMultiflier;
            if (s == 0)
            {
                s += 1; // 0은 해수면이므로 1 증가
            }
        }
        _initialized = true;
    }
}
