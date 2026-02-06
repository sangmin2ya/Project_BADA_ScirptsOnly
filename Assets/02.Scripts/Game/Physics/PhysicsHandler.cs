using Mirror;
using Mirror.BouncyCastle.Asn1.Cmp;
using Mirror.Examples.Tanks;
using System.Collections;
using System.Collections.Generic;
using System.Security.Authentication.ExtendedProtection;
using UnityEngine;

/// <summary>
/// 리지드바디가 있는 오브젝트에 충돌 처리를 서버에거 주관하도록 설정하는 클래스
/// [조우석 - 2025.07.22.]
/// </summary>
public class PhysicsHandler : NetworkBehaviour
{
    [SerializeField] protected Rigidbody2D _rigidbody;

    [Tooltip("오브젝트의 타입")]
    [SerializeField] protected PhysicsObjectType _physicsObjectType = PhysicsObjectType.없음;
    [Tooltip("LTD 오브젝트인가")]
    [SerializeField] protected bool _isLTDObject = true;
    [Tooltip("스턴이라고 판단할 속력")]
    [SerializeField] protected float _minStunSpeed = 10f;

    [Header("Speed Relative")]
    [Tooltip("최소 저항을 적용할 속력 하한값. 속력이 이 값 이상이라면 최소 저항 적용")]
    [SerializeField] protected float _minDragSpeedThreshold = 12;
    [Tooltip("최대 저항을 적용할 속력 상한값. 속력이 이 값 이하라면 최대 저항 적용")]
    [SerializeField] protected float _maxDragSpeedThreshold = 1;
    [Tooltip("일반 상태 공기 저항 값")]
    [SerializeField] protected float _minDrag = 2.5f;
    [Tooltip("저속 상태일 때 공기 저항 값")]
    [SerializeField] protected float _maxDrag = 20;
    [Tooltip("저속인 오브젝트를 정지시키기 위한 임계 속도")]
    protected float _minSpeedThreshold = 0.01f;

    [Header("Special Collision")]
    [Tooltip("충돌 당할 시 작용에 의해 튕겨날 Ratio. 충돌 당했을 때 충격을 가한 물체 속도의 몇 배의 속도로 튕겨날 것인가")]
    [SerializeField, Range(0, 1)] protected float _collisionPushedRatio = 0.4f;
    [Tooltip("충돌 시 반작용에 의해 감소하는 속도 Ratio. 해당 오브젝트가 어딘가에 충돌했을 때 속도가 몇 배로 감소할 것인가")]
    [SerializeField, Range(0, 1)] protected float _collisionBounceRatio = 0.8f;

    protected Vector2 _prevFrameVelocity = Vector2.zero;

    // LTD 오브젝트일 때에만 스턴이 가능한 오브젝트인지 '서버에서' 판단
    public bool CanStunBySpeed => (_prevFrameVelocity.magnitude > _minStunSpeed) && isServer && _isLTDObject;
    public PhysicsObjectType PhysicsObjectType => _physicsObjectType;
    public Vector2 PrevFrameVelocity => _prevFrameVelocity;
    public float CollisionPushedRatio => _collisionPushedRatio;
    public float CollisionBounceRatio => _collisionBounceRatio;

    protected virtual void Awake()
    {
        if (_rigidbody == null)
        {
            _rigidbody = GetComponent<Rigidbody2D>();
        }
    }

    // Start is called before the first frame update
    protected virtual void Start()
    {
        if (!isServer)
        {
            gameObject.layer = LayerMask.NameToLayer("Ignore Collision");
        }
    }

