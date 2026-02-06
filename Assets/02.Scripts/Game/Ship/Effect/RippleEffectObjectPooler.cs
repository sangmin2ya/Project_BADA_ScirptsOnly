using System.Collections.Generic;
using UnityEngine;
public class RippleEffectObjectPooler : MonoBehaviour
{
    public static RippleEffectObjectPooler Instance; // 싱글톤

    public GameObject ripplePrefab;
    public int poolSize = 40;

    private List<GameObject> pooledObjects;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        pooledObjects = new List<GameObject>();
        for (int i = 0; i < poolSize; i++)
        {
            GameObject obj = Instantiate(ripplePrefab, this.transform);
            obj.SetActive(false); // 비활성화 상태로 생성
            pooledObjects.Add(obj);
        }
    }

    public GameObject GetPooledObject()
    {
        // 비활성화된 오브젝트를 찾아서 반환
        foreach (var obj in pooledObjects)
        {
            if (!obj.activeInHierarchy)
            {
                return obj;
            }
        }
        // 만약 풀이 부족하면 새로 생성
        GameObject newObj = Instantiate(ripplePrefab, this.transform);
        pooledObjects.Add(newObj);
        return newObj;
    }
}