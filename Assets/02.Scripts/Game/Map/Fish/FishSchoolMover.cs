using UnityEngine;

public class FishSchoolMover : MonoBehaviour
{
    #region Serialized Fields
    [SerializeField] private Vector3 moveDirection = Vector3.right;
    [SerializeField] private float moveSpeed = 3f;
    #endregion

    #region Unity Methods
    private void Update()
    {
        MoveFishSchool();
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// 물고기 무리를 이동시킵니다
    /// </summary>
    private void MoveFishSchool()
    {
        transform.position += moveDirection * moveSpeed * Time.deltaTime;
    }

    /// <summary>
    /// 물고기 무리가 이동 방향을 바라보도록 회전시킵니다
    /// </summary>
    private void RotateTowardsMovementDirection()
    {
        if (moveDirection != Vector3.zero)
        {
            // 프리팹 기준 위쪽이 앞방향이므로 90도 보정
            float angle = Mathf.Atan2(moveDirection.x, moveDirection.y) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0, 0, -angle);
        }
    }
    #endregion

    #region Public Methods
    /// <summary>
    /// 이동 방향과 속도를 설정합니다
    /// </summary>
    public void SetMovement(Vector3 direction, float speed)
    {
        moveDirection = direction.normalized;
        moveSpeed = speed;
        RotateTowardsMovementDirection();
    }
    #endregion
}