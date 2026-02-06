using Mirror;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class InternalRangedAttackPerformState : InternalAttackState
{
    [Tooltip("던지기 파워")]
    [SerializeField] private float _throwPower = 5;
    [Tooltip("발사할 투사체 프리팹")]
    [SerializeField] private GameObject _projectilePrefab;
    [Tooltip("투사체가 생성될 위치")]
    [SerializeField] private Transform _projectileSpawnPoint;

    public override void Enter()
    {
        base.Enter();

        PerformAttack();
    }

    public override void Exit()
    {
        base.Exit();

        _internalController.AnimationController?.SetAttacking(false);
    }

    private void PerformAttack()
    {
        if (!_internalController.isServer)
        {
            return; // 서버에서만 실행
        }

        if (_projectilePrefab == null)
        {
            Debug.LogError("Projectile Prefab이 할당되지 않았습니다.");
            return;
        }

        // 서버에서 투사체 생성 및 스폰
        GameObject projectileInstance = Instantiate(_projectilePrefab, _projectileSpawnPoint.position, _projectileSpawnPoint.rotation);
        NetworkServer.Spawn(projectileInstance);
        // LTD오브젝트 이므로 자동 부모설정 됨 - 박상민
        // if (transform.parent != null)
        // {
        //     projectileInstance.GetComponent<NetworkParentSync>().SetNetworkParent(transform.parent);
        // }

        if (projectileInstance.TryGetComponent<LTDEnemyAttackObject>(out var projectileLTD))
        {
            projectileLTD.ServerThrownToward(_internalController.AimDirection * _throwPower, _internalController.netId, _internalController.TargetPos);
        }
    }
}
