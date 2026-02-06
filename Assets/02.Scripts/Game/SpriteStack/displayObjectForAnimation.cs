//using System.Collections.Generic;
//using System.Linq;
//using UnityEditor.Animations;
using UnityEngine;
/// <summary>
/// 스프라이트를 스택으로 쌓아주는 컴포넌트
/// [박상민 - 2025.08.01]
/// </summary>
public class displayObjectForAnimation : MonoBehaviour
{
    //[Header("Stack Object Settings")]
    //[SerializeField] private Sprite _sprite;
    //[SerializeField] private Vector3 _rotation;
    //[SerializeField] private float _objectHeight = 1; // 오브젝트 높이
    //[SerializeField] private float _waterDepth = 0; // 해수면 깊이 (떠있는 오브젝트면 0, 수면 아래면 -값)

    //[Header("유닛 위치")]
    //[Tooltip("오브젝트가 agent에 위치하는지, 함선에 위치하는지 여부")]
    //[SerializeField] private bool _isOnOrigin = false;

    //[SerializeField] private Material _unitMaterial;
    //[SerializeField] private Material _oceanUnitMaterial;
    //[SerializeField] private AnimatorController _animatorController;

    //[Header("자식 displayObject 리스트")]
    //[SerializeField] private List<DisplayObject> _childDisplayObjects = new List<DisplayObject>();

    //// private 
    //[SerializeField] private Camera _mainCamera;
    //[SerializeField] private GameObject _spriteObject;
    //private Collider2D _currentCollider; // 현재 콜라이더
    //private int _offsetMultiflier = 1; // 오프셋 배율
    //private bool _initOnce = false; // 초기화 여부
    //[SerializeField] private bool _isCloseToShip = false; // 함선과 가까운지 여부

    ////프로퍼티
    //public float ObjectHeight => _objectHeight;
    //public float WaterDepth => _waterDepth;
    //public float TotalHeight => _objectHeight + _waterDepth;

    //void GenerateSprite()
    //{
    //    _spriteObject = new GameObject("ObjectSprite");
    //    SpriteRenderer sp = _spriteObject.AddComponent<SpriteRenderer>();
    //    sp.sprite = _sprite;
    //    _spriteObject.transform.localPosition = Vector3.zero;
    //    _spriteObject.transform.localRotation = Quaternion.Euler(Vector3.zero);
    //    if (_unitMaterial != null)
    //    {
    //        sp.material = _unitMaterial; // 유닛 머티리얼 적용
    //    }
    //    if (_animatorController != null)
    //    {
    //        Animator animator = _spriteObject.AddComponent<Animator>();
    //        animator.runtimeAnimatorController = _animatorController;
    //    }
    //}

    //void Start()
    //{
    //    if (TryGetComponent<SpriteRenderer>(out SpriteRenderer sr))
    //    {
    //        sr.enabled = false; // 기본 스프라이트 렌더러 비활성화
    //    }
    //    GenerateSprite();
    //    if (_mainCamera == null)
    //    {
    //        _mainCamera = _isOnOrigin ? Camera.main : GameObject.Find("AgentCamera")?.GetComponent<Camera>();
    //    }
    //    draw_stack(); // 초기화 시 스택 그리기
    //}

    //void Update()
    //{
    //    if (!_isCloseToShip)
    //    {
    //        return; // 함선과 가까운 얘만 이미지 업데이트
    //    }
    //    draw_stack();
    //}

    //void draw_stack()
    //{
    //    if (_mainCamera == null)
    //    {
    //        _mainCamera = _isOnOrigin ? Camera.main : GameObject.Find("AgentCamera")?.GetComponent<Camera>();
    //    }
    //    if (_mainCamera == null)
    //    {
    //        return;
    //    }
    //    // 화면 밖이면 종료
    //    // if (!initOnce && Vector2.Distance(_mainCamera.transform.position, transform.position) > 100)
    //    // {
    //    //     return;
    //    // }

    //    int s = Mathf.FloorToInt(_waterDepth * 50); // 스프라이트의 정렬 순서 (해수면 아래는 음수, 해수면 위는 양수)
    //                                                // 오브젝트 높이에 따른 offset조절
    //                                                // offset이 더해지면서 정확히 해수면 높이에서 0이 되도록
    //    int worldOffsetMultiplier = Mathf.CeilToInt(_waterDepth * 0.5f / 0.01f); // 해수면 깊이에 따른 월드 오프셋 조정
    //    Vector3 worldOffset = _mainCamera.transform.up * (worldOffsetMultiplier * 0.01f);

    //    _spriteObject.transform.position = _spriteObject.transform.parent.position + worldOffset;

    //    // 카메라의 up 방향으로 offset 누적
    //    // 카메라와의 거리와 비례해서 증가
    //    // Vector3 direction = ((Vector2)transform.position - (Vector2)mainCamera.transform.position).normalized;
    //    // float distance = Vector2.Distance((Vector2)mainCamera.transform.position, (Vector2)transform.position) / 2; // 거리 조정
    //    // worldOffset += direction * (offset * distance);

    //    // 카메라 하단 기준 offset설정
    //    // float distance = Vector2.Distance((Vector2)(mainCamera.transform.position - (mainCamera.transform.up * screenOffset)), (Vector2)transform.position) / 2; // 거리 조정
    //    // worldOffset += mainCamera.transform.up * (offset * distance);

    //    // 월드 전체고정
    //    worldOffset += _mainCamera.transform.up * _offsetMultiflier * 0.01f;

    //    _spriteObject.GetComponent<SpriteRenderer>().sortingOrder = s;
    //    _initOnce = true; // 초기화 완료
    //}

    ///// <summary>
    ///// 함선과 가까운지 여부를 함선에서 설정해줌
    ///// </summary>
    //public void SetCloseToShip(bool value)
    //{
    //    _isCloseToShip = value;
    //    //콜라이더도 꺼줌
    //    if (_currentCollider != null)
    //    {
    //        _currentCollider.enabled = value; // 함선과 가까울 때만 콜라이더 활성화
    //    }
    //    foreach (DisplayObject child in _childDisplayObjects)
    //    {
    //        child.SetCloseToShip(value); // 자식 오브젝트들도 동일하게 설정
    //    }
    //}

    //public void ChangeDepth(float value)
    //{
    //    _waterDepth += value; // 깊이 변경
    //    foreach (DisplayObject child in _childDisplayObjects)
    //    {
    //        child.ChangeDepth(value); // 자식 오브젝트들도 깊이 변경
    //    }
    //}

    //public void ChangeDepthTo(float value)
    //{
    //    float delta = value - _waterDepth; // 변경할 깊이 계산
    //    ChangeDepth(delta); // 깊이 변경
    //}

    //public enum ColliderLayerType
    //{
    //    None,
    //    Physics,
    //    PhysicsIgnoreEachOther,
    //}
}
