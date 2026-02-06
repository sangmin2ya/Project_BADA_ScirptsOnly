using UnityEngine;
using Mirror;
/// <summary>
/// 배 외부 크레인 모듈 충돌 감지용 그물
/// [박상민 - 2025.10.03]
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class CraneNet : NetworkBehaviour
{
    private Crane _crane;

    void Awake()
    {
        _crane = GetComponentInParent<Crane>();
        GetComponent<Collider2D>().enabled = false; //초기에는 비활성화
    }

    void OnTriggerEnter2D(Collider2D collision)
    {
        if (!isServer) return;

        DummyLTDObject dummyLTD = collision.GetComponent<DummyLTDObject>();
        if (dummyLTD != null)
        {
            _crane.CaptureItem(dummyLTD);
        }
    }
}