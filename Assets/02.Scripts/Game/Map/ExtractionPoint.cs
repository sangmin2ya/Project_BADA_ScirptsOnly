using System.Collections;
using Mirror;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 바다에서 탈출하여 베이스캠프로 돌아가기위한 지점
/// </summary>
public class ExtractionPoint : NetworkBehaviour
{
    [SerializeField] private float _extractionDuration = 3f;
    [SerializeField] private string _extractionTag = "Ship";
    [SerializeField] private Image _extractionProgressImage;
    private Coroutine _extractionCoroutine;

    void Awake()
    {
        _extractionProgressImage.fillAmount = 0f;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag(_extractionTag))
        {
            // 플레이어가 탈출 지점에 들어왔을 때 탈출 코루틴 시작
            _extractionCoroutine = StartCoroutine(ExtractionCoroutine(other.gameObject));
        }
    }
    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag(_extractionTag))
        {
            // 플레이어가 탈출 지점에서 나갔을 때 탈출 코루틴 중지
            if (_extractionCoroutine != null)
            {
                StopCoroutine(_extractionCoroutine);
                _extractionCoroutine = null;
            }
            RpcUpdateExtractionProgress(0f);
        }
    }

    IEnumerator ExtractionCoroutine(GameObject player)
    {
        float elapsedTime = 0f;

        while (elapsedTime < _extractionDuration)
        {
            // 플레이어가 탈출 지점에 머무르는 동안 시간 증가
            elapsedTime += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsedTime / _extractionDuration);
            RpcUpdateExtractionProgress(progress);
            yield return null;
        }
        // 탈출 완료 처리 - 베이스캠프로 돌아감
        GameNetworkManager.singleton.ReturnToBaseCamp();
    }

    #region 클라이언트 동기화

    [ClientRpc]
    private void RpcUpdateExtractionProgress(float progress)
    {
        if (_extractionProgressImage != null)
        {
            _extractionProgressImage.fillAmount = progress;
        }
    }

    #endregion
}