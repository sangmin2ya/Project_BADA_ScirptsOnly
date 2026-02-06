using Mirror;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SubmarineController : ExternalEnemyController
{
    [Header("잠수함")]
    [Tooltip("잠수 후 공격까지 대기 시간")]
    [SerializeField] private float _fireDelayTime = 2;

    private List<EnemyCannonModule> _cannons = new List<EnemyCannonModule>();

    private SubmarineDiveController _diveController;

    // 람다식 & 프로퍼티
    public bool IsSubmerged => _diveController.IsSubmerged;

    protected override void Awake()
    {
        base.Awake();

        if (!TryGetComponent<SubmarineDiveController>(out _diveController))
        {
            Debug.LogError($"잠수함 타입 적 오브젝트 {gameObject.name}에 SubmarineDiveController 컴포넌트가 없습니다.");
        }

        foreach (var item in _moduleHolder.Cannons)
        {
            _cannons.Add(item.Module.GetComponent<EnemyCannonModule>());
        }
    }

    /// <summary>
    /// 잠수함 함포 공격 준비
    /// </summary>
    public void CheckCannonsTarget()
    {
        foreach (var cannon in _cannons)
        {
            if (cannon == null) continue;
            cannon.CheckAndLockTarget();
            cannon.StartCooldown(_fireDelayTime);
        }
    }

    [ClientRpc]
    public void RPCChangeDive(bool willDive)
    {
        _diveController.DoDive(willDive);
    }
}
