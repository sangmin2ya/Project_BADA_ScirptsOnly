using Cinemachine;
using UnityEngine;

public class CameraAnchorHandler : MonoBehaviour
{
    [SerializeField] private bool _isTargetPlayer = false;

    [Tooltip("따라갈 타겟")]
    [SerializeField] private Transform _target;

    [Tooltip("이동이 부드러움의 정도 (값이 낮을수록 부드러움)")]
    [SerializeField] private float _smoothMoveSpeed = 5f;
    [Tooltip("회전이 부드러움의 정도 (값이 낮을수록 부드러움)")]
    [SerializeField] private float _smoothRotateSpeed = 0;

    private void Awake()
    {
        // if (_isTargetPlayer)
        // {
        //     CameraManager.Instance.SetPlayerAnchor(this);
        // }
        // else
        // {
        //     CameraManager.Instance.SetShipAnchor(this);
        // }
    }

    private void LateUpdate()
    {
        if (_target == null)
        {
            return;
        }

        // 타겟의 로컬 위치를 목표 지점으로 설정
        Vector3 targetPosition = _target.position;
        Quaternion targetRotation = _target.rotation;
        // 자신의 로컬 위치를 목표 지점으로 부드럽게 이동 (Lerp 사용)
        transform.position = Vector3.Lerp(transform.position, targetPosition, _smoothMoveSpeed * Time.deltaTime);
        transform.rotation = Quaternion.Lerp(transform.rotation, targetRotation, _smoothRotateSpeed * Time.deltaTime);
    }

    public void SetTarget( Transform target )
    {
        _target = target;
    }
}