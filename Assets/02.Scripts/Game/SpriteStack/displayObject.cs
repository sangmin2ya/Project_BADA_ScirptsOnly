using System.Collections.Generic;
using System.Linq;
using UnityEngine;
/// <summary>
/// 스프라이트를 스택으로 쌓아주는 컴포넌트
/// [박상민 - 2025.08.01]
/// </summary>
public class DisplayObject : MonoBehaviour
{
    [Header("Stack Object Settings")]
    [SerializeField] private stackobject _stackObject;
    [SerializeField] private Vector3 _rotation;
    [SerializeField] private float _objectHeight = 1; // 오브젝트 높이
    [SerializeField] private float _waterDepth = 0; // 해수면 깊이 (떠있는 오브젝트면 0, 수면 아래면 -값)

    [Header("유닛 위치")]
    [Tooltip("오브젝트가 agent에 위치하는지, 함선에 위치하는지 여부")]
    [SerializeField] private bool _isOnOrigin = false;

    [SerializeField] private Material _unitMaterial;
    [SerializeField] private Material _oceanUnitMaterial;

    [Header("해수면 외각 효과")]
    [SerializeField] private ColliderOutlineRenderer _outlineRenderer;
    [Header("콜라이더 처리")]
    [SerializeField] private bool _areThisObjectHasCollider = true;
    [Tooltip("생성될 콜라이더의 레이어")]
    [SerializeField] private ColliderLayerType _layerType = ColliderLayerType.Physics;

    [Header("자식 displayObject 리스트")]
    [SerializeField] private List<DisplayObject> _childDisplayObjects = new List<DisplayObject>();

    // private 
    [SerializeField] private Camera _mainCamera;
    [SerializeField] private List<Sprite> _stackSprites = new List<Sprite>();
    private GameObject _parts;
    private List<GameObject> _partList = new List<GameObject>();
    private Collider2D _currentCollider; // 현재 콜라이더
    private int _offsetMultiflier = 1; // 오프셋 배율
    private bool _initOnce = false; // 초기화 여부
    [SerializeField] private bool _isCloseToShip = false; // 함선과 가까운지 여부

    //프로퍼티
    public float ObjectHeight => _objectHeight;
    public float WaterDepth => _waterDepth;
    public float TotalHeight => _objectHeight + _waterDepth;
    public GameObject Parts => _parts;

