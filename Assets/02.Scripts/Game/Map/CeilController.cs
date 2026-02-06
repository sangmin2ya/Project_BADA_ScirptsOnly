using System.Collections.Generic;
using Mirror;
using UnityEngine;
/// <summary>
/// 건물 내부를 가리던 천장을 관리하는 스크립트
/// [박상민 - 2025.07.18]
/// </summary>
public class CeilController : NetworkBehaviour
{
    [SerializeField] private List<GameObject> _covers = new List<GameObject>();
    [SerializeField] private List<GameObject> _inside = new List<GameObject>();
    [SerializeField] private Camera _innerMapCamera;
    private string _playerTag = "Player";
    private bool _isFirstEnter = true;

    void Start()
    {
        foreach (GameObject cover in _covers)
        {
            cover.gameObject.SetActive(true); // 시작 시 모든 커버 활성화
        }
        foreach (GameObject inside in _inside)
        {
            inside.SetActive(false); // 시작 시 모든 내부 오브젝트 비활성화
        }
        // 항상 월드 기준 위쪽을 바라보도록 설정
        if (_innerMapCamera != null)
        {
            _innerMapCamera.transform.rotation = Quaternion.Euler(0, 0, 0);
        }
    }
    void OnTriggerEnter2D(Collider2D other)
    {
        if (!isServer)
        {
            return;
        }
        if (other.CompareTag(_playerTag))
        {
            if (_isFirstEnter)
            {
                // 첫 진입에만 적 스폰
                InnerObjectSpawner spawner = GetComponent<InnerObjectSpawner>();
                if (spawner != null)
                {
                    spawner.SpawnInnerObject();
                }
            }
            RpcToggleCeil(other.GetComponent<NetworkIdentity>().connectionToClient, false);
            other.GetComponent<PlayerController>().RpcEnterBuilding(other.GetComponent<NetworkIdentity>()?.connectionToClient);
            _isFirstEnter = false;
        }
    }
    void OnTriggerExit2D(Collider2D other)
    {
        if (!isServer)
        {
            return;
        }
        //레이어 비교
        if (other.CompareTag(_playerTag))
        {
            other.GetComponent<PlayerController>().RpcExitBuilding(other.GetComponent<NetworkIdentity>()?.connectionToClient);
            RpcToggleCeil(other.GetComponent<NetworkIdentity>().connectionToClient, true);
        }
    }

    /// <summary>
    /// 천장과 내부 오브젝트의 활성화 상태를 토글하는 RPC 함수
    /// 서버에서 충돌을 처리하고 클라이언트에 천장을 보이거나 숨기도록 지시
    /// [박상민 - 2025.09.14]
    /// </summary>
    [TargetRpc]
    private void RpcToggleCeil(NetworkConnection target, bool isOn)
    {
        foreach (GameObject cover in _covers)
        {
            cover.SetActive(isOn);
        }
        foreach (GameObject inside in _inside)
        {
            inside.SetActive(!isOn);
        }
    }
}