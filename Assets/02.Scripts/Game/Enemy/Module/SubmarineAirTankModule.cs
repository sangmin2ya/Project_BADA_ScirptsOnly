using Mirror;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SubmarineAirTankModule : EnemyModuleController
{
    protected SubmarineModuleHolder _submarineModuleHolder;

    /// <summary>
    /// 데이터 초기화
    /// - 잠수함 모듈 컨트롤러를 추가로 연결
    /// </summary>
    /// <param name="holder"></param>
    /// <param name="index"></param>
    public void Initialize(SubmarineModuleHolder holder, int index)
    {
        _moduleHolder = holder;
        _submarineModuleHolder = holder;
        _index = index;
    }

    [Server]
    public override void SetModuleDestroy()
    {
        _submarineModuleHolder.OnTankDestroyed(_index);
    }
}
