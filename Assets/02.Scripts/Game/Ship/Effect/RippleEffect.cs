using UnityEngine;

public class RippleEffect : MonoBehaviour
{
    [Header("물결 효과 설정")]
    [SerializeField] private float _spreadSpeed;
    [SerializeField] private float _fadeDuration;
    [Header("물결 참조")]
    [SerializeField] private Transform _dotLeft;
    [SerializeField] private Transform _dotRight;
    [Header("물결 스프라이트 랜덤 스프라이트")]
    [SerializeField] private Sprite[] _randomSprites;

    private SpriteRenderer[] _spriteRenderers;
    private float _lifeTimer;
    private Vector3 _initialScale;
    private Vector3 _initialLeftLocalPosition;
    private Vector3 _initialRightLocalPosition;

    // 접근 프로퍼티
    public float SpreadSpeed
    {
        get => _spreadSpeed;
        set => _spreadSpeed = value;
    }
    public float FadeDuration
    {
        get => _fadeDuration;
        set => _fadeDuration = value;
    }

    void Awake()
    {
        _spriteRenderers = GetComponentsInChildren<SpriteRenderer>();
        _initialScale = transform.localScale;
        _initialLeftLocalPosition = _dotLeft.localPosition;
        _initialRightLocalPosition = _dotRight.localPosition;
    }

    /// <summary>
    /// 배마다 크기가 다르므로 리플의 초기 위치를 설정합니다.
    /// </summary>
    public void InitPosition(float distance)
    {
        // 초기 위치 설정
        _dotLeft.localPosition = new Vector3(-distance / 2, 0, 0);
        _dotRight.localPosition = new Vector3(distance / 2, 0, 0);
        _initialLeftLocalPosition = _dotLeft.localPosition;
        _initialRightLocalPosition = _dotRight.localPosition;
    }

    // 오브젝트 풀에서 활성화될 때마다 호출
    void OnEnable()
    {
        _lifeTimer = 0f;
        transform.localScale = _initialScale; // 크기 초기화
        _dotLeft.localPosition = _initialLeftLocalPosition; // 위치 초기화
        _dotRight.localPosition = _initialRightLocalPosition; // 위치 초기화
        // 모든 자식 스프라이트의 알파값 초기화
        foreach (var sr in _spriteRenderers)
        {
            //생성시 랜덤 스프라이트 설정
            if (_randomSprites.Length > 0)
            {
                sr.sprite = _randomSprites[Random.Range(0, _randomSprites.Length)];
            }
            //랜덤 로테이션 - 사용시 사방팔방으로 움직임
            //sr.transform.rotation = Quaternion.Euler(0, 0, Random.Range(0f, 360f));
            Color color = sr.color;
            color.a = 1f;
            sr.color = color;
            sr.transform.localScale = Vector3.one; // 크기 초기화
        }
    }

    void Update()
    {
        _lifeTimer += Time.deltaTime;

        // 점들이 점점 퍼져나감
        _dotLeft.Translate(_spreadSpeed * Time.deltaTime * Vector2.left, Space.Self);
        _dotRight.Translate(_spreadSpeed * Time.deltaTime * Vector2.right, Space.Self);

        // 크기가 커지는 기능
        // transform.localScale += 0.2f * Time.deltaTime * Vector3.one;

        // 점점 투명해지다가 비활성화
        if (_lifeTimer < _fadeDuration)
        {
            float alpha = 1.0f - (_lifeTimer / _fadeDuration);
            float scale = Mathf.Lerp(1.0f, 0.0f, _lifeTimer / _fadeDuration);
            foreach (var sr in _spriteRenderers)
            {
                Color color = sr.color;
                color.a = alpha;
                sr.color = color;

                sr.transform.localScale = scale * Vector3.one;
            }
        }
        else
        {
            // 풀 반납
            gameObject.SetActive(false);
        }
    }
}