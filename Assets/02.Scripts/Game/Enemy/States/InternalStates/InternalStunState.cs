using UnityEngine;

/// <summary>
/// 내부 적이 스턴되었을 때의 상태
/// [조우석 - 2025.08.14.]
/// </summary>
public class InternalStunState : InternalBaseState
{
    [Tooltip("스턴이 지속될 시간")]
    [SerializeField] private float _stunTime = 3;
    private InternalEnemyStunHandler _stunHandler;
    private float _stunTimer;

    public override void Setup(IEnemyController controller, EnemyStateMachine stateMachine)
    {
        base.Setup(controller, stateMachine);
        // StunHandler에서 스턴 시간 정보를 가져오기 위해 참조를 얻습니다.
        _stunHandler = controller.gameObject.GetComponent<InternalEnemyStunHandler>();
    }

    public override void Enter()
    {
        base.Enter();
        Debug.Log($"[{_controller.gameObject.name}] Entering Stun State.");

        _stunTimer = _stunTime;

        // 이미 Controller의 ApplyStun에서 애니메이션 처리 중
    }

    public override void StateUpdate()
    {
        if (_stunHandler == null)
        {
            return;
        }

        if (_stunHandler.IsStunTimerPaused)
        {
            return;
        }

        // 스턴 타이머 감소
        _stunTimer -= Time.deltaTime;
        if (_stunTimer <= 0)
        {
            // 시간이 다 되면 StunHandler에게 회복을 명령
            _stunHandler.RecoverFromStun();
        }
    }

    public override void Exit()
    {
        base.Exit();
        Debug.Log($"[{_controller.gameObject.name}] Exiting Stun State.");
    }
}