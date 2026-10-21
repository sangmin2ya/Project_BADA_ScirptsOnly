using UnityEngine;

/// <summary>
/// 잠수함이 발사하는 어뢰 투사체
/// [조우석 - 2025.08.14.]
/// </summary>
public class Torpedo : Bullet
{
    [SerializeField] private ParticleSystemRenderer _bubbleParticle;
    [SerializeField] private float _speed = 50;

    public override void OnStartClient()
    {
        base.OnStartClient();
        _bulletSprite.sortingOrder = Mathf.FloorToInt(_currentHeight * _offset / 0.01f);
        _bubbleParticle.sortingOrder = _bulletSprite.sortingOrder - 1;
        _isAbleToParry = false; // 어뢰는 패링 불가능
    }

    private void FixedUpdate()
    {
        // Bullet의 FixedUpdate를 수행하지 않음.
    }

    protected override void OnDespawned()
    {
        ClearIndicators();
        Instantiate(_hitParticlePrefab, transform.position, Quaternion.identity);
    }
}
