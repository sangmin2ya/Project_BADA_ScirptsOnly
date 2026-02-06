using UnityEngine;
using Mirror;
/// <summary>
/// 배 관리 클래스
/// [박상민 - 2025.06.19]
/// </summary>
public class ShipSpawner : NetworkSingleton<ShipSpawner>
{
    [Header("생성할 배")]
    [Tooltip("배의 프리팹")]
    [SerializeField] private GameObject _shipPrefab;

    [Header("생성할 에이전트")]
    [Tooltip("에이전트의 프리팹")]
    [SerializeField] private GameObject _agentPrefab;

    [Header("배 오브젝트")]
    [Tooltip("현재 배 오브젝트")]
    [SyncVar] private uint _currentShipId = 0;

    [Header("에이전트 오브젝트")]
    [Tooltip("현재 에이전트 오브젝트")]
    [SyncVar] private uint _agentId = 0;

    [SyncVar]
    private Vector2 _originOffset;

    public GameObject CurrentShip => _currentShipId != 0 ? GameNetworkManager.GetNetworkComponent<ShipController>(_currentShipId).gameObject : null;
    public GameObject AgentShip => _agentId != 0 ? GameNetworkManager.GetNetworkComponent<AgentController>(_agentId).gameObject : null;
    public Vector2 OriginOffset => _originOffset;

    public override void OnStartServer()
    {
        base.OnStartServer();
        if (isServer)
        {
            _originOffset = BaseCampManager.Instance.OriginSpawnPosition;
            CreateAgent();
            CreateShip();
        }
    }
    /// <summary>
    /// 배를 생성합니다.
    /// </summary>
    [Server]
    public void CreateShip()
    {
        if (_currentShipId != 0)
        {
            Debug.LogWarning("이미 배가 존재합니다.");
            return;
        }
        Debug.Log("배를 생성합니다.");
        GameObject currentShip = Instantiate(_shipPrefab, _originOffset, Quaternion.identity);
        NetworkServer.Spawn(currentShip.gameObject);
        _currentShipId = currentShip.GetComponent<NetworkIdentity>().netId;
        ShipController spawnedShip = GameNetworkManager.GetNetworkComponent<ShipController>(_currentShipId);
        if (spawnedShip != null)
        {
            spawnedShip.InitializeShipOnServer();
        }
        else
        {
            Debug.LogError("배 스폰 실패");
        }
    }
    /// <summary>
    /// 월드와 상호작용하는 에이전트를 생성합니다.
    /// [김도형 - 2025.07.22]
    /// </summary>
    [Server]
    public void CreateAgent()
    {
        if (_agentId != 0)
        {
            Debug.LogWarning("이미 에이전트가 존재합니다.");
            return;
        }
        Debug.Log("에이전트를 생성합니다.");
        GameObject agent = Instantiate(_agentPrefab, BaseCampManager.Instance.SpawnPosition, Quaternion.Euler(0, 0, BaseCampManager.Instance.SpawnRotation));
        NetworkServer.Spawn(agent.gameObject);
        _agentId = agent.GetComponent<NetworkIdentity>().netId;
    }
}