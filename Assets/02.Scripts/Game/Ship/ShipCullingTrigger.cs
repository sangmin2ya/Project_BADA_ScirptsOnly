using UnityEngine;
using System.Collections.Generic;
/// <summary>
/// 스트라이프 스태킹 최적화를 위한 트리거 컴포넌트
/// 안에 들어온 오브젝트들만 offset적용
/// [박상민 - 2025.08.05]
/// </summary>
public class ShipCullingTrigger : MonoBehaviour
{
    [Header("최적화 설정")]
    [Tooltip("화면 크기")]
    [SerializeField] private Vector2 _screenSize = new Vector2(1920, 1080);
    [SerializeField] private LayerMask _cullingLayerMask; // 최적화 적용할 레이어

    private void Start()
    {
        // 초기화 시 화면 크기에 맞춰서 Collider를 생성
        CreateColliderWithScreenSize();
        EnableEffectOfMine();
    }
    /// <summary>
    /// 화면 크기에 맞춰서 Collider를 생성하는 메서드
    /// </summary>
    private void CreateColliderWithScreenSize()
    {
        // 화면 크기에 맞춰서 Collider를 생성하는 로직
        BoxCollider2D collider = gameObject.AddComponent<BoxCollider2D>();
        collider.size = _screenSize * 0.1f; // Collider 크기를 화면 크기에 맞게 조정
        collider.isTrigger = true;
        collider.offset = Vector2.zero;
        collider.includeLayers = _cullingLayerMask; // 최적화 적용할 레이어 설정
        collider.excludeLayers = ~_cullingLayerMask;// 컬링 마스크 레이어를 제외한 모든 레이어를 제외
    }
    /// <summary>
    /// 자신은 최적화 제외이므로 효과를 활성화하는 메서드
    /// </summary>
    private void EnableEffectOfMine()
    {
        // 자신의 효과를 활성화하는 로직
        transform.parent.GetComponent<DisplayObject>().SetCloseToShip(true);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        Debug.Log("<color=green>시야에 들어옴 : </color>" + other.name);
        // 트리거에 들어온 오브젝트가 displayObject 컴포넌트를 가지고 있다면
        DisplayObject stackObject = other.transform.GetComponentInParent<DisplayObject>();
        if (stackObject != null)
        {
            // 함선과 가까운지 여부를 설정
            stackObject.SetCloseToShip(true);
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!other.enabled)
        {
            return; // Collider가 비활성화된 경우 무시
        }
        // 트리거에서 나간 오브젝트가 displayObject 컴포넌트를 가지고 있다면
        DisplayObject stackObject = other.transform.GetComponentInParent<DisplayObject>();
        if (stackObject != null)
        {
            // 함선과 멀어졌음을 설정
            stackObject.SetCloseToShip(false);
        }
    }
}