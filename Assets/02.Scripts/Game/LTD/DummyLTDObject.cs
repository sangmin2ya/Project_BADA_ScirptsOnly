using UnityEngine;
using Mirror;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 배 외부 크레인으로 인양 가능한 텅 빈 LTD 오브젝트 - 아이템 타입만 가지고 있음
/// [박상민 - 2025.10.03]
/// </summary>
[RequireComponent(typeof(DisplayObject), typeof(ColliderOutlineRenderer))]
public class DummyLTDObject : NetworkBehaviour
{
    [Header("아이템 데이터")]
    [SerializeField] private List<ItemData> _itemDatas;
    [SerializeField] private int _minItemCount = 1;
    [SerializeField] private int _maxItemCount = 3;
    [Header("움직임 설정")]
    [SerializeField] private float _currentHeight = 0f; // 현재 높이
    [SerializeField] private float _floatFrequency = 1f; // 떠오르는 속도
    [SerializeField] private float _floatingAmplitude = 0.2f; // 떠오르는 높이
    // private 
    private DisplayObject _displayObject;
    // 접근용 프로퍼티

    private void Awake()
    {
        _displayObject = GetComponent<DisplayObject>();
    }

    void Start()
    {
        if (!isServer)
        {
            return;
        }
        StartCoroutine(FloatingEffect());
    }

    protected IEnumerator FloatingEffect()
    {
        //중력으로 인해 현재 높이가 0이 될 때까지 기다림

        //현위치에서 아래로 내려갔다가 다시 원래대로 돌아왔다가 반복
        while (true)
        {
            float newDepth = _currentHeight + (Mathf.Sin(Time.time * _floatFrequency) - 1) * _floatingAmplitude;
            RpcSetObjectDepth(newDepth);
            yield return null;
        }
    }

    /// <summary>
    /// 아이템 갯수만큼 랜덤으로 아이템 데이터 반환
    /// </summary>
    /// <returns></returns>
    public List<ItemData> GetRandomItemDatas()
    {
        int itemCount = Random.Range(_minItemCount, _maxItemCount + 1);
        List<ItemData> selectedItems = new List<ItemData>();
        for (int i = 0; i < itemCount; i++)
        {
            int randomIndex = Random.Range(0, _itemDatas.Count);
            selectedItems.Add(_itemDatas[randomIndex]);
        }
        return selectedItems;
    }

    [ClientRpc]
    private void RpcSetObjectDepth(float newDepth)
    {
        _displayObject.ChangeDepthTo(newDepth);
    }
}