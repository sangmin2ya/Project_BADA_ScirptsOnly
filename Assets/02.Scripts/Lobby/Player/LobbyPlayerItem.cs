using UnityEngine;
using UnityEngine.UI;
using Steamworks;
using TMPro;
using Mirror;
using System.Collections;

/// <summary>
/// 네트워크 로비 참가자 UI 아이템을 관리하는 클래스
/// [박상민 - 2025.06.10]
/// </summary>
[RequireComponent(typeof(NetworkIdentity))]
public class LobbyPlayerItem : NetworkBehaviour
{
    [Header("<color=yellow>UI 컴포넌트")]
    [Tooltip("플레이어 이름을 표시할 텍스트")]
    [SerializeField] private TMP_Text _playerNameText;
    [Tooltip("플레이어 아바타 이미지")]
    [SerializeField] private RawImage _playerAvatarImage;
    [Tooltip("호스트 표시 아이콘")]
    [SerializeField] private GameObject _hostIcon;
    [Tooltip("준비 상태 표시 아이콘")]
    [SerializeField] private GameObject _readyIcon;
    [Tooltip("플레이어 상태 배경")]
    [SerializeField] private Image _backgroundImage;
    [Tooltip("플레이어 핑 텍스트")]
    [SerializeField] private TMP_Text _pingText;

    [Header("<color=green>색상 설정")]
    [Tooltip("호스트 배경 색상")]
    [SerializeField] private Color _hostBackgroundColor = new Color(1f, 0.8f, 0.2f, 0.3f);
    [Tooltip("준비 완료 배경 색상")]
    [SerializeField] private Color _readyBackgroundColor = new Color(0.2f, 1f, 0.2f, 0.3f);
    [Tooltip("일반 배경 색상")]
    [SerializeField] private Color _normalBackgroundColor = new Color(1f, 1f, 1f, 0.1f);

    [Header("<color=blue>핑 설정")]
    [Tooltip("핑 측정 간격 (초)")]
    [SerializeField] private float _pingUpdateInterval = 1f;

    // 네트워크 동기화 변수들
    [SyncVar(hook = nameof(OnPlayerIdChanged))]
    private ulong _steamId;

    [SyncVar(hook = nameof(OnPlayerNameChanged))]
    private string _playerName = "";

    [SyncVar(hook = nameof(OnHostStatusChanged))]
    private bool _isHost = false;

    [SyncVar(hook = nameof(OnReadyStatusChanged))]
    private bool _isReady = false;

    [SyncVar(hook = nameof(OnPingChanged))]
    private float _ping = 0f;

    // 프로퍼티들
    public CSteamID PlayerId => new CSteamID(_steamId);
    public string PlayerName => _playerName;
    public bool IsHost => _isHost;
    public bool IsReady => _isReady;
    public float Ping => _ping;

    // 핑 측정 관련
    private Coroutine _pingCoroutine;

    #region Unity Lifecycle
    public override void OnStartClient()
    {
        base.OnStartClient();

        // 클라이언트에서 UI 업데이트
        UpdateUI();

        Transform contentParent = FindAnyObjectByType<LobbyUIManager>().GetPlayerListContent();
        transform.SetParent(contentParent, false);
    }

    public override void OnStartLocalPlayer()
    {
        base.OnStartLocalPlayer();

        // 로컬 플레이어만 자신의 정보를 서버에 전송
        if (isLocalPlayer)
        {
            CSteamID mySteamId = SteamUser.GetSteamID();
            string myName = SteamFriends.GetPersonaName();

            CmdInitPlayerInfo(mySteamId.m_SteamID, myName);

            // 핑 측정 시작
            StartPingMeasurement();
        }
    }

    public override void OnStopClient()
    {
        base.OnStopClient();

        // 핑 측정 중단
        StopPingMeasurement();
    }

    void OnDestroy()
    {
        // 동적으로 생성된 아바타 텍스처 정리
        CleanupAvatarTexture();

        // 핑 측정 중단
        StopPingMeasurement();
    }

    #endregion

    #region Network Commands & RPCs

    /// <summary>
    /// 플레이어 정보를 서버에 전송
    /// </summary>
    [Command]
    private void CmdInitPlayerInfo(ulong steamId, string playerName)
    {
        _steamId = steamId;
        _playerName = playerName;

        // 호스트 여부 확인 (첫 번째 플레이어가 호스트)
        if (NetworkServer.connections.Count == 1)
        {
            _isHost = true;
            _isReady = true; // 호스트는 자동으로 준비 상태
        }
    }

    /// <summary>
    /// 준비 상태 변경 (클라이언트 → 서버)
    /// </summary>
    [Command]
    public void CmdSetReadyStatus(bool ready)
    {
        if (!_isHost) // 호스트는 준비 상태 변경 불가
        {
            _isReady = ready;
        }
    }

    /// <summary>
    /// 핑 업데이트 (클라이언트 → 서버)
    /// </summary>
    [Command]
    private void CmdUpdatePing(float ping)
    {
        _ping = ping;
    }

