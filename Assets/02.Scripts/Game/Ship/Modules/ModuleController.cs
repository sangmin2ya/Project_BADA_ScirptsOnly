using Mirror;
using UnityEngine;
/// <summary>
/// 모듈과 상호작용하는 지점 (모듈 컨트롤러)
/// [박상민 - 2025.06.20]
/// </summary>
[RequireComponent(typeof(Collider2D), typeof(SpriteRenderer))]
public class ModuleController : NetworkBehaviour
{
    [Header("연결된 모듈")]
    [Tooltip("이 지점에 연결된 모듈 오브젝트")]
    [SerializeField] private Module _connectedModule = null;

    [Header("상호작용 설정")]
    [Tooltip("플레이어가 고정되는 위치")]
    [SerializeField] private Transform _interactionPosition = null;

    // 프로퍼티
    public Transform InteractionPosition => _interactionPosition;
    public Module ConnectedModule => _connectedModule;

    [Server]
    public void SetConnectedModule(Module module)
    {
        if (_connectedModule != null)
        {
            Debug.LogWarning("이미 연결된 모듈이 있습니다.");
            return;
        }
        _connectedModule = module;
    }

    /// <summary>
    /// 상호작용이 가능한지 아닌지 보여주는 스프라이트를 토글
    /// </summary>
    [ClientRpc]
    public void ToggleInteraction(bool isActive)
    {
        _interactionPosition.gameObject.SetActive(isActive);
    }
}