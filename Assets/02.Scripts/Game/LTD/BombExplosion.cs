using Mirror;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 폭탄의 폭발 효과를 처리하는 클래스
/// [조우석 - 2025.08.12]
/// </summary>
public class BombExplosion : NetworkBehaviour
{
    [Tooltip("폭발 범위 콜라이더")]
    private Collider2D _explosionDetectCollider;
    [Tooltip("폭발 효과 콜라이더 활성화 시작 시간")]
    [SerializeField] private float _explosionDetectStartTime;
    [Tooltip("폭발 효과 콜라이더 비활성화 시작 시간")]
    [SerializeField] private float _explosionDetectEndTime;
    [Tooltip("폭발 효과 오브젝트 삭제 시간")]
    [SerializeField] private float _destructionTime;
    [Tooltip("폭발에 의한 넉백 세기")]
    [SerializeField] private float _explosionPower = 10;
    private float _timer = 0;

    private void Awake()
    {
        _explosionDetectCollider = GetComponent<Collider2D>();
    }

    private void OnTriggerEnter2D(Collider2D collider)
    {
        // 기본적으로 서버에서만 충돌을 처리
        if (!isServer)
        {
            return;
        }

        if (collider.TryGetComponent<PhysicsHandler>(out var physicsHandler))
        {
            Vector2 explosionAffectDirection = (collider.transform.position - transform.position).normalized;
            physicsHandler.ApplyVelocity(explosionAffectDirection * _explosionPower);
            if (physicsHandler.PhysicsObjectType == PhysicsObjectType.플레이어)
            {
                physicsHandler.GetComponent<PlayerState>().ApplyStun();
            }
        }
    }

    private void Update()
    {
        if (!isServer)
        {
            return;
        }

        _timer += Time.deltaTime;
        // 일정 시간이 지나면 폭발 오브젝트 삭제
        if (_timer > _destructionTime)
        {
            DoDestroy();
        }
        // _explosionDetectEndTime에 도달하면 폭발 범위 콜라이더를 비활성화
        else if (_timer > _explosionDetectEndTime && _timer < _destructionTime && _explosionDetectCollider.enabled)
        {
            _explosionDetectCollider.enabled = false;
        }
        // explosionDetectStartTime에 도달하면 폭발 범위 콜라이더가 활성화
        else if (_timer > _explosionDetectStartTime && _timer < _explosionDetectEndTime && !_explosionDetectCollider.enabled)
        {
            _explosionDetectCollider.enabled = true;
        }
    }

    /// <summary>
    /// LTD 오브젝트 파괴
    /// </summary>
    protected virtual void DoDestroy()
    {
        NetworkServer.Destroy(gameObject); // 오브젝트 제거
    }
}
