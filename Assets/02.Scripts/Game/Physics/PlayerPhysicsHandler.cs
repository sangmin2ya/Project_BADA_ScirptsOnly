using Mirror;
using System.Collections;
using System.Collections.Generic;
using System.Security.Cryptography;
using UnityEngine;

/// <summary>
/// 플레이어의 물리적인 추가적인 처리를 담당하는 클래스
/// [조우석 - 2025.07.24.]
/// </summary>
public class PlayerPhysicsHandler : PhysicsHandler
{
    private PlayerState _playerState;

    protected override void Awake()
    {
        base.Awake();
        if (_playerState == null)
        {
            _playerState = GetComponent<PlayerState>();
        }
    }

    protected override void Start()
    {
        // 플레이어는 IgnoreCollision으로 설정하지 않기 위해서 빈 메서드로 Override
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
            // 내가 던진 오브젝트와 충돌한 경우, 스턴 로직을 무시
            if (otherLTD.ThrowerNetId != 0 && otherLTD.ThrowerNetId == this.netId)
            {
                return;
            }

            // 오브젝트가 플레이어에게 부딪혔으므로,
            // throwerId를 초기화하여 튕겨나간 오브젝트가 원래 던진 플레이어를 스턴 가능하도록 수정
            if (otherLTD.ThrowerNetId != 0)
            {
                otherLTD.ClearThrowerId();
            }
            Debug.Log($"플레이어가 {otherLTD.name}과(와) 충돌함, 충돌 가능여부 {otherLTD.IsStunnable}");
            // 스턴 여부 최종 결정
            if (otherLTD.IsStunnable)
            {
                _playerState.ApplyStun();
                Debug.Log($"<color=red>플레이어가 스턴 당함! by {otherLTD.name}</color>");
            }
        }
    }
}
