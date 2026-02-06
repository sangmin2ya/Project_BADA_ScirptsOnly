using System.Collections.Generic;
using UnityEngine;
using Steamworks;
using System;

/// <summary>
/// 로비 데이터와 참가자 정보를 관리하는 클래스
/// [박상민 - 2025.06.10]
/// </summary>
public class LobbyDataManager : MonoBehaviour
{
    // 게임 이름 정적 변수
    public static string GameName = "SpacePirates";
    // 로비 코드 길이와 문자 설정
    private int _lobbyCodeLength = 5; // 로비 코드의 길이
    private string _codeCharacters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789"; // 로비 코드에 사용할 문자 집합

    // 로비 메타데이터 키 상수
    public const string GameIdKey = "GameID";
    public const string HostAddressKey = "HostAddress";
    public const string HostNameKey = "HostName";
    public const string LobbyCodeKey = "LobbyCode";
    public const string GameVersionKey = "GameVersion";
    public const string MaxPlayersKey = "MaxPlayers";
    public const string GameStatusKey = "GameStatus";

    // 플레이어 메타데이터 키 상수
    public const string PlayerReadyKey = "Ready";
    private const string PlayerLevelKey = "Level";
    private const string PlayerTeamKey = "Team";

    // 현재 로비 정보
    private CSteamID _currentLobbyId;
    public CSteamID CurrentLobbyId => _currentLobbyId;
    private string _currentLobbyCode;
    public string CurrentLobbyCode => _currentLobbyCode;
    private string _currentHostName;
    public string CurrentHostName => _currentHostName;
    private Dictionary<CSteamID, PlayerLobbyData> _playerDataCache;
    public int PlayerCount => _playerDataCache.Count;
    public bool HasLobby => _currentLobbyId.IsValid();


    /// <summary>
    /// 플레이어 로비 데이터 구조체
    /// </summary>
    [Serializable]
    public struct PlayerLobbyData
    {
        public CSteamID steamId;
        public string playerName;
        public bool isReady;
        public int level;
        public int team;
        public bool isHost;

        public PlayerLobbyData(CSteamID id)
        {
            steamId = id;
            playerName = SteamFriends.GetFriendPersonaName(id);
            isReady = false;
            level = 1;
            team = 0;
            isHost = false;
        }
    }

    void Awake()
    {
        _playerDataCache = new Dictionary<CSteamID, PlayerLobbyData>();
    }

    /// <summary>
    /// 랜덤 로비 코드 생성
    /// </summary>
    /// <returns>생성된 로비 코드</returns>
    public string GenerateLobbyCode()
    {
        System.Random random = new System.Random();
        char[] code = new char[_lobbyCodeLength];

        for (int i = 0; i < _lobbyCodeLength; i++)
        {
            code[i] = _codeCharacters[random.Next(_codeCharacters.Length)];
        }

        return new string(code);
    }

    /// <summary>
    /// 로비 메타데이터 설정
    /// </summary>
    /// <param name="lobbyId">로비 ID</param>
    /// <param name="lobbyCode">로비 코드</param>
    /// <param name="hostName">호스트 이름</param>
    /// <param name="hostAddress">호스트 주소</param>
    /// <param name="maxPlayers">최대 플레이어 수</param>
    public void SetLobbyMetadata(CSteamID lobbyId, string lobbyCode, string hostName, string hostAddress, int maxPlayers)
    {
        _currentLobbyId = lobbyId;
        _currentLobbyCode = lobbyCode;
        _currentHostName = hostName;

        SteamMatchmaking.SetLobbyData(lobbyId, GameIdKey, GameName);
        SteamMatchmaking.SetLobbyData(lobbyId, LobbyCodeKey, lobbyCode);
        SteamMatchmaking.SetLobbyData(lobbyId, HostAddressKey, hostAddress);
        SteamMatchmaking.SetLobbyData(lobbyId, HostNameKey, hostName);
        SteamMatchmaking.SetLobbyData(lobbyId, MaxPlayersKey, maxPlayers.ToString());
        SteamMatchmaking.SetLobbyData(lobbyId, GameVersionKey, Application.version);
        SteamMatchmaking.SetLobbyData(lobbyId, GameStatusKey, GameStateType.대기.ToString());

        Debug.Log($"로비 메타데이터 설정 완료: {lobbyCode}");
    }

    /// <summary>
    /// 참가자가 로비에 참여할 때 로비 데이터를 설정
    /// </summary>
    public void SetLobbyData(CSteamID lobbyId)
    {
        _currentLobbyId = lobbyId;
        _currentLobbyCode = SteamMatchmaking.GetLobbyData(lobbyId, LobbyCodeKey);
        _currentHostName = SteamMatchmaking.GetLobbyData(lobbyId, HostNameKey);
    }

    /// <summary>
    /// 로비 데이터 가져오기
    /// </summary>
    /// <param name="lobbyId">로비 ID</param>
    /// <param name="key">메타데이터 키</param>
    public string GetLobbyData(CSteamID lobbyId, string key)
    {
        return SteamMatchmaking.GetLobbyData(lobbyId, key);
    }
    /// <summary>
    /// 게임 진행 상태 업데이트
    /// </summary>
    public void SetLobbyMetadata(CSteamID lobbyId, GameStateType gameState)
    {
        if (!lobbyId.IsValid())
        {
            return;
        }
        // 게임 상태 업데이트
        SteamMatchmaking.SetLobbyData(lobbyId, GameStatusKey, gameState.ToString());
    }
    /// <summary>
    /// 특정 키로 로비 검색
    /// </summary>
    /// <param name="key">검색할 키</param>
    /// <param name="value">검색할 값</param>
    public void AddLobbyListFilter(string key, string value)
    {
        SteamMatchmaking.AddRequestLobbyListStringFilter(key, value, ELobbyComparison.k_ELobbyComparisonEqual);
    }

