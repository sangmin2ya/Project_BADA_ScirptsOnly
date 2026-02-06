using Mirror;
using UnityEngine;

/// <summary>
/// LTD 오브젝트 그 자체가 되어버린 플레이어...
/// [박상민 - 2025.07.02]
/// </summary>
public class LTDPlayer : LTDObject
{
    private PlayerState _playerState;
    private Rigidbody2D _rigidbody2D;
    [SerializeField] private PhysicsMaterial2D _ltdPhysicsMaterial;

    #region IInteractable 구현

    private string InteractionText
    {
        get => IsBeingLifted ? "부활(홀드)" : "들기";
    }
    private string ActionName
    {
        get => IsBeingLifted ? "Player/Use" : "Player/Throw";
    }
    private bool IsInteractable
    {
        get
        {
            if (IsBeingLifted)
            {
                return true; // 들려있을 때는 항상 상호작용 가능
            }
            else
            {
                return IsAbleToLift; // 들려있지 않을 때는 들 수 있을 때만 상호작용 가능
            }
        }
    }

    #endregion

    protected override void Awake()
    {
        base.Awake();

        _playerState = GetComponent<PlayerState>();
        _rigidbody2D = GetComponent<Rigidbody2D>();

        _ltdObjectType = LTDObjectType.플레이어; // LTD 오브젝트 타입을 플레이어로 설정
        _ltdObjectSize = LTDObjectSize.소; // 크기는 임시로
    }

    void OnEnable()
    {
        _rigidbody2D.sharedMaterial = _ltdPhysicsMaterial;
    }

    void OnDisable()
    {
        _rigidbody2D.sharedMaterial = null;
    }

    public override bool Lifted(PlayerLTDController player)
    {
        _playerState.CarriedByOtherPlayer(true);

        return base.Lifted(player);
    }

    public override void Dropped()
    {
        _playerState.CarriedByOtherPlayer(false);
        base.Dropped();
    }

    public override void Thrown(ThrowData throwData, uint throwerNetId)
    {
        _playerState.CarriedByOtherPlayer(false); // 던지기 전에 들린 상태를 해제
        base.Thrown(throwData, throwerNetId);
    }
}