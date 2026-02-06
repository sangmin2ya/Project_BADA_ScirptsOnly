using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class SubmarineZoningState : ExternalZoningState
{
    [Header("잠수함 쿨타임")]
    [Tooltip("잠수하는 시간 시간")]
    [SerializeField] private float _diveTime = 5;
    [Tooltip("잠수 하기까지 해수면에서 대기하는 시간")]
    [SerializeField] private float _resurfaceTime = 5;

    private float _diveTimer = 0;

    [Header("컴포넌트 참조 (SubmarineZoningState)")]
    private SubmarineController _submarineController;
    private SubmarineModuleHolder _submarineModuleHolder;


    // 람다식 & 프로퍼티
    private bool _canDive => _submarineModuleHolder.CanDive;
    private bool _isDiving => _submarineController.IsSubmerged;

    public override void Setup(IEnemyController controller, EnemyStateMachine stateMachine)
    {
        base.Setup(controller, stateMachine);

        if (!_externalController.TryGetComponent<SubmarineController>(out _submarineController))
        {
            Debug.LogWarning($"<color=yellow>잠수함 오브젝트 {gameObject.name}에 SubmarineController 컴포넌트를 찾을 수 없습니다.</color>");
        }
        if (!_externalController.TryGetComponent<SubmarineModuleHolder>(out _submarineModuleHolder))
        {
            Debug.LogWarning($"<color=yellow>잠수함 오브젝트 {gameObject.name}에 SubmarineModuleHolder 컴포넌트를 찾을 수 없습니다.</color>");
        }

        _submarineModuleHolder.OnAllModuleDestroyed += HandleAllModuleDestroyed;
    }

    private void OnDestroy()
    {
        _submarineModuleHolder.OnAllModuleDestroyed -= HandleAllModuleDestroyed;
    }

    public override void StateUpdate()
    {
        base.StateUpdate();

        // 공기 탱크가 파괴되어 잠수가 불가능하다면 return
        if (!_canDive)
        {
            return;
        }

        _diveTimer -= Time.deltaTime;
        if (_diveTimer <= 0)
        {
            if (_isDiving)
            {
                // 해수면으로 상승 후 타이머 적용
                _diveTimer = _resurfaceTime;
                _submarineController.RPCChangeDive(false);
            }
            else
            {
                // 잠수 후 타이머 적용
                _diveTimer = _diveTime;
                _submarineController.RPCChangeDive(true);

                // 공격 쿨타임 시작
                // 쿨타임 종료 후 자동으로 발포
                _submarineController.CheckCannonsTarget();
            }
        }
    }

    public override void StatePhysicsUpdate()
    {
        if (_canDive)
        {
            // 잠수 중일 때 물리 업데이트
            base.StatePhysicsUpdate();
        }

    }

    /// <summary>
    /// 공기 모듈이 모두 파괴되었을 때, 현재 잠수 중인 상태라면 해수면으로 올라온다.
    /// </summary>
    private void HandleAllModuleDestroyed()
    {
        if (_isDiving)
        {
            _submarineController.RPCChangeDive(false);
        }
    }
}