    /// <summary>
    /// 플레이어 준비 상태 설정
    /// </summary>
    /// <param name="lobbyId">로비 ID</param>
    /// <param name="playerId">플레이어 ID</param>
    /// <param name="ready">준비 상태</param>
    public void SetPlayerReady(CSteamID lobbyId, CSteamID playerId, bool ready)
    {
        string readyValue = ready ? "1" : "0";
        SteamMatchmaking.SetLobbyMemberData(lobbyId, PlayerReadyKey, readyValue);

        // 캐시 업데이트
        if (_playerDataCache.ContainsKey(playerId))
        {
            var data = _playerDataCache[playerId];
            data.isReady = ready;
            _playerDataCache[playerId] = data;
        }

        Debug.Log($"플레이어 {SteamFriends.GetFriendPersonaName(playerId)} 준비 상태: {ready}");
    }

    public string GetUnreadyPlayers(CSteamID lobbyId)
    {
        List<string> unreadyPlayers = new List<string>();
        int memberCount = SteamMatchmaking.GetNumLobbyMembers(lobbyId);

        for (int i = 0; i < memberCount; i++)
        {
            CSteamID memberId = SteamMatchmaking.GetLobbyMemberByIndex(lobbyId, i);
            if (!GetPlayerReady(lobbyId, memberId))
            {
                unreadyPlayers.Add(SteamFriends.GetFriendPersonaName(memberId));
            }
        }

        return string.Join(", ", unreadyPlayers);
    }


    /// <summary>
    /// 플레이어 준비 상태 가져오기
    /// </summary>
    /// <param name="lobbyId">로비 ID</param>
    /// <param name="playerId">플레이어 ID</param>
    /// <returns>준비했다면 True 아니라면 False</returns>
    public bool GetPlayerReady(CSteamID lobbyId, CSteamID playerId)
    {
        string readyValue = SteamMatchmaking.GetLobbyMemberData(lobbyId, playerId, PlayerReadyKey);
        return readyValue == "1";
    }

    /// <summary>
    /// 모든 플레이어가 준비되었는지 확인
    /// </summary>
    /// <param name="lobbyId">로비 ID</param>
    /// <returns>모든 플레이어 준비 상태</returns>
    public bool AreAllPlayersReady(CSteamID lobbyId)
    {
        int memberCount = SteamMatchmaking.GetNumLobbyMembers(lobbyId);

        // 최소 2명 이상이어야 게임 시작 가능
        //if (memberCount < 2) return false;

        for (int i = 0; i < memberCount; i++)
        {
            CSteamID memberId = SteamMatchmaking.GetLobbyMemberByIndex(lobbyId, i);

            // 호스트는 준비 상태 확인 제외
            if (SteamMatchmaking.GetLobbyOwner(lobbyId) == memberId) continue;

            if (!GetPlayerReady(lobbyId, memberId))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// 플레이어 데이터 캐시 업데이트
    /// </summary>
    /// <param name="lobbyId">로비 ID</param>
    public void UpdatePlayerDataCache(CSteamID lobbyId)
    {
        _playerDataCache.Clear();
        int memberCount = SteamMatchmaking.GetNumLobbyMembers(lobbyId);
        CSteamID hostId = SteamMatchmaking.GetLobbyOwner(lobbyId);

        for (int i = 0; i < memberCount; i++)
        {
            CSteamID memberId = SteamMatchmaking.GetLobbyMemberByIndex(lobbyId, i);
            PlayerLobbyData data = new PlayerLobbyData(memberId);

            data.isHost = (memberId == hostId);
            data.isReady = GetPlayerReady(lobbyId, memberId);

            _playerDataCache[memberId] = data;
        }

        Debug.Log($"플레이어 데이터 캐시 업데이트 완료: {memberCount}명");
    }

    /// <summary>
    /// 캐시된 플레이어 데이터 가져오기
    /// </summary>
    /// <param name="playerId">플레이어 ID</param>
    public PlayerLobbyData? GetCachedPlayerData(CSteamID playerId)
    {
        if (_playerDataCache.ContainsKey(playerId))
        {
            return _playerDataCache[playerId];
        }
        return null;
    }

    /// <summary>
    /// 모든 캐시된 플레이어 데이터 가져오기
    /// </summary>
    public Dictionary<CSteamID, PlayerLobbyData> GetAllCachedPlayerData()
    {
        return new Dictionary<CSteamID, PlayerLobbyData>(_playerDataCache);
    }

    /// <summary>
    /// 로비 유효성 검증
    /// </summary>
    /// <param name="lobbyCode">검증할 로비 코드</param>
    /// <returns>유효한 로비 코드 여부</returns>
    public bool IsValidLobbyCode(string lobbyCode)
    {
        if (string.IsNullOrEmpty(lobbyCode)) return false;
        if (lobbyCode.Length != _lobbyCodeLength) return false;

        foreach (char c in lobbyCode)
        {
            if (!_codeCharacters.Contains(c.ToString())) return false;
        }

        return true;
    }

    /// <summary>
    /// 현재 로비 정보 초기화
    /// </summary>
    public void ClearLobbyData()
    {
        _currentLobbyId = CSteamID.Nil;
        _currentLobbyCode = string.Empty;
        _currentHostName = string.Empty;
        _playerDataCache.Clear();
    }
}