    protected virtual void OnCollisionEnter2D(Collision2D collision)
    {
        /*
        - Dynamic 오브젝트에도 벽에 탄성충돌한 것처럼 강하게 튕겨나오도록 하고 싶다
        1. 충돌한 대상이 rb가 없으면 비탄성 충돌을 한다.
        2. 충돌한 대상이 rb가 있으면 충돌 직전 속도 비교를 통해서 충돌 가해자를 설정한다.
        3. 가해자는 충돌한 접점에 대해서 반사각으로 튕겨나온다. 이 때에 에너지의 손실이 조금 일어난 것처럼 튕겨난다.
        4. 피해자는 법선벡터 방향으로 튕겨저 나간다. 이 때에 너무 크게 튕겨나지 않도록 한다.
         */

        // 서버에서만 충돌 처리
        if (!isServer)
        {
            return;
        }

        /* 
        충돌에 영향을 받는 물체와 충돌하면 특수한 충돌을하도록 한다
        - 충돌을 일으킨 물체(Projectile: 가해자)는 반사각으로 크게 튕겨나 대략적으로 되돌아오는 것처럼 연출된다
        - 충돌 당한 물체(Target: 피해자)는 충돌 법선 방향으로 적당히 튕겨나간다
        - 가해자/피해자 기준 : 두 물체 중 속력이 더 큰 물체가 가해자, 작은 물체가 피해자
         */
        if (collision.collider.TryGetComponent<PhysicsHandler>(out PhysicsHandler otherPhysicsHandler))
        {
            if (collision.rigidbody == null)
            {
                Debug.LogError("충돌한 PhysicsHandler한 오브젝트에 Rigidbody가 없습니다");
                return;
            }

            // 중복 계산 방지 로직
            // 두 객체의 인스턴스 ID를 비교하여, ID가 더 작은 쪽에서만 계산을 실행
            if (this.GetInstanceID() < otherPhysicsHandler.GetInstanceID())
            {
                return; // 내 ID가 더 작으면, 상대방이 계산하도록 나는 여기서 빠져나간다
            }

            Rigidbody2D targetRb;
            Rigidbody2D projectileRb;
            PhysicsHandler targetPhysicsHandler;
            PhysicsHandler projectilePhysicsHandler;
            Vector2 projectileVelocity;

            if (this._prevFrameVelocity.magnitude > otherPhysicsHandler.PrevFrameVelocity.magnitude)
            {
                projectileRb = _rigidbody;
                targetRb = collision.rigidbody;
                projectilePhysicsHandler = this;
                targetPhysicsHandler = otherPhysicsHandler;
                projectileVelocity = this._prevFrameVelocity;
            }
            else
            {
                targetRb = _rigidbody;
                projectileRb = collision.rigidbody;
                targetPhysicsHandler = this;
                projectilePhysicsHandler = otherPhysicsHandler;
                projectileVelocity = otherPhysicsHandler.PrevFrameVelocity;
            }

            Vector2 normal = collision.GetContact(0).normal;

            Vector2 pushDirection = Vector2.Angle(projectileVelocity, normal) < 90 ? normal : -normal;
            targetRb.velocity = pushDirection * projectileVelocity.magnitude * targetPhysicsHandler.CollisionPushedRatio;

            Vector2 reflectedVelocity = Vector2.Reflect(projectileVelocity, normal);
            projectileRb.velocity = reflectedVelocity * projectilePhysicsHandler.CollisionBounceRatio;
        }
        // 벽에 충돌한다면 가해자와 동일하게 비탄성충돌을 한다
        else
        {
            Vector2 normal = collision.GetContact(0).normal;
            Vector2 reflectedVelocity = Vector2.Reflect(_prevFrameVelocity, normal);

            _rigidbody.velocity = reflectedVelocity * _collisionBounceRatio;
        }
    }

    protected virtual void FixedUpdate()
    {
        _prevFrameVelocity = _rigidbody.velocity;

        // 서버에서만 물리 연산 동작
        if (!isServer)
        {
            return;
        }

        // 일단은 LTD 오브젝트가 아니라면 Return;
        if (!_isLTDObject)
        {
            return;
        }

        // SetDrag();

        // // 일정 속력 이하가 되면 정지
        // if (_rigidbody.velocity.sqrMagnitude < _minSpeedThreshold * _minSpeedThreshold
        //     && _rigidbody.velocity != Vector2.zero)
        // {
        //     ResetVelocity();
        // }
    }

    protected virtual void SetDrag()
    {
        // 저속일 시 더 강한 공기저항을 주어 너무 미끄러지지 않도록 한다
        float t = Mathf.InverseLerp(_minDragSpeedThreshold, _maxDragSpeedThreshold, _rigidbody.velocity.magnitude); // 1 → 0 으로 맵핑
        float dynamicDrag = Mathf.Lerp(_minDrag, _maxDrag, t);

        if (Mathf.Abs(_rigidbody.drag - dynamicDrag) > 0.001f) // 약간의 오차 허용
        {
            _rigidbody.drag = dynamicDrag;
        }
    }

    public void ApplyVelocity(Vector2 velocity)
    {
        _rigidbody.velocity = velocity;
    }

    /// <summary>
    /// 해당 오브젝트의 힘, 속도, 토크, 각속도 값을 0으로 초기화하는 메서드
    /// </summary>
    public void ResetPhysicsState()
    {
        ResetVelocity();
        ResetForceAndTorque();
    }

    public void ResetVelocity()
    {
        _rigidbody.velocity = Vector2.zero;
        _rigidbody.angularVelocity = 0;
    }

    public void ResetForceAndTorque()
    {
        _rigidbody.totalForce = Vector2.zero;
        _rigidbody.totalTorque = 0;
    }

    /// <summary>
    /// 플레이어가 기절하면 LTD로 변경용
    /// </summary>
    public void SetIsLTDObject(bool isLTD)
    {
        _isLTDObject = isLTD;
    }
}
