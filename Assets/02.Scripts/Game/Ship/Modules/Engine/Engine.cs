using System;
using UnityEngine;
using Mirror;
using TMPro;

/// <summary>
/// 엔진 모듈 - 함선의 기어 입력을 전달하는 모듈입니다.
/// [박상민 - 2025.06.27]
/// </summary>
[RequireComponent(typeof(ModuleController))]
public class Engine : Module
{
    [Header("함선 설정")]
    [Tooltip("최대 전진 기어")]
    [SerializeField] private int _maxForwardGear = 3;
    [Tooltip("최대 후진 기어")]
    [SerializeField] private int _maxBackwardGear = 1;
    [SerializeField] private TMP_Text _gearText = null; // 기어 상태를 표시할 UI 텍스트

    [Header("함선 상태 (읽기 전용)")]
    [SyncVar(hook = nameof(OnGearChanged))]
    [SerializeField, ReadOnly] private int _currentGear = 0;
    [SerializeField, ReadOnly] private float _gearShiftingCooldown = 0;

    public int CurrentGear => _currentGear;

    protected override void Start()
    {
        base.Start();
        _moduleType = ModuleType.추진;
        
        InitializeUI();
        
        if (!isServer)
        {
            return;
        }
        
        InitializeModule();
    }

    private void Update()
    {
        if (IsOccupied)
        {
            UpdateGearInput();
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
    }

    /// <summary>
    /// 모듈 초기화
    /// </summary>
    private void InitializeModule()
    {
        _moduleController = GetComponent<ModuleController>();
        _moduleController.SetConnectedModule(this);
    }

    #region 기어 제어 메소드
    /// <summary>
    /// 클라이언트에서 입력을 받아 서버로 전송하여 함선의 기어를 설정합니다.
    /// </summary>
    [Command(requiresAuthority = false)]
    public void CmdSetGear(int gear)
    {
        _currentGear = gear;
        
        // ShipController에 기어 변경 알림
        if (_shipController != null)
        {
            _shipController.SetEngineGear(_currentGear);
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
                CmdSetGear(newGear);
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
    #endregion
}