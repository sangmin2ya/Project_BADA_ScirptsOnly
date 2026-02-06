using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SubmarineDiveController : MonoBehaviour
{
    [Header("잠수 동작 관련")]
    [Tooltip("현재 잠수 중인가")]
    private bool _isSubmerged = false;
    [Tooltip("잠수 시 Stacking Sprite 깊이")]
    [SerializeField] private float _diveDepth = -7;
    [Tooltip("상승 시 Stacking Sprite 깊이")]
    [SerializeField] private float _surfaceDepth = -2;
    private float _currentDepth;
    private float _startDepth;
    [Tooltip("잠수 동작 시간")]
    [SerializeField] private float _diveDuration = 2;
    private float _diveTimer = 0;
    [Tooltip("현재 잠수 수심이 변화하는 중인가")]
    private bool _isDivingInProgress = false;

    [Header("잠수 움직임 커브")]
    [Tooltip("잠수할 때의 움직임 곡선")]
    [SerializeField] private AnimationCurve _diveCurve;
    [Tooltip("같이 잠수할 Stacking Sprite")]
    [SerializeField] private DisplayObject _stackingSprite;

    [Header("잠수에 영향을 받는 콜라이더")]
    [Tooltip("일반상태 잠수함 콜라이더")]
    [SerializeField] private Collider2D _normalSubmarineCollider;
    [Tooltip("잠수 상태 잠수함 콜라이더")]
    [SerializeField] private Collider2D _submergedSubmarineCollider;
    [Tooltip("에어탱크 모듈의 콜라이더들")]
    [SerializeField] private List<Collider2D> _airTankColliders;
    [Tooltip("엔진모듈의 콜라이더")]
    [SerializeField] private Collider2D _engineCollider;
    [Tooltip("함포 모듈의 콜라이더")]
    [SerializeField] private Collider2D _cannonCollider;

    public bool IsSubmerged { get { return _isSubmerged; } }
    private float _diveTimeRatio => _diveTimer / _diveDuration;
    private float _targetDepth => _isSubmerged ? _diveDepth : _surfaceDepth;
    private float _previousDepth;

    private void Awake()
    {
        _currentDepth = _surfaceDepth;
        _stackingSprite = GetComponent<DisplayObject>();
    }

    private void Update()
    {
        DiveUpdate();
    }

    private void DiveUpdate()
    {
        if (!_isDivingInProgress)
        {
            return;
        }

        _diveTimer += Time.deltaTime;
        if (_diveTimer >= _diveDuration)
        {
            _isDivingInProgress = false;
            _diveTimer = _diveDuration;
        }

        _previousDepth = _currentDepth;
        _currentDepth = _startDepth + (_diveCurve.Evaluate(_diveTimeRatio) * (float)(_targetDepth - _startDepth));
        float depthChange = _currentDepth - _previousDepth;

        _stackingSprite.ChangeDepth(depthChange);
    }

    /// <summary>
    /// 잠수함의 잠수 상태를 변경
    /// </summary>
    /// <param name="willDiving">잠수할 것인가</param>
    public void DoDive(bool willDiving)
    {
        _diveTimer = 0;
        _isDivingInProgress = true;
        _isSubmerged = willDiving;
        _startDepth = _currentDepth;
        ToggleModuleColliderOnDive();
    }


    /// <summary>
    /// 아래로 들어갈떄 모듈들의 콜라이더를 꺼주고, 잠수함의 콜라이더를 조정해주는 스크립트
    /// [박상민 - 2025.08.13]
    /// </summary>
    public void ToggleModuleColliderOnDive()
    {
        if (_isSubmerged)
        {
            if (_normalSubmarineCollider != null)
                _normalSubmarineCollider.enabled = false;

            if (_submergedSubmarineCollider != null)
                _submergedSubmarineCollider.enabled = true;

            foreach (var airTankCollider in _airTankColliders)
            {
                if (airTankCollider != null)
                    airTankCollider.enabled = false;
            }

            if (_engineCollider != null)
                _engineCollider.enabled = false;

            if (_cannonCollider != null)
                _cannonCollider.enabled = false;
        }
        else
        {
            if (_normalSubmarineCollider != null)
                _normalSubmarineCollider.enabled = true;

            if (_submergedSubmarineCollider != null)
                _submergedSubmarineCollider.enabled = false;

            foreach (var airTankCollider in _airTankColliders)
            {
                if (airTankCollider != null)
                    airTankCollider.enabled = true;
            }

            if (_engineCollider != null)
                _engineCollider.enabled = true;

            if (_cannonCollider != null)
                _cannonCollider.enabled = true;
        }
    }
}
