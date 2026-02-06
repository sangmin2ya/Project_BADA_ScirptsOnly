using Mirror;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// 폭탄 LTD 오브젝트
/// [조우석 - 2025.08.12.]
/// </summary>
public class LTDBomb : LTDEnemyAttackObject
{
    [Header("Bomb")]
    [Tooltip("일정 시간(_despawnTimer)이 지난 후 폭발할 것인가")]
    [SerializeField] private bool _isExplosive = false;
    [Tooltip("폭발 효과 오브젝트")]
    [SerializeField] private GameObject _explosionEffect;

    protected override void Update()
    {
        // 터지지 않는다면 LTDEnemyAttackObject 처럼 동작
        if (!_isExplosive)
        {
            base.Update();
            return;
        }

        // 서버에서만 타이머 확인
        if (!isServer)
        {
            return;
        }

        // 일정 시간 지난 후 폭파
        _despawnTimer += Time.deltaTime;
        if (_despawnTimer > _despawnTime)
        {
            DoDestroy();
        }
    }

    /// <summary>
    /// 오브젝트 파괴 메서드
    /// - 파괴하기 전에 폭발을 일으킴
    /// </summary>
    protected override void DoDestroy()
    {
        if (_isExplosive)
        {
            Explosion();
        }

        base.DoDestroy();
    }

    /// <summary>
    /// 폭발 오브젝트 소환 메서드
    /// </summary>
    private void Explosion()
    {
        if (!isServer)
        {
            return; // 서버에서만 실행
        }

        if (_explosionEffect == null)
        {
            Debug.LogError("_explosionEffect이 할당되지 않았습니다.");
            return;
        }

        // 서버에서 투사체 생성 및 스폰
        NetworkObjectSpawner.Instance.RequestSpawnObjectAtPosition(_explosionEffect, transform.position, transform.rotation);
    }
}
