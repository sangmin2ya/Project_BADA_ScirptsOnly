using Mirror;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SubmarineModuleHolder : EnemyModuleHolder
{
    [Tooltip("적 함에 달려있는 공기 탱크 모듈")]
    public List<SubmarineModuleSlot> Tanks = new List<SubmarineModuleSlot>();

    [Tooltip("잠수함이 잠수 가능한가? 공기 탱크 모두 파괴 시 false")]
    private bool _canDive = true;

    public event Action OnAllModuleDestroyed;

    public bool CanDive { get { return _canDive; }}

    protected override void Awake()
    {
        base.Awake();

        for (int i = 0; i < Tanks.Count; i++)
        {
            Tanks[i].SubmarineModule.Initialize(this, i);
        }
    }

    /// <summary>
    /// 한 종류의 모듈이 모두 파괴되었는지 체크
    /// </summary>
    /// <param name="modules">잠수함 모듈 List</param>
    /// <returns>true = 모든 모듈 파괴, false = 파괴되지 않은 모듈 존재</returns>
    protected bool CheckAllModuleDestroyed(List<SubmarineModuleSlot> modules)
    {
        for (int i = 0; i < modules.Count; i++)
        {
            if (!modules[i].IsDestroyed)
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>
    /// SERVER : 공기 탱크 모듈이 파괴되었을 시 호출
    /// </summary>
    /// <param name="index"></param>
    [Server]
    public void OnTankDestroyed(int index)
    {
        if (index < 0 || index >= Tanks.Count)
        {
            Debug.LogWarning($"{this.name} 공기 탱크 모듈 리스트에 옳지않은 인덱스의 제거 요청이 들어옴");
            return;
        }

        if (!Tanks[index].IsDestroyed)
        {
            Tanks[index].IsDestroyed = true;
            RpcDestroyModule(Tanks[index].SubmarineModule);
        }
        else
        {
            Debug.LogWarning($"{this.name} 이미 제거된 공기 탱크에 대해서 제거 요청이 들어옴");
        }

        // 잠수함의 공기탬크가 하나라도 남아있다면 잠수 가능
        bool allModuleDestroyed = CheckAllModuleDestroyed(Tanks);
        if (_canDive && allModuleDestroyed)
        {
            OnAllModuleDestroyed?.Invoke();
        }
        _canDive = !allModuleDestroyed;
    }

    [Serializable]
    public class SubmarineModuleSlot : EnemyModuleSlot
    {
        public SubmarineAirTankModule SubmarineModule;
    }
    
}
