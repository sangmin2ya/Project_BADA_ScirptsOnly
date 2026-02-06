using Steamworks;
using UnityEngine;

/// <summary>
/// Steamworks API를 초기화하고 콜백을 처리하는 매니저 클래스
/// [박상민 - 2025.06.04]
/// </summary>
public class SteamManager : Singleton<SteamManager>
{
    public static bool Initialized { get; private set; }

    private void Start()
    {
        try
        {
            if (SteamAPI.RestartAppIfNecessary(AppId_t.Invalid))
            {
                Application.Quit();
                return;
            }
        }
        catch (System.DllNotFoundException e)
        {
            Debug.LogError("Steam DLL을 찾을 수 없습니다: " + e.Message);
            return;
        }

        Initialized = SteamAPI.Init();
        if (!Initialized)
        {
            Debug.LogError("Steam 초기화 실패!");
        }
        else
        {
            Debug.Log("Steam 초기화 성공: " + SteamFriends.GetPersonaName());
        }
    }

    private void Update()
    {
        if (Initialized)
        {
            SteamAPI.RunCallbacks();
        }
    }

    private void OnDestroy()
    {
        if (Initialized)
        {
            SteamAPI.Shutdown();
        }
    }
}