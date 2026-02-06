using UnityEngine;
using Mirror;
/// <summary>
/// 함선의 잔물결 효과를 제어하는 클래스
/// </summary>
public class ShipRippleController : MonoBehaviour
{
    [SerializeField] private Transform _rippleSpawnPoint;
    [SerializeField] private float _rippleInterval = 2f * Mathf.PI / 9.81f;
    [SerializeField] private float _rippleDistance = 5f;//리플 생성 지점끼리의 거리
    [SerializeField] private float _rippleSpreadSpeed = 0.354f; // 고정된 퍼짐 속도
    [SerializeField] private float _rippleMinimumFadeDuration = 0.8f; // 최소 지속 시간
    [SerializeField] private float _rippleMaximumFadeDuration = 1.5f; // 기본 지속 시간
    [SerializeField] private float _rippleFadeDuration = 1.0f; // 기본 지속 시간
    private float _rippleTimer;
    [SerializeField] private ShipController _shipController;
    [SerializeField] private EnemyControllerBase _enemyController;

    void Start()
    {
        _shipController = ShipSpawner.Instance?.CurrentShip.GetComponent<ShipController>();
        _enemyController = GetComponent<EnemyControllerBase>();
    }

    void Update()
    {
        if (_shipController != null && _shipController.CurrentSpeed > 0f)
        {
            _rippleInterval = 2f * Mathf.PI / _shipController.CurrentSpeed; // 속도에 따라 간격 조정
            _rippleTimer += Time.deltaTime;
            if (_rippleTimer > _rippleInterval * 0.1f)
            {
                _rippleTimer = 0f;
                SpawnRipple();
            }
        }
        else if (_enemyController != null && _enemyController.CurrentSpeed > 0f)
        {
            _rippleInterval = 2f * Mathf.PI / (_enemyController.CurrentSpeed * _enemyController.CurrentSpeed); // 속도에 따라 간격 조정
            _rippleTimer += Time.deltaTime;
            if (_rippleTimer > _rippleInterval * 0.1f)
            {
                _rippleTimer = 0f;
                SpawnRipple();
            }
        }
    }

    void SpawnRipple()
    {
        // 오브젝트 풀러에서 잔물결 오브젝트를 가져옵니다.
        GameObject ripple = RippleEffectObjectPooler.Instance.GetPooledObject();
        if (ripple != null)
        {
            RippleEffect rippleEffect = ripple.GetComponent<RippleEffect>();
            ripple.transform.position = _rippleSpawnPoint.position;
            ripple.transform.rotation = _rippleSpawnPoint.rotation;
            rippleEffect.InitPosition(_rippleDistance);
            rippleEffect.SpreadSpeed = _rippleSpreadSpeed * _shipController.CurrentSpeed; // 속도에 따라 퍼짐 속도 조정
            rippleEffect.FadeDuration = Mathf.Clamp(_rippleFadeDuration * Mathf.Log(_shipController.CurrentSpeed), _rippleMinimumFadeDuration, _rippleMaximumFadeDuration);
            ripple.SetActive(true); // OnEnable 함수 호출
        }
    }
}
