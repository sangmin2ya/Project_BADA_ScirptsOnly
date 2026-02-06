using Mirror;
using UnityEngine;
/// <summary>
/// 싱글턴 부모 클래스
/// [박상민 - 2025.06.04]
/// </summary>
[RequireComponent(typeof(NetworkIdentity))]
public class NetworkSingleton<T> : NetworkBehaviour where T : NetworkBehaviour
{
    private static T _instance;

    public static T Instance
    {
        get
        {
            if (_instance == null)
            {
                return null;
            }
            return _instance;
        }
    }

    protected virtual void Awake()
    {
        // 싱글톤 설정 (옵션)
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this as T;
    }
}