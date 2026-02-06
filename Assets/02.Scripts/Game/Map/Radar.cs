using UnityEngine;
using Mirror;
using UnityEngine.UI;

public class Radar : NetworkBehaviour, IInteractable
{
    [Header("레이더 설정")]
    [Tooltip("레이더 작동하는데 걸리는 시간")]
    [SerializeField] private int _activateTime = 5;
    [Tooltip("레이더 상호작용 상태바")]
    [SerializeField] private Image _radarInteractProgressBar = null;
    [Tooltip("상호작용 키 안내 UI")]
    [SerializeField] private GameObject _interactKeyUI = null;
    [Tooltip("레이더 강조용 파티클")]
    [SerializeField] private GameObject _radarHighlightParticle = null;

    private PlayerModuleController _playerModuleController;
    private bool _isActivating = false;
    private bool _isActive = false;
    private bool _isDeactivated = false;
    [SyncVar(hook = nameof(OnCurrentTimeChanged))] private float _currentTime = 0f;
    private SessionManager _sessionManager;

    #region IInteractable 구현
    public InteractionInfo GetInteractionInfo()
    {
        if (!isServer)
        {
            Debug.LogWarning("GetInteractionInfo는 서버에서만 호출되어야 합니다!");
            return new InteractionInfo();
        }

        string text = "코어 삽입";
        string action = "Player/Interact";
        bool interactable = SessionManager.Instance != null && SessionManager.Instance.IsRadarActive;
        Vector3 position = transform.position + Vector3.up * 1f;

        return new InteractionInfo(netId, text, action, interactable, position);
    }

    #endregion

    void Awake()
    {
        _sessionManager = SessionManager.Instance;
        if (_interactKeyUI != null)
        {
            _interactKeyUI.SetActive(false);
        }
        if (_radarInteractProgressBar != null)
        {
            _radarInteractProgressBar.fillAmount = 0f;
            _radarInteractProgressBar.transform.parent.gameObject.SetActive(false);
        }
        if (_radarHighlightParticle != null)
        {
            _radarHighlightParticle.SetActive(true);
        }
    }

    private void Update()
    {
        // // 이미 활성화 됐거나 서버가 아니면 종료
        // if (_isActive && _isDeactivated && !isServer)
        // {
        //     return;
        // }
        // // 레이더가 이미 최대치까지 활성화 되어있다면
        // if (!_sessionManager.IsRadarActive)
        // {
        //     _isDeactivated = true;
        //     _isActive = false;
        //     _isActivating = false;
        //     if (_radarHighlightParticle != null)
        //     {
        //         RpcShowRadarHighlight(false);
        //     }
        //     return;
        // }

        // if (_isActivating)
        // {
        //     _currentTime += Time.deltaTime;
        //     // 클라이언트에 진행상황 업데이트

        //     if (_currentTime >= _activateTime)
        //     {
        //         _isActive = true;
        //         _isActivating = false;
        //         Debug.Log("레이더가 활성화되었습니다.");
        //         _sessionManager.ActivateRadar();
        //         RpcShowRadarHighlight(false);
        //     }
        // }
    }

    public void StartActivateRadar(PlayerModuleController playerModuleController)
    {
        if (_isDeactivated)
        {
            return; // 이미 비활성화된 레이더는 다시 활성화할 수 없음
        }
        if (!_isActive && !_isActivating)
        {
            _isActivating = true;
            _currentTime = 0f;
            _playerModuleController = playerModuleController;
        }
    }

    public void StopActivateRadar()
    {
        if (_isDeactivated)
        {
            return; // 이미 비활성화된 레이더는 다시 활성화할 수 없음
        }
        if (_isActive)
        {
            return;
        }
        if (_isActivating)
        {
            _isActivating = false;
            _currentTime = 0f;
            // 클라이언트에 진행상황 초기화
            _playerModuleController = null;
        }
    }

    public bool ActiveRadar()
    {
        // 이미 최대치까지 활성화되어있다면
        if (!_sessionManager.IsRadarActive)
        {
            return false;
        }
        _sessionManager.ActivateRadar();
        return true;
    }

    // void OnTriggerEnter2D(Collider2D other)
    // {
    //     if (!isServer || _isActive || _isDeactivated)
    //     {
    //         return; // 서버가 아니거나 레이더가 이미 활성화되었거나 비활성화된 경우
    //     }
    //     if (other.TryGetComponent<PlayerModuleController>(out PlayerModuleController playerModuleController))
    //     {
    //         // 상호작용 UI 표시
    //         RpcShowInteractUI(playerModuleController.connectionToClient, true);
    //     }
    // }

    // void OnTriggerExit2D(Collider2D other)
    // {
    //     if (!isServer || _isActive || _isDeactivated)
    //     {
    //         return;
    //     }
    //     if (other.TryGetComponent<PlayerModuleController>(out PlayerModuleController playerModuleController))
    //     {
    //         // 상호작용 UI 숨기기
    //         RpcShowInteractUI(playerModuleController.connectionToClient, false);
    //     }
    // }

    #region 클라이언트 동기화

    void OnCurrentTimeChanged(float oldTime, float newTime)
    {
        _radarInteractProgressBar.fillAmount = Mathf.Clamp01(newTime / _activateTime);
    }

    [TargetRpc]
    public void RpcShowInteractUI(NetworkConnection target, bool isShow)
    {
        if (_interactKeyUI != null)
        {
            _interactKeyUI.SetActive(isShow);
        }
        if (_radarInteractProgressBar != null)
        {
            _radarInteractProgressBar.transform.parent.gameObject.SetActive(isShow);
        }
    }

    [ClientRpc]
    public void RpcShowRadarHighlight(bool isShow)
    {
        if (_radarHighlightParticle != null)
        {
            _radarHighlightParticle.SetActive(isShow);
        }
    }
    #endregion
}