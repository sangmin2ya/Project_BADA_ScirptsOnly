using Mirror;
using System;
using System.Collections.Generic;
using UnityEngine;
using static NetworkParentSync;
/// <summary>
/// 네트워크 부모 동기화를 위한 컴포넌트
/// [박상민 - 2025.06.19]
/// </summary>
public class NetworkParentSync : NetworkBehaviour
{
    [SyncVar(hook = nameof(OnParentInfoChanged))]
    private ParentInfo _parentInfo;

    [Serializable]
    public struct ParentInfo
    {
        public uint RootNetworkId;      // NetworkIdentity를 갖는 루트 부모
        public string RelativePath;     // 루트로부터의 상대 경로
    }

    #region 서버 메소드
    /// <summary>
    /// 중첩된 부모를 설정합니다
    /// </summary>
    [Server]
    public void SetNetworkParent(Transform targetParent)
    {
        // 가장 가까운 NetworkIdentity 부모 찾기
        Transform currentTransform = targetParent;
        NetworkIdentity rootNetworkIdentity = null;

        while (currentTransform != null)
        {
            rootNetworkIdentity = currentTransform.GetComponent<NetworkIdentity>();
            if (rootNetworkIdentity != null)
            {
                break;
            }
            currentTransform = currentTransform.parent;
        }

        if (rootNetworkIdentity != null)
        {
            // 루트로부터 타겟까지의 경로 계산
            string relativePath = GetRelativePathFromRootToTarget(rootNetworkIdentity.transform, targetParent);

            _parentInfo = new ParentInfo
            {
                RootNetworkId = rootNetworkIdentity.netId,
                RelativePath = relativePath
            };

            transform.SetParent(targetParent);
        }
        else
        {
            Debug.Log("NetworkIdentity를 가진 부모를 찾을 수 없습니다.");
        }
    }
    #endregion

    #region 동기화 콜백
    /// <summary>
    /// 부모 정보가 변경되었을 때 호출됩니다
    /// </summary>
    private void OnParentInfoChanged(ParentInfo oldParentInfo, ParentInfo newParentInfo)
    {
        Debug.Log($"{newParentInfo.RootNetworkId != 0}         {NetworkClient.spawned.TryGetValue(newParentInfo.RootNetworkId, out NetworkIdentity rootIdentity)}");
        if (newParentInfo.RootNetworkId != 0 && NetworkClient.spawned.TryGetValue(newParentInfo.RootNetworkId, out  rootIdentity) && !isServer)
        {
            Transform targetParentTransform = FindChildTransformByPath(rootIdentity.transform, newParentInfo.RelativePath);
            Vector3 currentPosition = transform.position;
            Quaternion currentRotation = transform.rotation;
            Debug.Log($"{targetParentTransform}        {currentPosition}      {currentRotation}");
            if (targetParentTransform != null)
            {
                transform.SetParent(targetParentTransform);
                transform.position = currentPosition; // 부모 변경 후에도 원래 위치 유지
                transform.rotation = currentRotation; // 부모 변경 후에도 원래 회전 유지
            }
            else
            {
                Debug.Log($"경로를 찾을 수 없습니다: {newParentInfo.RelativePath}");
            }
        }
    }
    #endregion

    #region 유틸리티 메소드
    /// <summary>
    /// 루트에서 타겟까지의 상대 경로를 가져옵니다
    /// </summary>
    private string GetRelativePathFromRootToTarget(Transform rootTransform, Transform targetTransform)
    {
        if (rootTransform == targetTransform)
        {
            return "";
        }

        List<string> pathSegments = new List<string>();
        Transform currentTransform = targetTransform;

        while (currentTransform != rootTransform && currentTransform != null)
        {
            pathSegments.Insert(0, currentTransform.name);
            currentTransform = currentTransform.parent;
        }

        return string.Join("/", pathSegments);
    }

    /// <summary>
    /// 경로를 통해 자식 Transform을 찾습니다
    /// </summary>
    private Transform FindChildTransformByPath(Transform rootTransform, string pathString)
    {
        if (string.IsNullOrEmpty(pathString))
        {
            return rootTransform;
        }

        string[] pathParts = pathString.Split('/');
        Transform currentTransform = rootTransform;

        foreach (string pathPart in pathParts)
        {
            currentTransform = currentTransform.Find(pathPart);
            if (currentTransform == null)
            {
                return null;
            }
        }

        return currentTransform;
    }
    #endregion
}