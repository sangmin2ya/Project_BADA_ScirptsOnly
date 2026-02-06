using UnityEngine;
using Mirror;
/// <summary>
/// 고철 파밍용 오브젝트
/// [박상민 - 2025.07.18]
/// </summary>
public class ScrapObject : LTDObject, ISaleable
{
    [Header("스크랩 설정")]
    [Tooltip("스크랩 오브젝트의 종류")]
    [SerializeField] private ScrapType _scrapType;
    [Tooltip("스크랩 오브젝트의 가치")]
    [SerializeField] private uint _price = 10;
    public ScrapType ScrapType => _scrapType;
    public uint Price => _price;
}