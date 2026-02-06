using UnityEngine;
using System;
/// <summary>
/// 맵 구역별 적 스폰 설정 ScriptableObject
/// </summary>
[CreateAssetMenu(fileName = "New Area Spawn Settings", menuName = "ScriptableObjects/Spawn System/Area Spawn Settings")]
public class AreaSpawnSettings : ScriptableObject
{
    [Header("맵 구역별 적 스폰 정보")]
    public AreaSpawnInfo[] AreaSpawnInfos;
}
/// <summary>
/// 맵 구역별 적 스폰 정보 구조체
/// </summary>
[Serializable]
public struct AreaSpawnInfo
{
    [Tooltip("맵 구역 타입")]
    public AreaType AreaType;
    [Tooltip("기본 스폰 비율 (0~1)")]
    [Range(0f, 1f)] public float BaseSpawnRate;
    [Tooltip("분당 스폰 비율 (0~1)")]
    [Range(0f, 1f)] public float SpawnRatePerMinute;
    [Tooltip("기본 스폰 웨이브 횟수")]
    public int EnemyWaveCount;
    [Tooltip("웨이브 간 쉬는시간 (초)")]
    public float WaveCooldownTime;
    [Tooltip("웨이브 타임아웃 시간 (초)")]
    public float WaveTimeOut;
    [Tooltip("스폰 자원 (난이도 포인트)")]
    public int DifficultyPoints;
}