using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TestPlayerController : MonoBehaviour
{
    [Header("플레이어 이동 설정")]
    [SerializeField] private float moveSpeed = 5f;
    private int currentPlayerUpDirection = 0; // 현재 플레이어의 상단 방향을 나타내는 변수, 0: 상단, 1: 좌측, 2: 하단, 3: 우측
    private GameObject[] wallpSprites = new GameObject[4]; // 벽 스프라이트를 저장할 배열 
    private Transform playerTransform;
    private Vector2 movementInput;
    private Animator playerAnimator;
    private SpriteRenderer playerSpriteRenderer;


    void Start()
    {
        playerTransform = GetComponent<Transform>();
        playerAnimator = GetComponent<Animator>();
        playerSpriteRenderer = GetComponent<SpriteRenderer>();

        // wallpSprites 배열 초기화
        wallpSprites[0] = GameObject.Find("TopWall");
        wallpSprites[1] = GameObject.Find("LeftWall");
        wallpSprites[2] = GameObject.Find("BottomWall");
        wallpSprites[3] = GameObject.Find("RightWall");

        // 벽 스프라이트 초기화
        wallpSprites[0].SetActive(true); // 상단 벽 활성화
        wallpSprites[1].SetActive(false); // 좌측 벽 비활성화
        wallpSprites[2].SetActive(false); // 하단 벽 비활성화
        wallpSprites[3].SetActive(false); // 우측 벽 비활성화
    }

    void Update()
    {
        // 플레이어 인풋감지
        movementInput.x = Input.GetAxis("Horizontal");
        movementInput.y = Input.GetAxis("Vertical");

        if (movementInput.x > 0)
        {
            playerSpriteRenderer.flipX = false; // 오른쪽 이동 시 스프라이트를 정방향으로
        }
        else if (movementInput.x < 0)
        {
            playerSpriteRenderer.flipX = true; // 왼쪽 이동 시 스프라이트를 반전
        }

        // 인풋 벡터의 크기가 1을 초과하면 정규화
        if (movementInput.magnitude > 1f)
        {
            movementInput.Normalize();
        }

        // 플레이어 애니메이션 설정
        if (movementInput.magnitude > 0.1f)
        {
            playerAnimator.SetBool("Moving", true);
        }
        else
        {
            playerAnimator.SetBool("Moving", false);
        }

        // 플레이어 회전
        Rotate();
    }
    void FixedUpdate()
    {
        // 플레이어 이동
        Move();
    }
    /// <summary>
    /// 플레이어 이동, 이동 방향은 플레이어의 회전을 고려하여 계산
    /// </summary>
    private void Move()
    {
        // 플레이어 기준으로 이동 방향을 계산
        Vector2 moveDirection = new Vector2(movementInput.x, movementInput.y);
        // 이때 플레이어 Rotation을 고려하여 이동 방향을 회전
        playerTransform.position += (Vector3)(playerTransform.rotation * moveDirection) * moveSpeed * Time.fixedDeltaTime;
    }
    /// <summary>
    /// 플레이어 회전, Q키와 E키 / RT, LT 버튼으로 회전
    /// </summary>
    private void Rotate()
    {
        if (Input.GetKeyDown(KeyCode.Q) || Input.GetKeyDown(KeyCode.Joystick1Button5))
        {
            playerTransform.Rotate(0, 0, 90);
        }
        else if (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Joystick1Button4))
        {
            playerTransform.Rotate(0, 0, -90);
        }
        // 플레이어의 현재 상단 방향을 업데이트
        currentPlayerUpDirection = Mathf.RoundToInt(playerTransform.eulerAngles.z / 90f) % 4;
        // 벽 스프라이트를 회전
        RotateWallpSprites();
    }
    /// <summary>
    /// 플레이어의 현재 상단 방향에 따라 벽 스프라이트를 회전
    /// </summary>
    private void RotateWallpSprites()
    {
        // 플레이어의 현재 상단 방향에 따라 벽 스프라이트를 회전
        for (int i = 0; i < wallpSprites.Length; i++)
        {
            wallpSprites[i].SetActive(i == currentPlayerUpDirection);
        }
    }
}
