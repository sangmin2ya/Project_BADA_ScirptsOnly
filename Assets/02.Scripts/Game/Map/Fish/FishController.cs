using System.Collections;
using UnityEngine;

public class FishController : MonoBehaviour
{
    #region Serialized Fields
    [SerializeField] private Transform[] _fishBones;
    [SerializeField] private float _lerpSpeed = 1f;
    [SerializeField] private float _rotationSpeed = 20f;
    [SerializeField] private float _angleLimit = 10f;
    [SerializeField] private float _swayMovementAmount = 0.5f;
    #endregion

    #region Private Fields
    private float[] _boneDistances;
    private Vector3 _startLocalPosition; // 로컬 포지션으로 변경
    private float _fishTime; // 회전과 움직임이 공유할 시간값
    #endregion

    #region Unity Methods
    private void Start()
    {
        InitializeBoneDistances();
        _startLocalPosition = transform.localPosition; // 로컬 포지션으로 변경
        
        // 랜덤 시작 시간으로 여러 물고기가 서로 다른 패턴을 갖도록 함
        _fishTime = Random.Range(0f, 2f * Mathf.PI);
        
        if(_fishBones == null || _fishBones.Length == 0 || _fishBones[0] == null)
        {
            Debug.LogError("FishController: fishBones 배열이 null이거나 비어있거나 첫 번째 요소가 null입니다.");
            return;
        }
        StartCoroutine(RotateFishContinuously(_fishBones[0]));
    }

    private void Update()
    {
        // 공통 시간값 업데이트
        _fishTime += Time.deltaTime;
        
        UpdateFishMovement();
        UpdateSwayMovement();
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// 각 뼈대 간의 거리를 초기화합니다
    /// </summary>
    private void InitializeBoneDistances()
    {
        if (_fishBones == null || _fishBones.Length < 2)
        {
            Debug.LogError("FishController: fishBones 배열이 null이거나 길이가 2 미만입니다.");
            return;
        }

        _boneDistances = new float[_fishBones.Length];

        for (int i = 1; i < _fishBones.Length; i++)
        {
            if (_fishBones[i] == null || _fishBones[i - 1] == null)
            {
                Debug.LogError($"FishController: fishBones[{i}] 또는 fishBones[{i - 1}]이 null입니다.");
                continue;
            }

            _boneDistances[i] = Vector2.Distance(_fishBones[i].position, _fishBones[i - 1].position);
        }
    }

    /// <summary>
    /// 물고기 뼈대의 움직임을 업데이트합니다
    /// </summary>
    private void UpdateFishMovement()
    {
        if (_fishBones == null || _boneDistances == null)
        {
            return;
        }

        for (int i = 1; i < _fishBones.Length; i++)
        {
            if (_fishBones[i] == null || _fishBones[i - 1] == null)
            {
                continue;
            }

            Transform currentBone = _fishBones[i];
            Transform previousBone = _fishBones[i - 1];

            // 회전을 이전 뼈대를 천천히 따라가도록
            currentBone.localRotation = Quaternion.Slerp(currentBone.localRotation, previousBone.localRotation, Time.deltaTime * _lerpSpeed);

            // 위치는 이전 뼈대의 -up 방향(꼬리 방향)으로 일정 거리만큼 떨어진 곳에 고정
            currentBone.position = (Vector2)previousBone.position + (Vector2)(-previousBone.up) * _boneDistances[i];
        }
    }

    /// <summary>
    /// 물고기가 몸을 흔들 때 좌우로 흔들리는 움직임을 업데이트합니다
    /// </summary>
    private void UpdateSwayMovement()
    {
        // 회전과 같은 시간값을 사용하여 동기화된 좌우 움직임
        float swayValue = Mathf.Sin(_fishTime * _rotationSpeed) * _swayMovementAmount;
        
        // 물고기가 바라보는 방향 기준으로 좌우 이동 (로컬 좌표계)
        Vector3 rightDirection = transform.right; // 물고기의 오른쪽 방향
        Vector3 swayOffset = transform.InverseTransformDirection(rightDirection) * swayValue;
        
        // 시작 로컬 위치를 기준으로 흔들림 적용
        transform.localPosition = _startLocalPosition + swayOffset;
    }

    /// <summary>
    /// 물고기를 실제 물고기처럼 사인파 패턴으로 좌우 회전시키는 코루틴
    /// </summary>
    private IEnumerator RotateFishContinuously(Transform fishTransform)
    {
        float time = 0f;

        while (true)
        {
            time += Time.deltaTime;
            
            // 사인파를 사용한 자연스러운 좌우 회전 (실제 물고기의 꼬리 진동 패턴)
            float currentAngle = Mathf.Sin(time * _rotationSpeed) * _angleLimit;

            // Z축을 기준으로 회전 적용
            fishTransform.localRotation = Quaternion.Euler(0, 0, currentAngle);

            yield return null;
        }
    }
    #endregion
}