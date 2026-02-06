using System.Collections;
using Mirror;
using UnityEngine;

public class RepairPoint : NetworkBehaviour
{
    [Header("수리지점 설정")]
    [Tooltip("수리지점 당 수리 횟수")]
    [SerializeField] private int _repairCount = 1;
    [Tooltip("회복량")]
    [SerializeField] private float _repairAmount = 10f;

    [Header("적 스폰 조건")]
    [Tooltip("스폰 체크 주기")]
    [SerializeField] private float _spawnCheckCycle = 10f;
    [Tooltip("스폰 확률")]
    [SerializeField] private float _spawnProbability = 0.3f;

    private GameObject _spawnedEnemy;
    [SyncVar(hook = nameof(OnCurrentCountChanged))] private int _currentCount;
    private ShipController _shipController;
    private SpriteRenderer _spriteRenderer;
    private Transform _holePoint;

    public bool IsEnemySpawned => _spawnedEnemy != null;

    void Awake()
    {
        _spriteRenderer = GetComponent<SpriteRenderer>();
    }
    void Start()
    {
        if (!isServer) return;

        _currentCount = 0;

        //적 스폰 코루틴 시작
        StartCoroutine(SpawnEnemyRoutine());
    }

    /// <summary>
    /// 생성해주고 초기화
    /// </summary>
    public void Init(ShipController shipController, float repairAmount, Transform holePivot)
    {
        _shipController = shipController;
        _currentCount = 0;
        _repairAmount = repairAmount;
        _holePoint = holePivot;
    }

    /// <summary>
    /// 수리를 하고, 수리필요치에 달성하면 true 반환
    /// </summary>
    public bool Repair()
    {
        _currentCount++;
        if (_currentCount >= _repairCount)
        {
            _shipController.Repair(_repairAmount, _holePoint, this);
            return true;
        }
        return false;
    }

    private void OnCurrentCountChanged(int oldCount, int newCount)
    {
        //일단은 투명화
        float alpha = 1f - ((float)newCount / _repairCount);
        Color color = _spriteRenderer.color;
        color.a = alpha;
        _spriteRenderer.color = color;
    }

    IEnumerator SpawnEnemyRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(_spawnCheckCycle);
            //이미 적이 스폰되어있으면 패스
            if (_spawnedEnemy != null || !_shipController.IsAbleToSpawnEnemy)
            {
                continue;
            }
            if (Random.value < _spawnProbability)
            {
                //적 스폰 요청
                _spawnedEnemy = NetworkObjectSpawner.Instance.RequestSpawnEnemyCharacter(EnemyCharacterType.케넨, transform.position, Quaternion.identity);
            }
        }
    }
}
