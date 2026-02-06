using UnityEngine;
using TMPro;
using Mirror;
using System.Collections.Generic;
using UnityEngine.UI;

public class SessionManager : NetworkSingleton<SessionManager>
{
    [Header("게임 타이머 설정")]
    [Tooltip("낮 시간 길이 (초)")]
    [SerializeField] private float _dayTimeLength = 900f;
    [Tooltip("황혼 시간 길이 (초)")]
    [SerializeField] private float _eveningTimeLength = 300f;
    [Tooltip("밤 시간 길이 (초)")]
    [SerializeField] private float _nightTimeLength = 300f;
    [Tooltip("심야 시간 길이 (초)")]
    [SerializeField] private float _midNightTimeLength = 300f;

    [Header("비밀기지 표시 설정")]

    // private 변수
    [SyncVar(hook = nameof(OnSessionTimeChanged))] private float _sessionTime;
    private TMP_Text _timerText;
    private GameObject _eveningIndicator;
    private GameObject _nightIndicator;
    private GameObject _midNightIndicator;
    [SyncVar(hook = nameof(OnRadarActivationCountChanged))]
    private int _radarActivationCount = 0;

    private List<GameObject> _secretBaseIndicators = new List<GameObject>();

    // public 프로퍼티
    public float SessionElapsedTime => _sessionTime;
    public int RadarActivationCount => _radarActivationCount;
    public bool IsRadarActive => _radarActivationCount <= 3; //최대 3번의 레이더 활성화 가능
    // 타임유닛 변화할때 난이도 조절을 하기위한 프로퍼티
    public float SessionElapsedTimePerTimeUnit
    {
        get
        {
            switch (CurrentTimeType)
            {
                case TimeType.낮:
                    return _dayTimeLength;
                case TimeType.황혼:
                    return _dayTimeLength + _eveningTimeLength;
                case TimeType.밤:
                    return _dayTimeLength + _eveningTimeLength + _nightTimeLength;
                case TimeType.심야:
                    return _dayTimeLength + _eveningTimeLength + _nightTimeLength + _midNightTimeLength;
                default:
                    return 0f;
            }
        }
    }
    public TimeType CurrentTimeType
    {
        get
        {
            if (_sessionTime < _dayTimeLength)
            {
                return TimeType.낮;
            }
            else if (_sessionTime < _dayTimeLength + _eveningTimeLength)
            {
                return TimeType.황혼;
            }
            else if (_sessionTime < _dayTimeLength + _eveningTimeLength + _nightTimeLength)
            {
                return TimeType.밤;
            }
            else
            {
                return TimeType.심야;
            }
        }
    }

    protected override void Awake()
    {
        base.Awake();

        // 시간대 UI 초기화
        _timerText = UIManager.Instance.TimerText;
        _eveningIndicator = UIManager.Instance.NightIndicators[0].gameObject;
        _nightIndicator = UIManager.Instance.NightIndicators[1].gameObject;
        _midNightIndicator = UIManager.Instance.NightIndicators[2].gameObject;
        _eveningIndicator.SetActive(false);
        _nightIndicator.SetActive(false);
        _midNightIndicator.SetActive(false);

        //비밀기지 UI 초기화
        _secretBaseIndicators = UIManager.Instance.SecretBaseIndicators;
        foreach (var indicator in _secretBaseIndicators)
        {
            indicator.SetActive(false);
        }
    }

    void Start()
    {
        if (!isServer)
        {
            return;
        }
        _sessionTime = 0f;
    }

    void Update()
    {
        if (!isServer)
        {
            return;
        }
        CountTime();
        if (Input.GetKeyDown(KeyCode.T))
        {
            GameNetworkManager.singleton.ReturnToBaseCamp();
        }
    }

    #region 시간 관리

    private void CountTime()
    {
        _sessionTime += Time.deltaTime;
    }
    #endregion

    #region 레이더 관리

    [Server]
    public void ActivateRadar()
    {
        if (!IsRadarActive)
        {
            //레이더는 이미 최대로 활성화되어 있음
            return;
        }
        RpcUpdateRadarIndicators(_radarActivationCount);
        _radarActivationCount++;
    }

    #endregion

    #region 클라이언트 동기화

    private void OnSessionTimeChanged(float oldTime, float newTime)
    {
        if (_timerText != null)
        {
            if (CurrentTimeType == TimeType.낮)
            {
                float remainingTime = _dayTimeLength - newTime;
                int minutes = Mathf.FloorToInt(remainingTime / 60f);
                int seconds = Mathf.FloorToInt(remainingTime % 60f);
                _timerText.SetText($"{minutes:00}:{seconds:00}");
            }
            else if (CurrentTimeType == TimeType.황혼)
            {
                float elapsedEveningTime = newTime - _dayTimeLength;
                int minutes = Mathf.FloorToInt(elapsedEveningTime / 60f);
                int seconds = Mathf.FloorToInt(elapsedEveningTime % 60f);
                _timerText.SetText($"{minutes:00}:{seconds:00}");
            }
            else if (CurrentTimeType == TimeType.밤)
            {
                float elapsedNightTime = newTime - _dayTimeLength - _eveningTimeLength;
                int minutes = Mathf.FloorToInt(elapsedNightTime / 60f);
                int seconds = Mathf.FloorToInt(elapsedNightTime % 60f);
                _timerText.SetText($"{minutes:00}:{seconds:00}");
            }
            else if (CurrentTimeType == TimeType.심야)
            {
                float elapsedDarkNightTime = newTime - _dayTimeLength - _eveningTimeLength - _nightTimeLength;
                int minutes = Mathf.FloorToInt(elapsedDarkNightTime / 60f);
                int seconds = Mathf.FloorToInt(elapsedDarkNightTime % 60f);
                _timerText.SetText($"{minutes:00}:{seconds:00}");
            }
            ToggleNightIndicator(CurrentTimeType);
        }
    }

    private void ToggleNightIndicator(TimeType timeType)
    {
        switch (timeType)
        {
            case TimeType.낮:
                _timerText?.gameObject.SetActive(true);
                _eveningIndicator?.SetActive(false);
                _nightIndicator?.SetActive(false);
                _midNightIndicator?.SetActive(false);
                break;
            case TimeType.황혼:
                _timerText?.gameObject.SetActive(false);
                _eveningIndicator?.SetActive(true);
                _nightIndicator?.SetActive(false);
                _midNightIndicator?.SetActive(false);
                break;
            case TimeType.밤:
                _timerText?.gameObject.SetActive(false);
                _eveningIndicator?.SetActive(false);
                _nightIndicator?.SetActive(true);
                _midNightIndicator?.SetActive(false);
                break;
            case TimeType.심야:
                _timerText?.gameObject.SetActive(false);
                _eveningIndicator?.SetActive(false);
                _nightIndicator?.SetActive(false);
                _midNightIndicator?.SetActive(true);
                break;
        }
    }

    [ClientRpc]
    private void RpcUpdateRadarIndicators(int index)
    {
        foreach (var indicator in _secretBaseIndicators)
        {
            indicator.SetActive(false);
        }
        _secretBaseIndicators[index].SetActive(true);
    }

    private void OnRadarActivationCountChanged(int oldCount, int newCount)
    {
        if (newCount < _secretBaseIndicators.Count)
        {
            GameObject.Find("Quest").GetComponent<TMP_Text>().text = $"- 코어를 찾아 함선 레이더에 삽입하기\n   ({newCount}/3)\n- 다음 바다로 가기 위한 아이템 찾기\n   (0/1)";
        }
    }
    #endregion
}
