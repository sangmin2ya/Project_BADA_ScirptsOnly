using UnityEngine;
using System.Collections;
using Mirror;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 함선 침몰시 깊이값을 컨트롤하는 스크립트
/// [박상민 - 2025.09.19]
/// </summary>
public class ShipSinkController : NetworkBehaviour
{
    [Header("<color=green>함선 침몰 설정 - 초기</color>")]
    [Tooltip("함선 기본 깊이")]
    [SerializeField] private float _defaultDepth = -1f;
    [Tooltip("함선 침몰 단계 시간")]
    [SerializeField] private float _sinkingPhaseDuration = 60f;
    [Tooltip("함선 침몰중일때 깊이")]
    [SerializeField] private float _sinkingDepth = 0.5f;
    [Tooltip("피격시 줄어드는 시간초")]
    [SerializeField] private float _sinkingPhaseDecreaseOnHit = 10f;
    [Tooltip("침몰용 UI")]
    [SerializeField] private Image _currentSinkingStateImage;
    [Tooltip("침몰용 UI 텍스트")]
    [SerializeField] private TMP_Text _currentSinkingPercentText;
    [Tooltip("이 오브젝트가 플레이어 함선인지 여부")]
    [SerializeField] private bool _isPlayerShip = false;

    [Header("<color=green>함선 침몰 설정 - 파괴시</color>")]
    [Tooltip("함선 침몰 시간")]
    [SerializeField] private float _sinkDuration = 5f;
    [Tooltip("함선 최대 침몰 깊이")]
    [SerializeField] private float _maxDepth = 50f;

    private bool _isSinkingPhase = false;
    public bool IsSinkingPhase => _isSinkingPhase;
    [SyncVar(hook = nameof(OnSinkingPhaseTimerChanged))] private float _sinkingPhaseTimer = 0f; // 침몰 단계 타이머
    private IDamageable _damageableParentObject;
    private DisplayObject _displayObject;
    private bool _destroyOnce = false;

    void Awake()
    {
        _displayObject = GetComponent<DisplayObject>();
        _damageableParentObject = GetComponent<IDamageable>();
        if (_displayObject == null)
        {
            Debug.LogError("ShipSinkController: DisplayObject 컴포넌트를 찾을 수 없습니다.");
        }

        if (!_isPlayerShip)
        {
            return;
        }

        _currentSinkingStateImage = GameObject.Find("SinkAmountImage").GetComponent<Image>();
        _currentSinkingPercentText = GameObject.Find("SinkPercentText").GetComponent<TMP_Text>();
        _currentSinkingStateImage.fillAmount = 0f;
        _currentSinkingStateImage.transform.parent.gameObject.SetActive(false);
        _sinkingPhaseTimer = _sinkingPhaseDuration;
        GetComponent<AgentController>()?.RpcChangeHeightOffset(_defaultDepth); // 기본 갑판 높이 오프셋으로 변경
    }

    void Update()
    {
        if (!isServer)
        {
            return;
        }
        // 침몰단계면 타이머 감소
        if (IsSinkingPhase)
        {
            if (_destroyOnce)
            {
                return;
            }

            _sinkingPhaseTimer -= Time.deltaTime;
            if (_sinkingPhaseTimer <= 0f)
            {
                //타이머가 0이되면 파괴용 침몰 시작
                _sinkingPhaseTimer = 0f;
                //Ship은 파괴되어야함. - @@@@@@@@@@@@@이후에 개선 필요
                ShipSpawner.Instance?.CurrentShip?.GetComponent<IDamageable>()?.DestroySelf();
                RpcStartSinkingOnDestroy();
                _destroyOnce = true;
            }
        }
        else // 침몰단계가 아니라면 서서히 타이머 증가
        {
            if (_sinkingPhaseTimer < _sinkingPhaseDuration)
            {
                _sinkingPhaseTimer += Time.deltaTime;
            }
            if (_sinkingPhaseTimer >= _sinkingPhaseDuration)
            {
                _sinkingPhaseTimer = _sinkingPhaseDuration;
                GetComponent<AgentController>()?.RpcChangeHeightOffset(_defaultDepth); // 떠오르면 기본 갑판 높이 오프셋으로 변경
                //타이머가 초기화되면 깊이 초기화
                RpcSetSinkingDepth(false);
            }
        }
    }

