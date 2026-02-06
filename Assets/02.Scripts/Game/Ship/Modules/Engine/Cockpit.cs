using UnityEngine;
using Mirror;

/// <summary>
/// 조향 모듈 - 함선의 회전 입력을 전달하는 모듈입니다.
/// [박상민 - 2025.06.27]
/// </summary>
[RequireComponent(typeof(ModuleController))]
public class Cockpit : Module
{
    [Header("함선 설정")]
    [Tooltip("회전 입력 감도")]
    [SerializeField] private float _rotationSensitivity = 1f;

    [Header("함선 상태 (읽기 전용)")]
    [SyncVar]
    [SerializeField, ReadOnly] private float _currentAngularInput = 0f;
    public float CurrentAngularInput => _currentAngularInput;

    protected override void Start()
    {
        base.Start();

        _moduleType = ModuleType.조향;

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
            UpdateRotationInput();
        }
        else
        {
            // 조작하지 않을 때는 회전 입력을 0으로 설정
            if (_currentAngularInput != 0f)
            {
                CmdSetAngularInput(0f);
            }
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
    [Command(requiresAuthority = false)]
    public void CmdSetAngularInput(float angularInput)
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
        float horizontalInput = Mathf.Sign(_rotationInput.x) * _rotationSensitivity;

        // 입력값이 변경되었을 때만 서버에 전송
        if (Mathf.Abs(horizontalInput - _currentAngularInput) > 0.01f)
        {
            Debug.Log(horizontalInput);
            CmdSetAngularInput(horizontalInput);
        }
    }
    #endregion
}