    void LoadStack()
    {
        if (_stackObject == null)
        {
            return;
        }
        _stackSprites = Resources.LoadAll<Sprite>($"SpriteStack/{_stackObject.stackName}").ToList();
    }
    void GenerateStack()
    {
        GameObject parts = new GameObject("Parts");
        _parts = parts;
        parts.transform.parent = transform;
        parts.transform.localPosition = Vector3.zero;
        parts.transform.localRotation = Quaternion.Euler(Vector3.zero);
        for (int i = 0; i < _stackSprites.Count; i++)
        {
            GameObject stackPart = new GameObject("part" + i);
            SpriteRenderer sp = stackPart.AddComponent<SpriteRenderer>();
            sp.sprite = _stackSprites[i];
            stackPart.transform.parent = parts.transform;
            stackPart.transform.localPosition = Vector3.zero;
            stackPart.transform.localRotation = Quaternion.Euler(Vector3.zero);
            if (_unitMaterial != null)
            {
                sp.material = _unitMaterial; // 유닛 머티리얼 적용
            }
            // 물위에 떠있어서 그림자가 보여야하면 첫번째 스프라이트 투명도 조절
            if (_waterDepth == 0 && i == 0)
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
        if (_stackObject == null)
        {
            return;
        }
        if (TryGetComponent<SpriteRenderer>(out SpriteRenderer sr))
        {
            sr.enabled = false; // 기본 스프라이트 렌더러 비활성화
        }
        LoadStack();
        GenerateStack();
        _outlineRenderer = GetComponent<ColliderOutlineRenderer>();
        if (_mainCamera == null)
        {
            _mainCamera = _isOnOrigin ? Camera.main : GameObject.Find("AgentCamera")?.GetComponent<Camera>();
        }
        draw_stack(); // 초기화 시 스택 그리기
    }

    void Update()
    {
        if (_stackObject == null)
        {
            return;
        }
        if (!_isCloseToShip)
        {
            return; // 함선과 가까운 얘만 이미지 업데이트
        }
        draw_stack();
    }

    void draw_stack()
    {
        if (_mainCamera == null)
        {
            _mainCamera = _isOnOrigin ? Camera.main : GameObject.Find("AgentCamera")?.GetComponent<Camera>();
        }
        if (_mainCamera == null)
        {
            return;
        }
        // 화면 밖이면 종료
        // if (!initOnce && Vector2.Distance(_mainCamera.transform.position, transform.position) > 100)
        // {
        //     return;
        // }

        int s = Mathf.FloorToInt(_waterDepth * 50); // 스프라이트의 정렬 순서 (해수면 아래는 음수, 해수면 위는 양수)
        // 오브젝트 높이에 따른 offset조절
        _offsetMultiflier = Mathf.CeilToInt(_objectHeight / _stackSprites.Count * 0.5f / 0.01f);  // 0.01 단위로 오프셋이 증가하는 단위
                                                                                                  // offset이 더해지면서 정확히 해수면 높이에서 0이 되도록
        int worldOffsetMultiplier = Mathf.CeilToInt(_waterDepth * 0.5f / 0.01f); // 해수면 깊이에 따른 월드 오프셋 조정
        Vector3 worldOffset = _mainCamera.transform.up * (worldOffsetMultiplier * 0.01f);

        bool isUnderwater = true; // 현재 단면도가 수면 아래에 있는지 여부

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
            if (s < 0)
            {
                part.GetComponent<SpriteRenderer>().sortingLayerName = "Floor";
            }
            else if (s == 0)
            {
                part.GetComponent<SpriteRenderer>().sortingLayerName = "Shadow";
            }
            else
            {
                part.GetComponent<SpriteRenderer>().sortingLayerName = "Default";
                if (!_isOnOrigin)//바다에 있으면 배에 가려지는 셰이더 사용
                {
                    if (_oceanUnitMaterial != null)
                        part.GetComponent<SpriteRenderer>().material = _oceanUnitMaterial;
                }
            }

            part.GetComponent<SpriteRenderer>().sortingOrder = s;
            if (isUnderwater && s > 0 && _waterDepth < 0 && _areThisObjectHasCollider)
            {
                // 해수면 아래에 있다가 해수면에 도달하면 콜라이더 추가
                isUnderwater = false; // 해수면에 도달했으므로 false로 변경
                if (part.TryGetComponent<PolygonCollider2D>(out PolygonCollider2D polygonCollider2D))
                {
                    polygonCollider2D.enabled = true; // 콜라이더 비활성화
                    _currentCollider = polygonCollider2D;
                    // 안전한 아웃라인 업데이트
                    if (_outlineRenderer != null)
                    {
                        _outlineRenderer.ChangeCollider(polygonCollider2D);
                    }
                }
                else
                {
                    // 콜라이더가 없으면 새로 추가
                    PolygonCollider2D newCollider = part.AddComponent<PolygonCollider2D>();
                    _currentCollider = newCollider;
                    if (_layerType != ColliderLayerType.None)
                    {
                        _currentCollider.gameObject.layer = LayerMask.NameToLayer(_layerType.ToString());
                    }
                    if (_outlineRenderer != null)
                    {
                        _outlineRenderer.ChangeCollider(newCollider);
                    }
                }
            }
            else
            {
                //해수면이 아니라면 폴리곤 콜라이더 제거
                if (part.TryGetComponent<PolygonCollider2D>(out PolygonCollider2D collider))
                {
                    collider.enabled = false; // 콜라이더 비활성화
                    if (_outlineRenderer != null)
                    {
                        _outlineRenderer.ChangeCollider(null); // 아웃라인 렌더러도 콜라이더 제거
                    }
                }
            }

            s += _offsetMultiflier;
            if (s == 0)
            {
                s += 1; // 0은 해수면이므로 1 증가
            }
        }
        _initOnce = true; // 초기화 완료
    }

    /// <summary>
    /// 함선과 가까운지 여부를 함선에서 설정해줌
    /// </summary>
    public void SetCloseToShip(bool value)
    {
        _isCloseToShip = value;
        //콜라이더도 꺼줌
        if (_currentCollider != null)
        {
            _currentCollider.enabled = value; // 함선과 가까울 때만 콜라이더 활성화
        }
        if (_outlineRenderer != null)
        {
            _outlineRenderer.enabled = value; // 아웃라인 렌더러도 활성화
        }
        foreach (DisplayObject child in _childDisplayObjects)
        {
            child.SetCloseToShip(value); // 자식 오브젝트들도 동일하게 설정
        }
    }

    public void ChangeDepth(float value)
    {
        _waterDepth += value; // 깊이 변경
        foreach (DisplayObject child in _childDisplayObjects)
        {
            child.ChangeDepth(value); // 자식 오브젝트들도 깊이 변경
        }
    }

    public void ChangeDepthTo(float value)
    {
        float delta = value - _waterDepth; // 변경할 깊이 계산
        ChangeDepth(delta); // 깊이 변경
    }

    public enum ColliderLayerType
    {
        None,
        Physics,
        PhysicsIgnoreEachOther,
    }
}
