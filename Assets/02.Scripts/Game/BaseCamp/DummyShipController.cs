using UnityEngine;
using Mirror;
using System.Collections.Generic;
/// <summary>
/// 베이스캠프에 위치하는 더미 배 컨트롤러. - 내부에 실은 아이템을 저장하고 전달함
/// [박상민 - 2025.11.18]
/// </summary>
public class DummyShipController : NetworkBehaviour
{
    private List<LTDObject> _storedItems = new List<LTDObject>();

    public override void OnStartServer()
    {
        base.OnStartServer();
        // 시작할때 배 내부에 아이템들 스폰
        var storedItemsData = BaseCampManager.Instance.GetStoredItemsAndReset();
        foreach (var itemData in storedItemsData)
        {
            Vector2 spawnPos = transform.TransformPoint(itemData.SpawnLocalPosition);
            float rotationZ = transform.eulerAngles.z + itemData.SpawnLocalRotation;
            GameObject ltdObject = Instantiate(ItemManager.Instance.GetItemPrefab(itemData.Item), spawnPos, Quaternion.Euler(0f, 0f, rotationZ));
            NetworkServer.Spawn(ltdObject);
        }
    }

    public void StartGameSession(OceanType oceanType)
    {
        if (!isServer)
        {
            return;
        }
        BaseCampManager.Instance.GetStoredItemsAndReset(); //혹시 모르니까 초기화`
        // 배에 보관된 아이템의 데이터를 BaseCampManager에 저장
        foreach (var item in _storedItems)
        {
            if (item == null)
            {
                continue;
            }
            ItemData itemData = new ItemData
            {
                itemType = item.LtdobjectType,
                scrapType = item is ScrapObject scrap ? scrap.ScrapType : default
            };
            Vector2 spawnPos = transform.InverseTransformPoint(item.transform.position);
            float rotationZ = item.transform.eulerAngles.z - transform.eulerAngles.z;
            BaseCampManager.Instance.StoreItemInShip(itemData, spawnPos, rotationZ);
        }
        // 씬전환
        BaseCampManager.Instance.SetOceanType(oceanType);
        GameNetworkManager.singleton.StartSession();
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!isServer)
        {
            return;
        }

        if (other.TryGetComponent<LTDObject>(out var ltdObject))
        {
            // 플레이어나 적일 경우 리턴
            if (ltdObject.LtdobjectType == LTDObjectType.플레이어 || ltdObject.LtdobjectType == LTDObjectType.몬스터)
            {
                return;
            }
            // 배에 아이템 보관
            _storedItems.Add(ltdObject);
        }
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (!isServer)
        {
            return;
        }

        if (other.TryGetComponent<LTDObject>(out var ltdObject))
        {
            // 배에서 아이템 제거
            _storedItems.Remove(ltdObject);
        }
    }
}