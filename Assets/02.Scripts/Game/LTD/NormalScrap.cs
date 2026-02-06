using Mirror;
using UnityEngine;

/// <summary>
/// 일반 고철 파밍용 오브젝트 - 수리 재료로도 사용 가능
/// [박상민 - 2025.07.23]
/// </summary>
public class NormalScrap : ScrapObject
{
    // private ShipController _shipController;

    // private void Start()
    // {
    //     _shipController = ShipSpawner.Instance.CurrentShip.GetComponent<ShipController>();
    // }
    /// <summary>
    /// 수리후 파괴
    /// </summary>
    // [Command(requiresAuthority = false)]
    // private void CmdDestroyOnRepair()
    // {
    //     NetworkServer.Destroy(gameObject); // 오브젝트 제거
    // }

    // protected override void OnTriggerEnter2D(Collider2D other)
    // {
    //     base.OnTriggerEnter2D(other);
    //     if (other.CompareTag("RepairPoint"))
    //     {
    //         _shipController.Repair(other.gameObject); // 함선 수리 명령
    //         CmdDestroyOnRepair();
    //     }
    // }
}