using Mirror;
using UnityEngine;

/// <summary>
/// 드릴이 박힐 상대 오브젝트, 함선이나 건물에
/// </summary>
public class DockableWall : MonoBehaviour
{
    [Header("도킹 설정")]
    [Tooltip("도킹 성공 시 고정될 위치")]
    [SerializeField]private Transform _drillPivot = null;
    public Transform DrillPivot => _drillPivot;
    [Tooltip("부모 네트워크 오브젝트")]
    [SerializeField] private NetworkIdentity _parentNetworkObject = null;
    public NetworkIdentity ParentNetworkObject => _parentNetworkObject;
    [Tooltip("텔레포트 지점 피벗")]
    [SerializeField] private Transform _teleportPivot = null;
    public Transform TeleportPivot => _teleportPivot;

    [Header("도킹 상태")]
    [Tooltip("박힌 드릴 오브젝트")]
    [SerializeField] private Drill _connectedDrill = null;
}