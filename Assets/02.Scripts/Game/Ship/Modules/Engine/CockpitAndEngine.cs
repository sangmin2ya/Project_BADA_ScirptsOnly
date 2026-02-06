using UnityEngine;
using UnityEngine.UI;
using Mirror;
using TMPro;
using System;

/// <summary>
/// 조향 모듈 - 함선의 회전 입력을 전달하는 모듈입니다.
/// [박상민 - 2025.06.27]
/// </summary>
[RequireComponent(typeof(ModuleController))]
public class CockpitAndEngine : Module
{
    [Header("조향 설정")]
    [Tooltip("회전 입력 감도")]
    [SerializeField] private float _rotationSensitivity = 1f;

    [Header("추진 설정")]
    [Tooltip("최대 전진 기어")]
    [SerializeField] private int _maxForwardGear = 3;
    [Tooltip("최대 후진 기어")]
    [SerializeField] private int _maxBackwardGear = 1;
    [SerializeField] private TMP_Text _gearText = null; // 기어 상태를 표시할 UI 텍스트
    [Tooltip("엔진 과열 수치")]
    [SerializeField] private float _maxEngineCooling = 7.5f;

    [Header("함선 상태 (읽기 전용)")]
    [SyncVar][SerializeField, ReadOnly] private float _currentAngularInput = 0f;
    [SyncVar(hook = nameof(OnGearChanged))]
    [SerializeField, ReadOnly] private int _currentGear = 0;
    [SerializeField, ReadOnly] private float _gearShiftingCooldown = 0;
    [SyncVar(hook = nameof(OnEngineCoolingChanged))]
    [SerializeField, ReadOnly] private float _engineCooling = 0f;
    [SyncVar]
    [SerializeField, ReadOnly] private bool _engineOverheated = false;
    [SerializeField, ReadOnly] private Image _engineBar;

    public int CurrentGear => _currentGear;
    public float CurrentAngularInput => _currentAngularInput;

    protected override void Start()
    {
        base.Start();
        _moduleType = ModuleType.조향;

        InitializeUI();

        if (!isServer)
        {
            return;
        }

        InitializeModule();

        _engineCooling = _maxEngineCooling;
    }

    private void Update()
    {
        if (!isServer)
        {
            return;
        }
        UpdateCoolingSystem();
        // 권한이 없는 경우 업데이트를 건너뜀
        if (IsOccupied)
        {
            UpdateRotationInput();
            UpdateGearInput();
        }
        else
        {
            // 조작하지 않을 때는 회전 입력을 0으로 설정
            if (_currentAngularInput != 0f)
            {
                SetAngularInput(0f);
            }
        }

        // 부스터 활성화
        if (_isAbility1Activated && !_engineOverheated)
        {
            Boost();
        }
    }

    /// <summary>
    /// 부스터 사용 후 엔진을 냉각시키고 다시 정상 상태로 되돌립니다.
    /// </summary>
    [Server]
    private void UpdateCoolingSystem()
    {
        if (_engineOverheated)
        {
            _engineCooling += Time.deltaTime;
            if (_engineCooling >= _maxEngineCooling)
            {
                _engineOverheated = false; // 엔진이 다시 정상 상태로 돌아옴
                _engineCooling = _maxEngineCooling; // 냉각 수치 초기화
            }
        }
    }

    /// <summary>
    /// UI 초기화
    /// </summary>
    private void InitializeUI()
    {
        _gearText = _gearText ?? GameObject.Find("GearText")?.GetComponent<TMP_Text>();
        if (_gearText != null)
        {
            _gearText.text = $"현재 기어 : [ {_currentGear} ] 단";
        }
        _engineBar = GameObject.Find("EngineBar")?.GetComponent<Image>();
        if (_engineBar != null)
        {
            _engineBar.fillAmount = 1f;
        }
    }

    /// <summary>
    /// 모듈 초기화
    /// </summary>
    private void InitializeModule()
    {
        _moduleController = GetComponent<ModuleController>();
        _moduleController.SetConnectedModule(this);
    }

    #region 회전 제어 메소드
    /// <summary>
    /// 클라이언트에서 입력을 받아 서버로 전송하여 함선의 회전 입력을 설정합니다.
    /// </summary>
    public void SetAngularInput(float angularInput)
    {
        _currentAngularInput = angularInput;

        // ShipController에 회전 입력 전달
        if (_shipController != null)
        {
            _shipController.SetAngularInput(_currentAngularInput);
        }
    }

    /// <summary>
    /// 함선의 회전 입력을 업데이트합니다.
    /// </summary>
    private void UpdateRotationInput()
    {
        float horizontalInput = GetSignWithDeadzone(_rotationInput.x, 0.01f) * _rotationSensitivity;

        // 입력값이 변경되었을 때만 서버에 전송
        if (Mathf.Abs(horizontalInput - _currentAngularInput) > 0.01f)
        {
            SetAngularInput(horizontalInput);
        }
    }
    #endregion

    #region 기어 제어 메소드
    /// <summary>
    /// 클라이언트에서 입력을 받아 서버로 전송하여 함선의 기어를 설정합니다.
    /// </summary>
    public void SetGear(int gear)
    {
        _currentGear = gear;

        // ShipController에 기어 변경 알림
        if (_shipController != null)
        {
            _shipController.SetEngineGear(_currentGear);
        }
    }

    /// <summary>
    /// 함선의 부스터를 활성화합니다.
    /// </summary>
    private void Boost()
    {
        _engineCooling = 0f; // 부스터 사용 시 엔진 냉각 초기화
        _engineOverheated = true; // 엔진 과열 상태로 설정

        if (_shipController != null)
        {
            _shipController.ActivateBoost();
        }
    }

    /// <summary>
    /// 함선의 기어 입력을 업데이트합니다.
    /// </summary>
    private void UpdateGearInput()
    {
        if (Mathf.Abs(_rotationInput.y) > 0.5f)
        {
            if (_gearShiftingCooldown > 0)
            {
                _gearShiftingCooldown -= Time.deltaTime;
            }
            else
            {
                int newGear = _currentGear + Math.Sign(_rotationInput.y);
                newGear = Mathf.Clamp(newGear, -_maxBackwardGear, _maxForwardGear);
                _gearShiftingCooldown = 0.5f;
                SetGear(newGear);
            }
        }
        else
        {
            _gearShiftingCooldown = 0f;
        }
    }

    /// <summary>
    /// 기어가 변경되었을 때 텍스트를 업데이트합니다.
    /// </summary>
    private void OnGearChanged(int oldGear, int newGear)
    {
        if (_gearText != null)
        {
            _gearText.text = $"현재 기어 : [ {newGear} ] 단";
        }
    }

    /// <summary>
    /// 엔진 과열 상태가 변경되었을 때 UI를 업데이트합니다.
    /// </summary>
    private void OnEngineCoolingChanged(float oldCooling, float newCooling)
    {
        _engineCooling = newCooling;
        if (_engineBar != null)
        {
            _engineBar.fillAmount = _engineCooling / _maxEngineCooling;
        }
    }
    #endregion

    /// <summary>
    /// float 값에 대해서 데드존을 지정하여 부호를 판단하는 메서드
    /// [조우석 - 2025.07.30.]
    /// </summary>
    /// <param name="value"></param>
    /// <param name="tolerance"></param>
    /// <returns></returns>
    public static int GetSignWithDeadzone(float value, float tolerance)
    {
        // 값의 절대값이 오차 허용 범위보다 작은지 확인
        if (Mathf.Abs(value) < tolerance)
        {
            return 0;
        }

        return (int)Mathf.Sign(value);
    }
}