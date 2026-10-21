using UnityEngine;
using Mirror;

public class SpawnBullet : Bullet
{
    [Header("스폰해줄 몬스터 프리팹")]
    [SerializeField] private GameObject _spawnMonsterPrefab;

    protected override void HandleHit(GameObject target)
    {
        // base.HandleHit에서 풀에 반납되면 isServer가 false로 초기화되므로 먼저 확인
        bool shouldSpawnMonster = isServer && target.CompareTag("Ship");
        Vector3 hitPosition = transform.position;
        base.HandleHit(target);
        // 파괴될때 몬스터 스폰
        if (shouldSpawnMonster)
        {
            // Ship에 맞았을 때 몬스터 스폰
            GameObject monster = Instantiate(_spawnMonsterPrefab, ShipSpawner.Instance.CurrentShip.GetComponent<ShipController>().
                GetClosestHolePoint(target.transform.InverseTransformPoint(hitPosition)).position, Quaternion.identity);
            NetworkServer.Spawn(monster);
        }
    }
}
