using Mirror;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 내부 적의 물리적인 상호작용을 관리하는 클래스
/// [조우석 - 2025.08.12.]
/// </summary>
public class InternalEnemyPhysicsHandler : PhysicsHandler
{
    private InternalEnemyStunHandler _enemyStunHandler;
    private InternalEnemyHealthController _healthController;

    protected override void Awake()
    {
        base.Awake();

        if (!TryGetComponent<InternalEnemyStunHandler>(out _enemyStunHandler))
        {
            Debug.LogError($"내부 적 {this.gameObject}의 InternalEnemyStunHandler를 찾을 수 없습니다.");
        }
        if (!TryGetComponent<InternalEnemyHealthController>(out _healthController))
        {
            Debug.LogError($"내부 적 {this.gameObject}의 InternalEnemyHealthController를 찾을 수 없습니다.");
        }
    }

    protected override void OnCollisionEnter2D(Collision2D collision)
    {
        base.OnCollisionEnter2D(collision);

        // 기본적으로 서버에서만 충돌을 처리
        if (!isServer)
        {
            return;
        }

        if (collision.collider.TryGetComponent<LTDObject>(out var otherLTD))
        {
            // 본인이 던진 오브젝트와 충돌한 경우, 스턴 로직을 무시
            if (otherLTD.ThrowerNetId != 0 && otherLTD.ThrowerNetId == this.netId)
            {
                return;
            }

            // 오브젝트가 적에게 부딪혔으므로,
            // throwerId를 초기화하여 튕겨나간 오브젝트가 원래 던진 플레이어를 스턴 가능하도록 수정
            if (otherLTD.ThrowerNetId != 0)
            {
                otherLTD.ClearThrowerId();
            }

            // 스턴 여부 최종 결정
            if (otherLTD.IsStunnable)
            {
                ServerReportCollision(otherLTD.netId, true);
            }
        }
        else if (collision.collider != null)
        {
            if (this.CanStunBySpeed)
            {
                // 벽에 던져졌을 때 피격
                _enemyStunHandler.TakeDamage();
                // @@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@
                // 임시: 추후 데미지 정확한 값으로 지정 필요
                _healthController.TakeDamage(10);
            }
        }
    }

    /// <summary>
    /// 플레이어 충돌 시 서버에서 충돌 처리를 동작하는 메서드
    /// - 스턴 처리 확인
    /// </summary>
    /// <param name="otherId"></param>
    /// <param name="isStunnable"></param>
    [Server]
    private void ServerReportCollision(uint otherId, bool isStunnable)
    {
        if (!isStunnable)
        {
            return;
        }

        if (!NetworkServer.spawned.TryGetValue(otherId, out var otherObjectIdentity))
        {
            return;
        }

        if (otherObjectIdentity.TryGetComponent<LTDObject>(out var otherLTD))
        {
            _enemyStunHandler.TakeDamage();
            // @@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@
            // 임시: 추후 데미지 정확한 값으로 지정 필요
            _healthController.TakeDamage(2);
        }
    }
}
