// using Cinemachine;
// using Mirror;
// using UnityEngine;

// public class CameraManager : NetworkSingleton<CameraManager>
// {
//     [SerializeField] private Camera _originCam;
//     [SerializeField] public Camera _agentCam;

//     [SerializeField] private float _playerOrthoSize = 10;
//     [SerializeField] private float _shipOrthoSize = 50;

//     private CinemachineVirtualCamera _originVirtualCam;
//     private Vector3 _cameraZOffset = new Vector3(0, 0, -10);
//     private float _heightOffset = 2f; // 배 갑판 높이
//     private float _sinkingPhaseHeightOffset = 0f; // 침몰시 추가 높이 오프셋
//     [SyncVar] private bool _isSinkingPhase = false;

//     public Camera AgentCam => _agentCam;
//     public Camera OriginCam => _originCam;

//     private void LateUpdate()
//     {
//         if (_originCam != null && _agentCam != null)
//         {
//             FollowTarget();
//         }
//     }

//     private void FollowTarget()
//     {
//         if (_originVirtualCam == null)
//         {
//             _originVirtualCam = _originCam.GetComponent<CinemachineBrain>().ActiveVirtualCamera as CinemachineVirtualCamera;
//             if (_originVirtualCam == null)
//             {
//                 return;
//             }
//         }
//         // Agent의 카메라 위치는 실제 Origin의 위치에서 Offset 값 만큼 감소
//         // Agent의 카메라 회전은 Origin의 회전을 그대로 따라감
//         _agentCam.transform.position = _originVirtualCam.transform.position - (Vector3)TempShipSpawner.Instance.OriginOffset + _cameraZOffset + _originVirtualCam.transform.up * ((_isSinkingPhase ? _sinkingPhaseHeightOffset : _heightOffset) * 0.5f);
//         _agentCam.transform.rotation = _originVirtualCam.transform.rotation;
//     }

//     [TargetRpc]
//     public void RpcSwitchCamera(NetworkConnection conn, bool isOnModule)
//     {
//         if (isOnModule)
//         {
//             // 일단은 시야 범위는 즉각적으로 바뀌도록 설정 후 확인
//             _agentCam.orthographicSize = _shipOrthoSize;
//             _originVirtualCam.m_Lens.OrthographicSize = _shipOrthoSize;
//         }
//         else
//         {
//             // 일단은 시야 범위는 즉각적으로 바뀌도록 설정 후 확인
//             _agentCam.orthographicSize = _playerOrthoSize;
//             _originVirtualCam.m_Lens.OrthographicSize = _playerOrthoSize;
//         }
//     }

//     [Server]
//     public void SetSinkingPhase(bool isSinking)
//     {
//         _isSinkingPhase = isSinking;
//     }
// }
