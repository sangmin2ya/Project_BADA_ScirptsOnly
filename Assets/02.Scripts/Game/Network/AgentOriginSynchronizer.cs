using Mirror;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Agent에서 실제 본오브젝트를 생성하고 동기화시켜주는 클래스
/// - 해당 클래스는 Agent에서 동작하는 것으로 상정하였다.
/// [조우석 - 2025.08.16.]
/// </summary>
public class AgentOriginSynchronizer : NetworkBehaviour
{
    [Tooltip("정적인 오브젝트인가. 정적이라면 스폰된 오브젝트의 위치가 지속적으로 동기화되지 않음.")]
    [SerializeField] private bool _isStatic = false;
    [Tooltip("Agent에서 소환할 본체 오브젝트 프리팹")]
    [SerializeField] private GameObject _originPrafab;

    [Tooltip("본체 오브젝트의 Transform")]
    private Transform _originTransform;
    [Tooltip("본체 오브젝트의 Net Id")]
    [SyncVar(hook = nameof(OnOriginNetIdChanged))] private uint _originNetId = 0;
    [Tooltip("본체 스폰이 완료되었는가")]
    [SyncVar] private bool _hasSpawnedOrigin = false;

    private GameObject _originObject;
    private Vector2 _spawnOffset => ShipSpawner.Instance != null ? ShipSpawner.Instance.OriginOffset : Vector2.zero;

    public Transform OriginTransform => _originTransform;

    public override void OnStartServer()
    {
        base.OnStartServer();

        SpawnOrigin();
        //TryRequestSpawnInnerObjects();
    }

    private void FixedUpdate()
    {
        // 정적인 오브젝트가 아니라면 주기적으로 Agent와 Origin의 위치를 동기화
        if (!_isStatic && _hasSpawnedOrigin && _originTransform != null)
        {
            _originTransform.rotation = transform.rotation;
            _originTransform.position = (Vector2)transform.position + _spawnOffset;
        }
    }

    /// <summary>
    /// Agent의 내부 공간 오브젝트 생성 후 Agent와 연결
    /// </summary>
    [Server]
    private void SpawnOrigin()
    {
        Debug.Log($"{gameObject.name}: 내부 공간 생성 중...");
        _originObject = Instantiate(_originPrafab, transform.position + (Vector3)_spawnOffset, transform.rotation);
        NetworkServer.Spawn(_originObject);
        _originNetId = _originObject.GetComponent<NetworkIdentity>().netId;
        _originTransform = _originObject.transform;
        _hasSpawnedOrigin = true;
        Debug.Log($"{gameObject.name}: 내부 공간 생성 완료");
    }

    private void OnOriginNetIdChanged(uint oldNetId, uint newNetId)
    {
        Debug.Log($"{gameObject.name}: Origin NetId 변경됨 (Old: {oldNetId}, New: {newNetId})");
        _originNetId = newNetId;
        _originTransform = GameNetworkManager.GetNetworkComponent<Transform>(_originNetId);
    }

    /// <summary>
    /// 내부에 생성해야하는 오브젝트들이 있다면 스폰을 요청
    /// </summary>
    [Server]
    private void TryRequestSpawnInnerObjects()
    {
        if (_originTransform.TryGetComponent<InnerObjectSpawner>(out var innerSpawner))
        {
            innerSpawner.SpawnInnerObject();
        }
    }
    /// <summary>
    /// Agent가 파괴되었을 때 내부 공간도 파괴
    /// </summary>
    private void OnDestroy()
    {
        if (isServer && _hasSpawnedOrigin && _originTransform != null)
        {
            NetworkServer.Destroy(_originTransform.gameObject);
        }
    }
}
