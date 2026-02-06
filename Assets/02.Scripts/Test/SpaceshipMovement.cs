using UnityEngine;

public class SpaceshipMovement : MonoBehaviour
{
    [Header("움직임 설정")]
    [SerializeField] private float maxForward = 10f;
    [SerializeField] private float maxBackward = 5f;
    [SerializeField] private float maxSide = 3f;
    [SerializeField] private float forwardAcceleration = 5f;
    [SerializeField] private float sideAcceleration = 3f;
    
    /*
    [Header("물리 설정")]
    [SerializeField] private float dragCoefficient = 0.1f;
    [SerializeField] private float frictionCoefficient = 2f;
    */
    // [SerializeField] private float sideFrictionMultiplier = 1.5f;

    private float forwardSpeed = 0f;
    private float sideSpeed = 0f;
    private Vector2 velocity = Vector2.zero;
    private Vector2 inputVector = Vector2.zero;
    
    void Update()
    {
        HandleInput();
        UpdateMovement();
        ApplyMovement();
    }
    
    void HandleInput()
    {
        inputVector = Vector2.zero;
        
        if (Input.GetKey(KeyCode.W))
            inputVector.y = 1f;
        else if (Input.GetKey(KeyCode.S))
            inputVector.y = -1f;
            
        if (Input.GetKey(KeyCode.A))
            inputVector.x = -1f;
        else if (Input.GetKey(KeyCode.D))
            inputVector.x = 1f;
    }

    void UpdateMovement()
    {
        // 전진/후진 처리
        forwardSpeed += inputVector.y * Time.deltaTime * forwardAcceleration;
        sideSpeed += inputVector.x * Time.deltaTime * sideAcceleration;
        
        forwardSpeed = Mathf.Clamp(forwardSpeed, -maxBackward, maxForward);
        sideSpeed = Mathf.Clamp(sideSpeed, -maxSide, maxSide);

        if (Mathf.Abs(inputVector.y) < 0.1f && Mathf.Abs(forwardSpeed) < 0.1f) forwardSpeed = 0f;
        if (Mathf.Abs(inputVector.x) < 0.1f && Mathf.Abs(sideSpeed) < 0.1f) sideSpeed = 0f;
        
        velocity = forwardSpeed * transform.up + sideSpeed * transform.right;

        //ApplyPhysicsEffects();
    }

    /*
    void ApplyPhysicsEffects()
    {
        // 항력 (속도의 제곱에 비례)
        float speedSquared = velocity.sqrMagnitude;
        Vector2 dragForce = dragCoefficient * speedSquared * velocity.normalized;
        velocity -= dragForce * Time.deltaTime;

        // 마찰력 (일정한 감속)
        Vector2 frictionForce = frictionCoefficient * velocity.normalized;

        if (frictionForce.magnitude * Time.deltaTime >= velocity.magnitude)
        {
            velocity = Vector2.zero;
        }
        else
        {
            velocity -= frictionForce * Time.deltaTime;
        }

    }
    */

    void ApplyMovement()
    {
        transform.position += (Vector3)(velocity * Time.deltaTime);
    }
}
