using UnityEngine;
using System;

[CreateAssetMenu(fileName = "OceanSpawnSetting", menuName = "ScriptableObjects/OceanSpawnSetting", order = 1)]
public class OceanSpawnSetting : ScriptableObject
{
    [Tooltip("오션 스폰 설정 리스트")]
    public OceanSpawnSettingInfo[] OceanSpawnSettings;
}

[Serializable]
public class OceanSpawnSettingInfo
{
    [Tooltip("오션 타입")]
    public OceanType OceanType;
    [Tooltip("스폰 위치")]
    public SpawnPosAndRot[] SpawnPosAndRots;
}

[Serializable]
public class SpawnPosAndRot
{
    [Tooltip("스폰 위치")]
    public Vector2 SpawnPosition;
    [Tooltip("스폰 회전값")]
    public float SpawnRotation;
}