using UnityEngine;

public class AreaZone : MonoBehaviour
{
    [Header("구역 설정")]
    [Tooltip("이 구역의 타입")]
    [SerializeField] private AreaType _areaType = AreaType.안전구역;
    public AreaType ZoneAreaType => _areaType;
}