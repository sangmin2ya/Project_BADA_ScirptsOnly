using Mirror;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 적 함선의 모듈 정보를 관리하는 클래스
/// [조우석 - 2025.07.11.]
/// </summary>
public class EnemyModuleHolder : NetworkBehaviour
{
    [Header("Modules (직접 인스펙터 창에서 연결 필요) ")]
    [Tooltip("적 함에 달려있는 함포 모듈")]
    public List<EnemyModuleSlot> Cannons = new List<EnemyModuleSlot>();
    [Tooltip("적 함에 달려있는 엔진 모듈")]
    public List<EnemyModuleSlot> Engines = new List<EnemyModuleSlot>();

    [Tooltip("함포가 모두 파괴되어 무장해제가 된 상태인가")]
    protected bool _isDisarmed = false;
    [Tooltip("파괴된 엔진의 비율(0 = 정상, 1 = 모두 파괴)")]
    protected float _engineDestructionRatio = 0;

    protected ExternalEnemyController _enemyController;

    public bool IsDisarmed { get { return _isDisarmed; } }
    public float EngineDestructionRatio { get { return _engineDestructionRatio; } }

    protected virtual void Awake()
    {
        _enemyController = GetComponent<ExternalEnemyController>();
        for (int i = 0; i < Cannons.Count; i++)
        {
            Cannons[i].Module.Initialize(this, i, _enemyController);
        }

        for (int i = 0; i < Engines.Count; i++)
        {
            Engines[i].Module.Initialize(this, i);
        }
    }

    /// <summary>
    /// 한 종류의 모듈이 모두 파괴되었는지 체크
    /// </summary>
    /// <param name="modules">해당하는 모듈 'EnemyModuleSlot' List</param>
    /// <returns>true = 모든 모듈 파괴, false = 파괴되지 않은 모듈 존재</returns>
    protected bool CheckAllModuleDestroyed(List<EnemyModuleSlot> modules)
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
    /// 한 종류의 모듈이 파괴된 비율이 어느 정도인지 확인
    /// </summary>
    /// <param name="modules"></param>
    /// <returns></returns>
    protected float CheckModuleDestroyRatio(List<EnemyModuleSlot> modules)
    {
        if (modules.Count < 0)
        {
            Debug.LogWarning($"{this.gameObject} 적함선의 엔진모듈 계수를 0으로 나눠 계산 중.");
            return 0f;
        }

        int destroyCount = 0;
        for (int i = 0; i < modules.Count; i++)
        {
            if (modules[i].IsDestroyed)
            {
                destroyCount++;
            }
        }
        return Mathf.Clamp01((float)destroyCount / (float)modules.Count);
    }

    #region 네트워크

    /// <summary>
    /// SERVER : 함포 모듈이 파괴되었을 시 호출
    /// </summary>
    /// <param name="index"></param>
    [Server]
    public void OnCannonDestroyed(int index)
    {
        if (index < 0 || index >= Cannons.Count)
        {
            Debug.LogWarning($"{this.name} 캐논 모듈 리스트에 옳지않은 인덱스의 제거 요청이 들어옴");
            return;
        }

        if (!Cannons[index].IsDestroyed)
        {
            Cannons[index].IsDestroyed = true;
            RpcDestroyModule(Cannons[index].Module);
        }
        else
        {
            Debug.LogWarning($"{this.name} 이미 제거된 캐논에 대해서 제거 요청이 들어옴");
        }

        // 적 함선이 공격 불능 상태인지 확인
        _isDisarmed = CheckAllModuleDestroyed(Cannons);
    }

    /// <summary>
    /// SERVER : 엔진 모듈이 파괴되었을 시 호출
    /// </summary>
    /// <param name="index"></param>
    [Server]
    public void OnEngineDestroyed(int index)
    {
        if (index < 0 || index >= Engines.Count)
        {
            Debug.LogWarning($"{this.name} 엔진 모듈 리스트에 옳지않은 인덱스의 제거 요청이 들어옴");
            return;
        }

        if (!Engines[index].IsDestroyed)
        {
            Engines[index].IsDestroyed = true;
            RpcDestroyModule(Engines[index].Module);
        }
        else
        {
            Debug.LogWarning($"{this.name} 이미 제거된 엔진에 대해서 제거 요청이 들어옴");
        }

        // 적 함선이 이동 불능 상태인지 확인
        _engineDestructionRatio = CheckModuleDestroyRatio(Engines);
    }

    /// <summary>
    /// 클라이언트들에서 해당하는 모듈 오브젝트를 제거
    /// </summary>
    /// <param name="module"></param>
    [ClientRpc]
    protected void RpcDestroyModule(EnemyModuleController module)
    {
        Debug.Log($"Client RPC - {this.name} {module.name} 모듈 Destroy");
        GameObject.Destroy(module.gameObject);
    }

    #endregion

    /// <summary>
    /// 모듈 관리용 클래스
    /// </summary>
    [Serializable]
    public class EnemyModuleSlot
    {
        public EnemyModuleController Module;
        public bool IsDestroyed;
    }
}