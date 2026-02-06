using UnityEngine;
using Mirror;

public class DummyItemSpawner : NetworkBehaviour
{
    #region Serialized Fields
    [SerializeField] private GameObject[] dummyItemPrefabs;
    [SerializeField] private float spawnDistance = 100f;
    [SerializeField] private float spawnInterval = 10f;
    [SerializeField] private float despawnDistance = 150f;
    [SerializeField] private int maxSpawnCount = 20;
    #endregion

    #region Private Fields
    private Transform _shipTransform;
    private float _nextSpawnTime;
    private System.Collections.Generic.List<GameObject> _spawnedItems;
    #endregion

    #region Unity Methods
    private void Start()
    {
        if (!isServer)
        {
            return;
        }

        _spawnedItems = new System.Collections.Generic.List<GameObject>();
        _nextSpawnTime = Time.time + spawnInterval;

        if (FindShipTransform() == false)
        {
            Debug.LogWarning("DummyItemSpawner: 함선을 찾을 수 없습니다.");
        }
    }

    private void Update()
    {
        if (!isServer)
        {
            return;
        }

        if (_shipTransform == null)
        {
            if (FindShipTransform() == false)
            {
                return;
            }
        }

        if (Time.time >= _nextSpawnTime)
        {
            SpawnDummyItem();
            _nextSpawnTime = Time.time + spawnInterval;
        }

        CheckAndDespawnDistantItems();
    }
    #endregion

    #region Spawn Methods
    /// <summary>
    /// 플레이어 주변에 더미 아이템을 스폰합니다.
    /// </summary>
    private void SpawnDummyItem()
    {
        if (dummyItemPrefabs == null || dummyItemPrefabs.Length == 0)
        {
            Debug.LogError("DummyItemSpawner: 스폰할 아이템 프리팹이 설정되지 않았습니다.");
            return;
        }

        if (_spawnedItems.Count >= maxSpawnCount)
        {
            Debug.Log("DummyItemSpawner: 최대 스폰 개수에 도달했습니다.");
            return;
        }

        Vector3 spawnPosition = GetRandomSpawnPosition();

        if (spawnPosition == Vector3.zero)
        {
            Debug.LogWarning("DummyItemSpawner: 유효한 스폰 위치를 찾지 못했습니다.");
            return;
        }

        int randomIndex = Random.Range(0, dummyItemPrefabs.Length);
        GameObject selectedPrefab = dummyItemPrefabs[randomIndex];

        if (selectedPrefab == null)
        {
            Debug.LogError("DummyItemSpawner: 선택된 프리팹이 null입니다.");
            return;
        }

        GameObject spawnedItem = Instantiate(selectedPrefab, spawnPosition, Quaternion.identity);
        NetworkServer.Spawn(spawnedItem);
        _spawnedItems.Add(spawnedItem);
    }

    /// <summary>
    /// 플레이어 주변의 랜덤한 스폰 위치를 반환합니다.
    /// </summary>
    private Vector3 GetRandomSpawnPosition()
    {
        if (_shipTransform == null)
        {
            Debug.LogError("DummyItemSpawner: 플레이어 Transform이 null입니다.");
            return Vector3.zero;
        }

        Vector2 randomCircle = Random.insideUnitCircle.normalized * spawnDistance;
        Vector2 spawnPosition = (Vector2)_shipTransform.position + new Vector2(randomCircle.x, randomCircle.y);

        return spawnPosition;
    }
    #endregion

    #region Despawn Methods
    /// <summary>
    /// 플레이어로부터 멀리 떨어진 아이템을 제거합니다.
    /// </summary>
    private void CheckAndDespawnDistantItems()
    {
        if (_shipTransform == null)
        {
            return;
        }

        for (int i = _spawnedItems.Count - 1; i >= 0; i--)
        {
            if (_spawnedItems[i] == null)
            {
                _spawnedItems.RemoveAt(i);
                continue;
            }

            float distance = Vector3.Distance(_shipTransform.position, _spawnedItems[i].transform.position);

            if (distance > despawnDistance)
            {
                NetworkServer.Destroy(_spawnedItems[i]);
                _spawnedItems.RemoveAt(i);
            }
        }
    }
    #endregion

    #region Helper Methods
    /// <summary>
    /// 플레이어의 Transform을 찾습니다.
    /// </summary>
    private bool FindShipTransform()
    {
        GameObject ship = ShipSpawner.Instance.AgentShip;

        if (ship != null)
        {
            _shipTransform = ship.transform;
            return true;
        }

        return false;
    }
    #endregion
}