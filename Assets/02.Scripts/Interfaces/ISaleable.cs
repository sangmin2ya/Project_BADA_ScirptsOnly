using UnityEngine;

/// <summary>
/// 상점에서 거래가능한 물건
/// [박상민 - 2025.10.30]
/// </summary>
public interface ISaleable
{
    [Tooltip("판매 가격")]
    uint Price { get; }
}