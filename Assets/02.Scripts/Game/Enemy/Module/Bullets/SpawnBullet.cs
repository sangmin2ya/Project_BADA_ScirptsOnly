using UnityEngine;
using Mirror;

public class SpawnBullet : Bullet
{
    [Header("스폰해줄 몬스터 프리팹")]
    [SerializeField] private GameObject _spawnMonsterPrefab;

    protected override void HandleHit(GameObject target)
    {
        base.HandleHit(target);
        // 파괴될때 몬스터 스폰
        if (isServer)
        {
            if (target.CompareTag("Ship"))
            {
                // Ship에 맞았을 때 몬스터 스폰
                GameObject monster = Instantiate(_spawnMonsterPrefab, ShipSpawner.Instance.CurrentShip.GetComponent<ShipController>().
                    GetClosestHolePoint(target.transform.InverseTransformPoint(transform.position)).position, Quaternion.identity);
                NetworkServer.Spawn(monster);
            }
        }
    }
}
