using UnityEngine;
using System;
/// <summary>
/// 가중치 설정 ScriptableObject
/// </summary>
[CreateAssetMenu(fileName = "New Spawn Weight Settings", menuName = "ScriptableObjects/Spawn System/Spawn Weight Settings")]
public class EnemySpawnWeightSettings : ScriptableObject
{
    [Header("기본 가중치 프로필")]
    public EnemyWeightProfile DefaultProfile;

    [Header("스테이지별 가중치 매핑")]
    public TimeRangeProfile[] TimeRangeProfiles;
}

[Serializable]
public class StageWeightMapping // 추후 스테이지별 매핑이 필요할 경우 사용
{
    public int StageIndex;
    public TimeBasedWeightMapping TimeBasedMapping;
    //public ZoneBasedWeightMapping zoneBasedMapping; // 추후 구역별 가중치 매핑이 필요할 경우 사용
}

[Serializable]
public class TimeBasedWeightMapping // 추후 타임유닛별 매핑이 필요할 경우 사용
{
    public TimeRangeProfile[] TimeProfiles;
}

[Serializable]
public class TimeRangeProfile
{
    public TimeType TimeType;
    public EnemyWeightProfile[] WeightProfile;
}

[Serializable]
public class EnemyWeightProfile
{
    [Tooltip("적 타입")]
    public EnemyShipType EnemyShipType;
    [Tooltip("적 프리팹")]
    public EnemyControllerBase EnemyPrefab;
    [Tooltip("가중치")]
    public float Weight;
    [Tooltip("소모 난이도 포인트")]
    public float Point;
}