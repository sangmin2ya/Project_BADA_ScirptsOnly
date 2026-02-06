using DG.Tweening;
using Mirror;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 드릴 포인트 - 적함의 DockingPoint와 충돌하여 도킹하는 드릴 끝부분
/// [박상민 - 2025.07.02]
/// </summary>
public class DrillPoint : MonoBehaviour
{
    [Header("드릴 포인트 설정")]
    [Tooltip("도킹 성공 시 고정될 거리")]
    [SerializeField] private float _dockingDistance = 0.1f;
    [SerializeField] private Drill _parentDrill = null;

    private Collider2D _collider2D; // 드릴 포인트의 콜라이더

    #region 유니티 생명주기
    private void Awake()
    {
        _parentDrill = GetComponentInParent<Drill>();
        _collider2D = GetComponent<Collider2D>();
        ToggleCollider(false); // 초기에는 콜라이더 비활성화

        if (_parentDrill == null)
        {
            Debug.LogError("DrillPoint: 부모 Drill 컴포넌트를 찾을 수 없습니다.");
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (_parentDrill == null || _parentDrill.IsStucked)
        {
            return;
        }

        // 도킹 포인트 태그와 일치하는지 확인
        if (other.TryGetComponent<DockableWall>(out DockableWall dockableObject))
        {
            Debug.Log($"DrillPoint: Docking to {dockableObject.name} at distance {_dockingDistance}");
            // 도킹 시도
            _parentDrill.DockToTarget(dockableObject); // 부모 드릴에 도킹 알림
        }
    }
    #endregion

    /// <summary>
    /// 드릴 발사할때만 콜라이더를 활성화하고, 도킹시에는 비활성화
    /// </summary>
    public void ToggleCollider(bool enable)
    {
        if (_collider2D != null)
        {
            _collider2D.enabled = enable;
        }
        else
        {
            Debug.LogError("DrillPoint: Collider2D 컴포넌트를 찾을 수 없습니다.");
        }
    }
}