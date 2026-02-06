using UnityEngine;
using System.Collections.Generic;
using Mirror;

// 벽의 그림자를 고려한 Stencil FOV
public class FOVStencil : NetworkBehaviour
{
    [Header("FOV Settings")]
    public float initialViewRadius = 20f;
    public int rayCount = 100;
    public LayerMask wallLayer = 8;
    [SyncVar] private float currentViewRadius = 20;

    [Header("Materials")]
    public Material fovMaterial;

    private Mesh shadowMesh;
    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;

    void Start()
    {
        if (isLocalPlayer)
        {
            CreateFOVMesh();
            currentViewRadius = initialViewRadius;
        }
    }

    void Update()
    {
        if (isLocalPlayer)
        {
            UpdateFOVWithShadows();
        }
    }

    void CreateFOVMesh()
    {
        GameObject fovObj = new GameObject("FOV with Shadows");
        fovObj.transform.parent = transform;
        fovObj.transform.localPosition = Vector3.zero;

        meshFilter = fovObj.AddComponent<MeshFilter>();
        meshRenderer = fovObj.AddComponent<MeshRenderer>();
        meshRenderer.material = fovMaterial;
        meshRenderer.sortingOrder = -100;

        shadowMesh = new Mesh();
        shadowMesh.name = "Shadow FOV";
        meshFilter.mesh = shadowMesh;
    }

    void UpdateFOVWithShadows()
    {
        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();

        vertices.Add(Vector3.zero);

        float angleStep = 360f / rayCount;

        for (int i = 0; i <= rayCount; i++)
        {
            float angle = i * angleStep * Mathf.Deg2Rad;
            Vector2 rayDirection = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));

            Vector3 rayEndPoint = CastRayForShadow(rayDirection);
            vertices.Add(transform.InverseTransformPoint(rayEndPoint));

            if (i < rayCount)
            {
                triangles.Add(0);        // 중심점
                triangles.Add(i + 2);    // 현재 점
                triangles.Add(i + 1);    // 다음 점
            }
        }

        shadowMesh.Clear();
        shadowMesh.vertices = vertices.ToArray();
        shadowMesh.triangles = triangles.ToArray();
        shadowMesh.RecalculateNormals();
    }

    Vector3 CastRayForShadow(Vector2 direction)
    {
        // 플레이어 위치에서 해당 방향으로 레이캐스트
        RaycastHit2D hit = Physics2D.Raycast(transform.position, direction, currentViewRadius, wallLayer);

        if (hit.collider != null)
        {
            return hit.point;
        }
        else
        {
            return (Vector2)transform.position + direction * currentViewRadius;
        }
    }

    /// <summary>
    /// 플레이어의 시야 범위를 설정하는 변수
    /// </summary>
    /// <param name="radius"></param>
    [Server]
    public void SetViewRadius(float radius)
    {
        // SyncVar 변수이기에 모든 클라이언트에 값 적용
        currentViewRadius = radius;
    }

    public void ResetViewRadius()
    {
        SetViewRadius(initialViewRadius);
    }
}