    /// <summary>
    /// 핑 측정 요청 (서버 → 클라이언트)
    /// </summary>
    [ClientRpc]
    private void RpcPingRequest(float timestamp)
    {
        if (isLocalPlayer)
        {
            // 즉시 응답
            CmdPingResponse(timestamp);
        }
    }

    /// <summary>
    /// 핑 측정 응답 (클라이언트 → 서버)
    /// </summary>
    [Command]
    private void CmdPingResponse(float timestamp)
    {
        float roundTripTime = Time.time - timestamp;
        _ping = roundTripTime * 1000f; // 밀리초로 변환
    }

    #endregion

    #region SyncVar Hooks

    /// <summary>
    /// Steam ID 변경 시 호출
    /// </summary>
    private void OnPlayerIdChanged(ulong oldId, ulong newId)
    {
        UpdatePlayerName();
        LoadPlayerAvatar();
    }

    /// <summary>
    /// 플레이어 이름 변경 시 호출
    /// </summary>
    private void OnPlayerNameChanged(string oldName, string newName)
    {
        UpdatePlayerName();
    }

    /// <summary>
    /// 호스트 상태 변경 시 호출
    /// </summary>
    private void OnHostStatusChanged(bool oldStatus, bool newStatus)
    {
        UpdatePlayerName();
        UpdateHostStatus();
        UpdateBackgroundColor();
    }

    /// <summary>
    /// 준비 상태 변경 시 호출
    /// </summary>
    private void OnReadyStatusChanged(bool oldStatus, bool newStatus)
    {
        UpdateReadyStatus();
        UpdateBackgroundColor();
    }

    /// <summary>
    /// 핑 변경 시 호출
    /// </summary>
    private void OnPingChanged(float oldPing, float newPing)
    {
        UpdatePingDisplay();
    }

    #endregion

    #region UI Updates

    /// <summary>
    /// 전체 UI 업데이트
    /// </summary>
    private void UpdateUI()
    {
        UpdatePlayerName();
        UpdateHostStatus();
        UpdateReadyStatus();
        UpdateBackgroundColor();
        UpdatePingDisplay();
        LoadPlayerAvatar();
    }

    /// <summary>
    /// 플레이어 이름 업데이트
    /// </summary>
    private void UpdatePlayerName()
    {
        if (_playerNameText == null) return;

        string displayName = string.IsNullOrEmpty(_playerName) ? "Unknown" : _playerName;

        // 호스트 표시
        if (_isHost)
        {
            _playerNameText.text = $"{displayName} (방장)";
        }
        else
        {
            _playerNameText.text = displayName;
        }
    }

    /// <summary>
    /// 호스트 상태 업데이트
    /// </summary>
    private void UpdateHostStatus()
    {
        if (_hostIcon != null)
        {
            _hostIcon.SetActive(_isHost);
        }
    }

    /// <summary>
    /// 준비 상태 업데이트
    /// </summary>
    private void UpdateReadyStatus()
    {
        if (_readyIcon != null)
        {
            // 호스트는 항상 준비 상태로 간주하되, 아이콘은 표시하지 않음
            _readyIcon.SetActive(_isReady && !_isHost);
        }
    }

    /// <summary>
    /// 배경 색상 업데이트
    /// </summary>
    private void UpdateBackgroundColor()
    {
        if (_backgroundImage == null) return;

        if (_isHost)
        {
            _backgroundImage.color = _hostBackgroundColor;
        }
        else if (_isReady)
        {
            _backgroundImage.color = _readyBackgroundColor;
        }
        else
        {
            _backgroundImage.color = _normalBackgroundColor;
        }
    }

    /// <summary>
    /// 핑 표시 업데이트
    /// </summary>
    private void UpdatePingDisplay()
    {
        if (_pingText == null) return;

        if (_ping <= 0)
        {
            _pingText.text = "-";
        }
        else
        {
            _pingText.text = $"{_ping:F0}ms";

            // 핑에 따른 색상 변경
            if (_ping < 50)
                _pingText.color = Color.green;
            else if (_ping < 100)
                _pingText.color = Color.yellow;
            else
                _pingText.color = Color.red;
        }
    }

    #endregion

    #region Ping Measurement

    /// <summary>
    /// 핑 측정 시작
    /// </summary>
    private void StartPingMeasurement()
    {
        if (_pingCoroutine != null)
        {
            StopCoroutine(_pingCoroutine);
        }

        _pingCoroutine = StartCoroutine(PingMeasurementCoroutine());
    }

    /// <summary>
    /// 핑 측정 중단
    /// </summary>
    private void StopPingMeasurement()
    {
        if (_pingCoroutine != null)
        {
            StopCoroutine(_pingCoroutine);
            _pingCoroutine = null;
        }
    }

