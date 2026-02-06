using Mirror;
using UnityEngine;
using UnityEngine.UI;

public class ExtractionManager : NetworkBehaviour
{
    [Header("탈출 설정")]
    [SerializeField] private GameObject _extractionUI;
    [SerializeField] private Image _extractionProgressImage;
    [SerializeField] private float _extractionDuration = 5f;

    private float _currentExtractionTime = 0f;
    private bool _isExtracting = false;

    void Awake()
    {
        _extractionProgressImage.fillAmount = 0f;
        _extractionUI.SetActive(false);
    }

    void Update()
    {
        if (!isServer)
        {
            return;
        }
        if (_isExtracting)
        {
            _currentExtractionTime += Time.deltaTime;
            float progress = Mathf.Clamp01(_currentExtractionTime / _extractionDuration);
            RpcUpdateExtractionProgress(progress);

            if (_currentExtractionTime >= _extractionDuration)
            {
                // 배에있는 물건 BaseCampManager로 옮기기
                GetComponent<ShipController>().SaveStoredItems();
                // 탈출 완료 처리 - 베이스캠프로 돌아감
                GameNetworkManager.singleton.ReturnToBaseCamp();
                _isExtracting = false;
            }
        }
    }

    public void StartExtraction()
    {
        _isExtracting = true;
        _currentExtractionTime = 0f;
    }
    public void StopExtraction()
    {
        _isExtracting = false;
        _currentExtractionTime = 0f;
        RpcUpdateExtractionProgress(0f);
    }

    [ClientRpc]
    private void RpcUpdateExtractionProgress(float progress)
    {
        if (progress == 0f)
        {
            _extractionUI.SetActive(false);
        }
        else
        {
            _extractionUI.SetActive(true);
            _extractionProgressImage.fillAmount = progress;
        }
    }
}