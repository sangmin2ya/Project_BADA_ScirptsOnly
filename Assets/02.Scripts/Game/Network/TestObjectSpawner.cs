using System;
using System.Collections.Generic;
using Mirror;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// 테스트용 오브젝트 스포너
/// [박상민 - 2025.06.11]
/// </summary>
public class TestObjectSpawner : MonoBehaviour
{
    #region 필드

    //[SerializeField] private Transform _spawnTransform;
    private NetworkObjectSpawner _networkObjectSpawner;
    private Transform _buildingSpawnTransform;
    private Transform _secretBaseSpawnTransform;
    private Transform _BattleShipSpawnTransform;
    private Transform _fleeShipSpawnTransform;
    private Transform _zakkoShipSpawnTransform;
    private Transform _submarineSpawnTransform;

    #endregion

    #region Unity 생명주기

    private void Start()
    {
        // NetworkObjectSpawner 인스턴스가 준비될 때까지 대기
        if (NetworkObjectSpawner.Instance != null)
        {
            _networkObjectSpawner = NetworkObjectSpawner.Instance;
        }
        else
        {
            Debug.LogWarning("NetworkObjectSpawner 인스턴스가 아직 준비되지 않았습니다. Update에서 재시도합니다.");
        }

        // _BattleShipSpawnTransform = GameObject.Find("BattleShipSpawnPos")?.transform;
        // _fleeShipSpawnTransform = GameObject.Find("FleeShipSpawnPos")?.transform;
        // _zakkoShipSpawnTransform = GameObject.Find("ZakkoShipSpawnPos")?.transform;
        // _submarineSpawnTransform = GameObject.Find("SubmarineSpawnPos")?.transform;

        // if (_buildingSpawnTransform == null || _BattleShipSpawnTransform == null ||
        //     _fleeShipSpawnTransform == null || _zakkoShipSpawnTransform == null || _submarineSpawnTransform == null)
        // {
        //     Debug.LogError("스폰 위치가 설정되지 않았습니다. 스폰 위치 오브젝트를 확인하세요.");
        // }

        _buildingSpawnTransform = GameObject.Find("BuildingSpawnPos")?.transform;
        if (_buildingSpawnTransform == null)
        {
            Debug.LogError("BuildingSpawnPos 오브젝트를 찾을 수 없습니다. 스폰 위치 오브젝트를 확인하세요.");
            return;
        }
        SpawnBuildingObjects();

        _secretBaseSpawnTransform = GameObject.Find("SecretBaseSpawnPos")?.transform;
        if (_secretBaseSpawnTransform == null)
        {
            Debug.LogError("SecretBaseSpawnPos 오브젝트를 찾을 수 없습니다. 스폰 위치 오브젝트를 확인하세요.");
            return;
        }
        SpawnSecretBaseObjects();
    }

    private void SpawnEnemyObjects()
    {
        for (int i = 0; i < _BattleShipSpawnTransform.childCount; i++)
        {
            if (_networkObjectSpawner != null && _BattleShipSpawnTransform != null)
            {
                _networkObjectSpawner.CmdRequestSpawnEnemyObject(EnemyShipType.전투함선, _BattleShipSpawnTransform.GetChild(i).position, Quaternion.identity);
            }
            else
            {
                Debug.LogWarning("NetworkObjectSpawner 또는 BattleShipSpawnTransform이 null입니다.");
            }
        }
        // for (int i = 0; i < _fleeShipSpawnTransform.childCount; i++)
        // {
        //     if (_networkObjectSpawner != null && _fleeShipSpawnTransform != null)
        //     {
        //         _networkObjectSpawner.CmdRequestSpawnEnemyObject(EnemyShipType.도주함선, _fleeShipSpawnTransform.GetChild(i).position, Quaternion.identity);
        //     }
        //     else
        //     {
        //         Debug.LogWarning("NetworkObjectSpawner 또는 FleeShipSpawnTransform이 null입니다.");
        //     }
        // }
        for (int i = 0; i < _zakkoShipSpawnTransform.childCount; i++)
        {
            if (_networkObjectSpawner != null && _zakkoShipSpawnTransform != null)
            {
                _networkObjectSpawner.CmdRequestSpawnEnemyObject(EnemyShipType.자코함선, _zakkoShipSpawnTransform.GetChild(i).position, Quaternion.identity);
            }
            else
            {
                Debug.LogWarning("NetworkObjectSpawner 또는 ZakkoShipSpawnTransform이 null입니다.");
            }
        }
        for (int i = 0; i < _submarineSpawnTransform.childCount; i++)
        {
            if (_networkObjectSpawner != null && _submarineSpawnTransform != null)
            {
                _networkObjectSpawner.CmdRequestSpawnEnemyObject(EnemyShipType.잠수함, _submarineSpawnTransform.GetChild(i).position, Quaternion.identity);
            }
            else
            {
                Debug.LogWarning("NetworkObjectSpawner 또는 SubmarineSpawnTransform이 null입니다.");
            }
        }
    }

    private void SpawnBuildingObjects()
    {
        for (int i = 0; i < _buildingSpawnTransform.childCount; i++)
        {
            if (_networkObjectSpawner != null && _buildingSpawnTransform != null)
            {
                Transform transform = _buildingSpawnTransform.GetChild(i);
                _networkObjectSpawner.RequestSpawnBuildingobject(i, transform.position, transform.rotation);
            }
            else
            {
                Debug.LogWarning("NetworkObjectSpawner 또는 BuildingSpawnTransform이 null입니다.");
            }
        }
    }

    private void SpawnSecretBaseObjects()
    {
        if (_networkObjectSpawner != null && _secretBaseSpawnTransform != null)
        {
            _networkObjectSpawner.RequestSecretBaseObject(_secretBaseSpawnTransform.position, _secretBaseSpawnTransform.rotation);
        }
    }

    #endregion
}