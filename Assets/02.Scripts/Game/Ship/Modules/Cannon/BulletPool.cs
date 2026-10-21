using System.Collections.Generic;
using Mirror;
using UnityEngine;

/// <summary>
/// 포탄 네트워크 오브젝트 풀
/// 서버는 Get/Despawn으로 꺼내고 반납하고(NetworkServer.UnSpawn),
/// 클라이언트는 NetworkClient.RegisterPrefab의 spawn/unspawn 핸들러로 같은 풀을 사용
/// [박상민 - 2026.10.22]
/// </summary>
public static class BulletPool
{
    private static readonly Dictionary<uint, Stack<GameObject>> _pools = new Dictionary<uint, Stack<GameObject>>();
    private static readonly Dictionary<uint, GameObject> _prefabs = new Dictionary<uint, GameObject>();
    // 풀에 들어있는 오브젝트. 호스트에서 서버/클라이언트 양쪽이 반납해도 한 번만 들어가도록 사용
    private static readonly HashSet<GameObject> _pooled = new HashSet<GameObject>();

    /// <summary>
    /// 클라이언트 시작 시 호출. 스폰 프리팹 중 Bullet을 가진 프리팹의 생성/파괴를 풀 핸들러로 교체
    /// </summary>
    public static void RegisterClientHandlers(IEnumerable<GameObject> spawnPrefabs)
    {
        foreach (GameObject prefab in spawnPrefabs)
        {
            if (prefab == null || prefab.GetComponent<Bullet>() == null)
            {
                continue;
            }
            if (!prefab.TryGetComponent(out NetworkIdentity identity))
            {
                continue;
            }
            _prefabs[identity.assetId] = prefab;
            // NetworkManager가 기본 방식으로 등록한 프리팹은 해제 후 핸들러와 함께 다시 등록해야 함
            NetworkClient.UnregisterPrefab(prefab);
            NetworkClient.RegisterPrefab(prefab, SpawnHandler, UnspawnHandler);
        }
    }

    /// <summary>
    /// 서버에서 포탄을 꺼냄. 풀이 비어있으면 새로 생성. 이후 NetworkServer.Spawn은 호출하는 쪽에서 수행
    /// </summary>
    public static GameObject Get(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        if (!prefab.TryGetComponent(out NetworkIdentity identity))
        {
            return Object.Instantiate(prefab, position, rotation);
        }
        _prefabs[identity.assetId] = prefab;

        GameObject bullet = Take(identity.assetId, position, rotation);
        if (bullet == null)
        {
            return Object.Instantiate(prefab, position, rotation);
        }
        bullet.transform.localScale = prefab.transform.localScale;
        if (bullet.TryGetComponent(out Bullet bulletComponent))
        {
            bulletComponent.ResetForReuse(prefab.GetComponent<Bullet>());
        }
        return bullet;
    }

    /// <summary>
    /// 서버에서 포탄을 반납. 모든 클라이언트에서 unspawn 핸들러가 호출됨
    /// </summary>
    public static void Despawn(GameObject bullet)
    {
        // 이미 반납된 포탄 (같은 프레임에 여러 번 피격 처리되는 경우)
        if (bullet == null || !bullet.activeSelf)
        {
            return;
        }
        NetworkServer.UnSpawn(bullet);
        Return(bullet);
    }

    private static GameObject SpawnHandler(SpawnMessage msg)
    {
        GameObject bullet = Take(msg.assetId, msg.position, msg.rotation);
        if (bullet != null)
        {
            return bullet;
        }
        if (_prefabs.TryGetValue(msg.assetId, out GameObject prefab))
        {
            return Object.Instantiate(prefab, msg.position, msg.rotation);
        }
        Debug.LogError($"BulletPool: 등록되지 않은 assetId {msg.assetId}");
        return null;
    }

    private static void UnspawnHandler(GameObject bullet)
    {
        Return(bullet);
    }

    private static GameObject Take(uint assetId, Vector3 position, Quaternion rotation)
    {
        if (!_pools.TryGetValue(assetId, out Stack<GameObject> pool))
        {
            return null;
        }
        while (pool.Count > 0)
        {
            GameObject bullet = pool.Pop();
            _pooled.Remove(bullet);
            // 씬 전환 등으로 파괴된 오브젝트는 건너뜀
            if (bullet == null)
            {
                continue;
            }
            bullet.transform.SetPositionAndRotation(position, rotation);
            bullet.SetActive(true);
            return bullet;
        }
        return null;
    }

    private static void Return(GameObject bullet)
    {
        if (bullet == null || !_pooled.Add(bullet))
        {
            return;
        }
        bullet.SetActive(false);
        uint assetId = bullet.GetComponent<NetworkIdentity>().assetId;
        if (!_pools.TryGetValue(assetId, out Stack<GameObject> pool))
        {
            pool = new Stack<GameObject>();
            _pools[assetId] = pool;
        }
        pool.Push(bullet);
    }
}