    #region 초기 침몰 단계

    [Server]
    public void StartSinkingPhase()
    {
        if (_isSinkingPhase)
        {
            return; // 이미 침몰 단계가 시작되었으면 무시
        }
        _isSinkingPhase = true;
        GetComponent<AgentController>()?.RpcChangeHeightOffset(_sinkingDepth); // 가라앉은만큼 갑판 높이 오프셋 감소
        RpcSetSinkingDepth(true); // 모든 클라이언트에게 침몰 단계 시작 알림
    }

    [Server]
    public void StopSinkingPhase()
    {
        if (!_isSinkingPhase)
        {
            return; // 이미 침몰 단계가 종료되었으면 무시
        }
        _isSinkingPhase = false;
    }
    /// <summary>
    /// 침몰 단계 시작시 물에 잠겼다는 모습을 보여줌
    /// </summary>
    [ClientRpc]
    private void RpcSetSinkingDepth(bool isSinking)
    {
        if (!_isPlayerShip)
        {
            return;
        }
        float targetDepth = isSinking ? _sinkingDepth : _defaultDepth;
        _displayObject.ChangeDepthTo(targetDepth);
    }

    private void OnSinkingPhaseTimerChanged(float oldValue, float newValue)
    {
        if (!_isPlayerShip)
        {
            return;
        }
        //최대라면 UI채우고 비활성화, 조금이라도 달았으면 보여주기
        if (newValue >= _sinkingPhaseDuration)
        {
            _currentSinkingStateImage.fillAmount = 0f;
            _currentSinkingStateImage.transform.parent.gameObject.SetActive(false);
        }
        else if (newValue < _sinkingPhaseDuration)
        {
            if (!_currentSinkingStateImage.transform.parent.gameObject.activeSelf)
            {
                _currentSinkingStateImage.transform.parent.gameObject.SetActive(true);
            }
            _currentSinkingStateImage.fillAmount = 1 - newValue / _sinkingPhaseDuration;
            _currentSinkingPercentText.text = Mathf.CeilToInt((1 - newValue / _sinkingPhaseDuration) * 100f).ToString() + "%";
        }
    }

    /// <summary>
    /// 피격시 침몰 단계 타이머 감소
    /// </summary>
    public void DecreaseSinkingPhaseTimerOnHit()
    {
        if (!_isPlayerShip)
        {
            return;
        }
        if (!isServer || !IsSinkingPhase)
        {
            return;
        }
        _sinkingPhaseTimer -= _sinkingPhaseDecreaseOnHit;
        if (_sinkingPhaseTimer < 0f)
        {
            _sinkingPhaseTimer = 0f;
        }
    }

    #endregion

    #region 파괴시 실행되는 잠수기능
    /// <summary>
    /// 모든 클라이언트들에게 침몰 시작
    /// </summary>
    [ClientRpc]
    public void RpcStartSinkingOnDestroy()
    {
        StartCoroutine(SinkCoroutine());
    }

    private IEnumerator SinkCoroutine()
    {
        float elapsedTime = 0f;
        float initialDepth = _displayObject.WaterDepth;

        while (elapsedTime < _sinkDuration)
        {
            elapsedTime += Time.deltaTime;
            float newDepth = Mathf.Lerp(initialDepth, _maxDepth, elapsedTime / _sinkDuration);
            _displayObject.ChangeDepthTo(newDepth);
            yield return null;
        }

        // 침몰 완료 후 깊이를 최대값으로 설정
        _displayObject.ChangeDepthTo(_maxDepth);

        // 서버만 침몰후 오브젝트 제거
        _damageableParentObject.DestroySelf();
    }

    #endregion
}