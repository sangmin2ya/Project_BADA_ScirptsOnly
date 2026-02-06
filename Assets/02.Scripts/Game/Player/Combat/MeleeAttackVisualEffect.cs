using UnityEngine;
using System.Collections;

/// <summary>
/// 근접 공격 시각 이펙트 관리
/// [작성자 - 2025.11.13]
/// </summary>
public class MeleeAttackVisualEffect : MonoBehaviour
{
    #region SerializeField

    [Header("슬래시 마크 설정")]
    [SerializeField] private GameObject _slashMarkPrefab;
    [SerializeField] private float _slashMarkDuration = 0.15f;
    [SerializeField] private float _slashMarkScale = 1.2f;
    [SerializeField] private float _slashMarkOffset = 0.8f;  // 플레이어로부터의 거리
    [SerializeField] private Color _slashColor1 = new Color(1f, 1f, 1f, 0.8f);  // 1타 색상
    [SerializeField] private Color _slashColor2 = new Color(1f, 0.9f, 0.9f, 0.8f);  // 2타 색상

    [Header("무기 연출 설정")]
    [SerializeField] private GameObject _weaponStickPrefab;
    [SerializeField] private float _weaponStickLength = 1.5f;  // 막대기 길이
    [SerializeField] private float _weaponSwingDuration = 0.3f;  // 휘두르는 시간
    [SerializeField] private float _weaponArcAngle = 120f;  // 휘두르는 각도
    [SerializeField] private Color _weaponColor = new Color(0.8f, 0.8f, 0.8f, 0.6f);  // 막대기 색상

    [Header("타격 이펙트 설정")]
    [SerializeField] private GameObject _hitFlashPrefab;
    [SerializeField] private GameObject _shockwaveRingPrefab;
    [SerializeField] private GameObject _hitParticlePrefab;
    [SerializeField] private float _hitFlashDuration = 0.15f;
    [SerializeField] private float _shockwaveScale = 0.75f;
    [SerializeField] private int _hitParticleCount = 7;

    [Header("카메라 쉐이크 설정")]
    [SerializeField] private bool _enableCameraShake = true;
    [SerializeField] private float _shakeIntensity = 0.05f;  // 1~2픽셀
    [SerializeField] private float _shakeDuration = 0.15f;

    #endregion

    #region Private Fields

    private Camera _mainCamera;

    #endregion

    #region MonoBehaviour

    private void Awake()
    {
        _mainCamera = Camera.main;
    }

    #endregion

    #region 슬래시 마크 이펙트

    /// <summary>
    /// 슬래시 마크 재생
    /// </summary>
    /// <param name="position">공격 위치 (플레이어 위치)</param>
    /// <param name="direction">공격 방향</param>
    /// <param name="comboIndex">콤보 인덱스 (1 또는 2)</param>
    public void PlaySlashEffect(Vector2 position, Vector2 direction, int comboIndex)
    {
        // 슬래시 이펙트 위치를 공격 방향 앞쪽으로 오프셋
        Vector2 slashPosition = position + direction * _slashMarkOffset;

        // 무기 휘두르기 연출
        PlayWeaponSwing(position, direction, comboIndex);

        if (_slashMarkPrefab == null)
        {
            // 프리팹이 없으면 Unity 기본 스프라이트로 임시 생성
            CreateTemporarySlashMark(slashPosition, direction, comboIndex);
            return;
        }

        GameObject slash = Instantiate(_slashMarkPrefab, slashPosition, Quaternion.identity);

        // 방향에 따라 회전
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

        // 콤보에 따라 각도 조정
        if (comboIndex == 1)
        {
            // 1타: 우→좌 (우측 상단에서 좌측 하단으로)
            angle += 45f;
        }
        else
        {
            // 2타: 좌→우 (좌측 상단에서 우측 하단으로)
            angle -= 45f;
        }

        slash.transform.rotation = Quaternion.Euler(0, 0, angle);
        slash.transform.localScale = Vector3.one * _slashMarkScale;

        // 색상 설정
        SpriteRenderer sr = slash.GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            sr.color = comboIndex == 1 ? _slashColor1 : _slashColor2;
        }

