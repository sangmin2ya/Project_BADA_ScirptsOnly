using Mirror;
using UnityEngine;
using UnityEngine.UI;

public class TeleportPoint : NetworkBehaviour
{
    public Transform teleportPoint;
    public float teleportDuration = 3.0f;
    [SerializeField] private Image _teleportProgressBar = null;


    public void InitTeleportPoint(Transform transform)
    {
        Debug.Log("텔레포트 포인트 초기화: " + transform.name);
        teleportPoint = transform;
        gameObject.SetActive(false); // 연결되면 텔레포트 포인트 비활성화
        _teleportProgressBar.fillAmount = 0f;
    }

    [TargetRpc]
    public void RpcUpdateTeleportProgressBar(NetworkConnection target, float progress)
    {
        if (_teleportProgressBar != null)
        {
            _teleportProgressBar.fillAmount = progress;
            if (progress >= 1f)
            {
                _teleportProgressBar.transform.parent.gameObject.SetActive(false);
            }
            else if (progress <= 0f)
            {
                _teleportProgressBar.transform.parent.gameObject.SetActive(false);
            }
            else
            {
                _teleportProgressBar.transform.parent.gameObject.SetActive(true);
            }
        }
    }
}