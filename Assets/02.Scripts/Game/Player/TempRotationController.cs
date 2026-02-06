using UnityEngine;

public class TempRotationController : MonoBehaviour
{
    private Camera _mainCamera;

    void Start()
    {
        _mainCamera = Camera.main;
    }

    void LateUpdate()
    {
        transform.rotation = Quaternion.Euler(0, 0, _mainCamera.transform.eulerAngles.z);
    }
}