        // 페이드 아웃 애니메이션
        StartCoroutine(FadeOutAndDestroy(slash, _slashMarkDuration));
    }

    /// <summary>
    /// 임시 슬래시 마크 생성 (프리팹 없을 때)
    /// </summary>
    private void CreateTemporarySlashMark(Vector2 position, Vector2 direction, int comboIndex)
    {
        GameObject slash = new GameObject("TempSlashMark");
        slash.transform.position = position;

        // Unity 기본 사각형 스프라이트 추가
        SpriteRenderer sr = slash.AddComponent<SpriteRenderer>();
        sr.sprite = CreateRectangleSprite();
        sr.color = comboIndex == 1 ? _slashColor1 : _slashColor2;

        // 방향 설정
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        angle += comboIndex == 1 ? 45f : -45f;
        slash.transform.rotation = Quaternion.Euler(0, 0, angle);
        slash.transform.localScale = new Vector3(0.5f, 2f, 1f) * _slashMarkScale;

        StartCoroutine(FadeOutAndDestroy(slash, _slashMarkDuration));
    }

    /// <summary>
    /// 무기 휘두르기 연출
    /// </summary>
    /// <param name="playerPosition">플레이어 위치</param>
    /// <param name="direction">공격 방향</param>
    /// <param name="comboIndex">콤보 인덱스</param>
    private void PlayWeaponSwing(Vector2 playerPosition, Vector2 direction, int comboIndex)
    {
        GameObject weapon;
        if (_weaponStickPrefab == null)
        {
            // 프리팹이 없으면 기본 막대기 생성
            weapon = CreateTemporaryWeaponStick(playerPosition, direction, comboIndex);
        }
        else
        {
            weapon = Instantiate(_weaponStickPrefab, playerPosition, Quaternion.identity);
        }

        // 무기를 플레이어의 자식으로 만들어 따라다니게 함
        weapon.transform.SetParent(transform);
        StartCoroutine(AnimateWeaponSwing(weapon, direction, comboIndex));
    }

    /// <summary>
    /// 임시 무기 막대기 생성 (프리팹 없을 때)
    /// </summary>
    private GameObject CreateTemporaryWeaponStick(Vector2 playerPosition, Vector2 direction, int comboIndex)
    {
        GameObject weapon = new GameObject("TempWeaponStick");
        weapon.transform.position = playerPosition;

        // LineRenderer로 막대기 표현
        LineRenderer lr = weapon.AddComponent<LineRenderer>();
        lr.startWidth = 0.1f;
        lr.endWidth = 0.05f;
        lr.material = new Material(Shader.Find("Sprites/Default"));
        lr.startColor = _weaponColor;
        lr.endColor = _weaponColor;
        lr.positionCount = 2;
        lr.useWorldSpace = false;  // 로컬 좌표 사용

        // 막대기 길이 설정 - Position 0이 회전 축(플레이어 중심)
        // Position 0: 회전 축 (플레이어 중심에 고정)
        // Position 1: 막대기 끝 (회전하면서 움직임)
        lr.SetPosition(0, Vector3.zero);  // 축: GameObject 원점 = 플레이어 중심
        lr.SetPosition(1, new Vector3(_weaponStickLength, 0, 0));  // 끝: 오른쪽으로 연장

        return weapon;
    }

    /// <summary>
    /// 무기 휘두르기 애니메이션
    /// 무기는 플레이어 중심(localPosition = 0)을 축으로 회전
    /// </summary>
    private IEnumerator AnimateWeaponSwing(GameObject weapon, Vector2 direction, int comboIndex)
    {
        float elapsed = 0f;
        float baseAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

        // 시작 각도와 끝 각도 계산
        float startAngle, endAngle;
        if (comboIndex == 1)
        {
            // 1타: 우측에서 좌측으로 (시계 방향)
            startAngle = baseAngle + (_weaponArcAngle / 2f);
            endAngle = baseAngle - (_weaponArcAngle / 2f);
        }
        else
        {
            // 2타: 좌측에서 우측으로 (반시계 방향)
            startAngle = baseAngle - (_weaponArcAngle / 2f);
            endAngle = baseAngle + (_weaponArcAngle / 2f);
        }

        // 플레이어 중심을 회전 축으로 설정 (LineRenderer의 Position 0이 여기 위치)
        weapon.transform.localPosition = Vector3.zero;
        weapon.transform.rotation = Quaternion.Euler(0, 0, startAngle);

        // 회전 애니메이션
        while (elapsed < _weaponSwingDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / _weaponSwingDuration;

            // Ease-out 커브 적용 (빠르게 시작, 천천히 끝)
            float easedT = 1f - Mathf.Pow(1f - t, 3f);

            float currentAngle = Mathf.Lerp(startAngle, endAngle, easedT);
            weapon.transform.rotation = Quaternion.Euler(0, 0, currentAngle);

            // 페이드 아웃
            if (weapon.TryGetComponent<SpriteRenderer>(out SpriteRenderer sr))
            {
                Color color = sr.color;
                color.a = 1f - t;
                sr.color = color;
            }
            else if (weapon.TryGetComponent<LineRenderer>(out LineRenderer lr))
            {
                Color color = lr.startColor;
                color.a = _weaponColor.a * (1f - t);
                lr.startColor = color;
                lr.endColor = color;
            }

            yield return null;
        }

        Destroy(weapon);
    }

    /// <summary>
    /// Unity 기본 사각형 스프라이트 생성
    /// </summary>
    private Sprite CreateRectangleSprite()
    {
        Texture2D texture = new Texture2D(16, 64);
        Color[] pixels = new Color[16 * 64];
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = Color.white;
        }
        texture.SetPixels(pixels);
        texture.Apply();

        return Sprite.Create(texture, new Rect(0, 0, 16, 64), new Vector2(0.5f, 0.5f));
    }

    #endregion

    #region 타격 이펙트

    /// <summary>
    /// 타격 이펙트 재생
    /// </summary>
    /// <param name="hitPosition">타격 위치</param>
    public void PlayHitEffect(Vector2 hitPosition)
    {
        // 적 점멸
        PlayHitFlash(hitPosition);

        // 충격파 링
        PlayShockwaveRing(hitPosition);

        // 파티클 산개
        PlayHitParticles(hitPosition);

        // 카메라 쉐이크
        if (_enableCameraShake)
        {
            StartCoroutine(CameraShake());
        }
    }

    /// <summary>
    /// 적 점멸 효과
    /// </summary>
    private void PlayHitFlash(Vector2 position)
    {
        if (_hitFlashPrefab != null)
        {
            GameObject flash = Instantiate(_hitFlashPrefab, position, Quaternion.identity);
            Destroy(flash, _hitFlashDuration);
        }
    }

    /// <summary>
    /// 충격파 링 효과
    /// </summary>
    private void PlayShockwaveRing(Vector2 position)
    {
        if (_shockwaveRingPrefab == null)
        {
            // 임시 충격파 생성
            CreateTemporaryShockwave(position);
            return;
        }

        GameObject ring = Instantiate(_shockwaveRingPrefab, position, Quaternion.identity);
        StartCoroutine(AnimateShockwave(ring));
    }

    /// <summary>
    /// 임시 충격파 생성 (프리팹 없을 때)
    /// </summary>
    private void CreateTemporaryShockwave(Vector2 position)
    {
        GameObject ring = new GameObject("TempShockwave");
        ring.transform.position = position;

        LineRenderer lr = ring.AddComponent<LineRenderer>();
        lr.useWorldSpace = false;
        lr.startWidth = 0.1f;
        lr.endWidth = 0.1f;
        lr.material = new Material(Shader.Find("Sprites/Default"));
        lr.startColor = Color.yellow;
        lr.endColor = Color.yellow;

        // 원형 만들기
        int segments = 32;
        lr.positionCount = segments + 1;
        float radius = 0.5f;

        for (int i = 0; i <= segments; i++)
        {
            float angle = i * 360f / segments * Mathf.Deg2Rad;
            Vector3 pos = new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0);
            lr.SetPosition(i, pos);
        }

        StartCoroutine(AnimateShockwave(ring));
    }

    /// <summary>
    /// 충격파 애니메이션
    /// </summary>
    private IEnumerator AnimateShockwave(GameObject ring)
    {
        float elapsed = 0f;
        float duration = _hitFlashDuration;
        Vector3 startScale = Vector3.one * 0.5f * _shockwaveScale;
        Vector3 endScale = Vector3.one * _shockwaveScale;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            ring.transform.localScale = Vector3.Lerp(startScale, endScale, t);

            // 페이드 아웃
            if (ring.TryGetComponent<SpriteRenderer>(out SpriteRenderer sr))
            {
                Color color = sr.color;
                color.a = 1f - t;
                sr.color = color;
            }
            else if (ring.TryGetComponent<LineRenderer>(out LineRenderer lr))
            {
                Color color = lr.startColor;
                color.a = 1f - t;
                lr.startColor = color;
                lr.endColor = color;
            }

            yield return null;
        }

        Destroy(ring);
    }

    /// <summary>
    /// 파티클 산개 효과
    /// </summary>
    private void PlayHitParticles(Vector2 position)
    {
        for (int i = 0; i < _hitParticleCount; i++)
        {
            Vector2 randomDirection = Random.insideUnitCircle.normalized;
            CreateHitParticle(position, randomDirection);
        }
    }

    /// <summary>
    /// 단일 파티클 생성
    /// </summary>
    private void CreateHitParticle(Vector2 position, Vector2 direction)
    {
        GameObject particle = new GameObject("HitParticle");
        particle.transform.position = position;

        SpriteRenderer sr = particle.AddComponent<SpriteRenderer>();
        sr.sprite = CreateSmallCircleSprite();
        sr.color = new Color(1f, 0.9f, 0.7f);  // 연한 주황색
        particle.transform.localScale = Vector3.one * 0.1f;

        StartCoroutine(AnimateParticle(particle, direction));
    }

    /// <summary>
    /// 작은 원형 스프라이트 생성
    /// </summary>
    private Sprite CreateSmallCircleSprite()
    {
        int size = 16;
        Texture2D texture = new Texture2D(size, size);
        Color[] pixels = new Color[size * size];

        int center = size / 2;
        float radius = size / 2f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                pixels[y * size + x] = dist <= radius ? Color.white : Color.clear;
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();

        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    /// <summary>
    /// 파티클 애니메이션
    /// </summary>
    private IEnumerator AnimateParticle(GameObject particle, Vector2 direction)
    {
        float elapsed = 0f;
        float duration = 0.2f;
        float speed = 2f;
        Vector2 startPos = particle.transform.position;

        SpriteRenderer sr = particle.GetComponent<SpriteRenderer>();

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            // 이동
            particle.transform.position = startPos + direction * speed * t;

            // 페이드 아웃
            if (sr != null)
            {
                Color color = sr.color;
                color.a = 1f - t;
                sr.color = color;
            }

            yield return null;
        }

        Destroy(particle);
    }

    #endregion

    #region 카메라 쉐이크

    /// <summary>
    /// 카메라 쉐이크
    /// </summary>
    private IEnumerator CameraShake()
    {
        if (_mainCamera == null)
        {
            yield break;
        }

        Vector3 originalPosition = _mainCamera.transform.localPosition;
        float elapsed = 0f;

        while (elapsed < _shakeDuration)
        {
            elapsed += Time.deltaTime;

            float x = Random.Range(-1f, 1f) * _shakeIntensity;
            float y = Random.Range(-1f, 1f) * _shakeIntensity;

            _mainCamera.transform.localPosition = originalPosition + new Vector3(x, y, 0);

            yield return null;
        }

        _mainCamera.transform.localPosition = originalPosition;
    }

    #endregion

    #region 유틸리티

    /// <summary>
    /// 페이드 아웃 후 파괴
    /// </summary>
    private IEnumerator FadeOutAndDestroy(GameObject obj, float duration)
    {
        SpriteRenderer sr = obj.GetComponent<SpriteRenderer>();
        if (sr == null)
        {
            yield break;
        }

        float elapsed = 0f;
        Color startColor = sr.color;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float alpha = 1f - (elapsed / duration);

            Color newColor = startColor;
            newColor.a = alpha;
            sr.color = newColor;

            // 얇아지는 효과
            Vector3 scale = obj.transform.localScale;
            scale.x = Mathf.Lerp(scale.x, scale.x * 0.5f, elapsed / duration);
            obj.transform.localScale = scale;

            yield return null;
        }

        Destroy(obj);
    }

    #endregion
}