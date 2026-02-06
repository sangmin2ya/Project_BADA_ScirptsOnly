using MoreMountains.Feedbacks;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

/// <summary>
/// 재장전을 위해서 탄환의 충돌을 감지하는 클래스
/// [조우석 - 2025.07.30.]
/// </summary>
public class CannonReloadSpot : MonoBehaviour
{
    private Cannon _linkedCannon = null;
    private ModuleController _moduleController;

    private void Awake()
    {
        if (_moduleController == null)
        {
            _moduleController = GetComponentInParent<ModuleController>();
        }
    }

    /// <summary>
    /// 함포에 재장전이 가능한지 확인하는 메서드
    /// </summary>
    /// <returns></returns>
    public bool CheckCanReload()
    {
        if (GetCannonModule() == null)
        {
            Debug.LogWarning("연결된 함포 모듈이 없음");
            return false;
        }

        if (_linkedCannon.IsShellFull)
        {
            Debug.Log($"{_linkedCannon.name} 함포가 가득 차 더 장전할 수 없음");
            return false;
        }
        
        return true;
    }

    private Cannon GetCannonModule()
    {
        if (_linkedCannon == null)
        {
            _linkedCannon = _moduleController.ConnectedModule as Cannon;
        }
        return _linkedCannon;
    }

    /// <summary>
    /// 함포에 재장전을 시도하는 메서드
    /// </summary>
    public void TryReload()
    {
        GetCannonModule()?.ReloadShell();
    }
}
