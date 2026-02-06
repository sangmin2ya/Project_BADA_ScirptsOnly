using Mirror;
using UnityEngine;

public class CannonModuleBoxObject : ModuleBoxObject
{
    private int _loadedShellCount = 0;
    private Cannon _installedCannon;

    [Server]
    protected override Module InstallModule(ModulePoint modulePoint)
    {
        Module module = base.InstallModule(modulePoint);

        _installedCannon = module as Cannon;
        if (_installedCannon == null)
        {
            Debug.LogError("설치된 모듈이 Cannon 타입이 아닙니다.");
            return module;
        }
        _installedCannon.SetLoadedShellCount(_loadedShellCount);
        return module;
    }

    [Server]
    protected override void DetachModule()
    {
        _loadedShellCount = _installedCannon.LoadedShellCount;

        base.DetachModule();

        _installedCannon = null;
    }
}