    /// <summary>
    /// 핑 측정 코루틴
    /// </summary>
    private IEnumerator PingMeasurementCoroutine()
    {
        while (isLocalPlayer && NetworkClient.isConnected)
        {
            if (isServer)
            {
                // 서버인 경우 (호스트) 핑은 0
                if (_ping != 0)
                {
                    CmdUpdatePing(0);
                }
            }
            else
            {
                // 클라이언트인 경우 RTT 측정
                MeasurePing();
            }

            yield return new WaitForSeconds(_pingUpdateInterval);
        }
    }

    /// <summary>
    /// 핑 측정 실행
    /// </summary>
    private void MeasurePing()
    {
        if (isServer)
        {
            // 서버에서 모든 클라이언트에게 핑 요청
            RpcPingRequest(Time.time);
        }
        else
        {
            // 클라이언트에서는 NetworkTime을 사용한 간단한 핑 계산
            float ping = (float)(NetworkTime.rtt * 1000.0); // 밀리초로 변환
            CmdUpdatePing(ping);
        }
    }

    #endregion

    #region Avatar Management

    /// <summary>
    /// Steam 프로필 이미지 로드
    /// </summary>
    private void LoadPlayerAvatar()
    {
        if (_playerAvatarImage == null || _steamId == 0) return;

        CSteamID steamId = new CSteamID(_steamId);

        // Steam 아바타 ID 가져오기
        int avatarId = SteamFriends.GetMediumFriendAvatar(steamId);
        if (avatarId == 0)
        {
            // 기본 아바타 표시
            _playerAvatarImage.color = Color.gray;
            Debug.Log($"플레이어 {steamId}의 아바타를 찾을 수 없습니다.");
            return;
        }

        // 아바타 이미지 크기 가져오기
        uint width, height;
        if (!SteamUtils.GetImageSize(avatarId, out width, out height) || width == 0 || height == 0)
        {
            _playerAvatarImage.color = Color.gray;
            Debug.LogWarning($"유효하지 않은 아바타 크기: {width}x{height}");
            return;
        }

        // 이미지 데이터 가져오기
        byte[] imageData = new byte[width * height * 4]; // RGBA
        if (!SteamUtils.GetImageRGBA(avatarId, imageData, (int)(width * height * 4)))
        {
            _playerAvatarImage.color = Color.gray;
            Debug.LogWarning("Steam 아바타 데이터를 가져올 수 없습니다.");
            return;
        }

        // Unity Texture2D로 변환
        Texture2D avatarTexture = new Texture2D((int)width, (int)height, TextureFormat.RGBA32, false);
        avatarTexture.filterMode = FilterMode.Bilinear;

        // Steam 이미지는 상하가 뒤집혀 있으므로 수정하면서 적용
        Color32[] pixels = new Color32[width * height];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int steamIndex = ((int)height - 1 - y) * (int)width + x;
                int dataIndex = steamIndex * 4;
                int unityIndex = y * (int)width + x;

                pixels[unityIndex] = new Color32(
                    imageData[dataIndex + 0], // R
                    imageData[dataIndex + 1], // G
                    imageData[dataIndex + 2], // B
                    imageData[dataIndex + 3]  // A
                );
            }
        }

        avatarTexture.SetPixels32(pixels);
        avatarTexture.Apply();

        // RawImage에 적용
        _playerAvatarImage.texture = avatarTexture;
        _playerAvatarImage.color = Color.white;
    }

    /// <summary>
    /// 아바타 텍스처 메모리 정리
    /// </summary>
    private void CleanupAvatarTexture()
    {
        if (_playerAvatarImage != null && _playerAvatarImage.texture != null)
        {
            // 기본 텍스처가 아닌 동적 생성된 텍스처만 삭제
            Texture texture = _playerAvatarImage.texture;
            if (texture.name != "DefaultAvatarTexture") // 기본 텍스처 이름은 제외
            {
                Destroy(texture);
            }
        }
    }

    #endregion

    #region Public API

    /// <summary>
    /// 플레이어가 자신인지 확인
    /// </summary>
    /// <returns>자신 여부</returns>
    public bool IsLocalPlayer()
    {
        return isLocalPlayer;
    }

    /// <summary>
    /// 준비 상태 토글 (로컬 플레이어만)
    /// </summary>
    public void ToggleReady()
    {
        if (isLocalPlayer && !_isHost)
        {
            CmdSetReadyStatus(!_isReady);
        }
    }

    /// <summary>
    /// 호스트 권한으로 준비 상태 설정
    /// </summary>
    [Server]
    public void SetReadyStatus(bool ready)
    {
        if (!_isHost) // 호스트는 항상 준비 상태
        {
            _isReady = ready;
        }
    }

    /// <summary>
    /// 호스트 권한으로 호스트 상태 설정
    /// </summary>
    [Server]
    public void SetHostStatus(bool isHost)
    {
        _isHost = isHost;
        if (_isHost)
        {
            _isReady = true; // 호스트는 항상 준비 상태
        }
    }

    #endregion
}