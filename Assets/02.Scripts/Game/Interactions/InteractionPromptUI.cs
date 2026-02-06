using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 상호작용 프롬프트 UI를 표시하는 컴포넌트 (서버 권한 기반)
/// </summary>
public class InteractionPromptUI : MonoBehaviour
{
    #region SerializeField

    [SerializeField] private Image inputIconImage;
    [SerializeField] private TextMeshProUGUI interactionText;
    [SerializeField] private float fadeSpeed = 5f;

    #endregion

    #region Private 필드

    private Vector3 _targetPosition;
    private bool _isVisible;
    private Camera _mainCamera;

    #endregion

    #region Public 및 프로퍼티

    public bool IsVisible => _isVisible;

    #endregion

    #region MonoBehaviour

    private void Awake()
    {
       
        _mainCamera = Camera.main;
        
        _isVisible = false;
    }

    private void LateUpdate()
    {
        // 타겟 위치로 이동
        if (_isVisible)
        {
            transform.position = _targetPosition;
        }
        
        // 카메라를 향하도록 회전
        if (_mainCamera != null)
        {
            transform.LookAt(transform.position + _mainCamera.transform.rotation * Vector3.forward,
                _mainCamera.transform.rotation * Vector3.up);
        }
    }

    #endregion

    #region Public 메서드

    /// <summary>
    /// 특정 위치에 상호작용 프롬프트를 표시합니다
    /// </summary>
    /// <param name="worldPosition">월드 위치</param>
    /// <param name="text">표시할 텍스트</param>
    /// <param name="icon">입력 아이콘 스프라이트</param>
    public void ShowAtPosition(Vector3 worldPosition, string text, Sprite icon)
    {
        _targetPosition = worldPosition;
        _isVisible = true;
        
        if (interactionText != null)
        {
            interactionText.text = text;
        }
        
        if (inputIconImage != null && icon != null)
        {
            inputIconImage.sprite = icon;
            inputIconImage.enabled = true;
        }
        else if (inputIconImage != null)
        {
            inputIconImage.enabled = false;
        }
        
        transform.position = _targetPosition;
    }

    /// <summary>
    /// 상호작용 프롬프트를 숨깁니다
    /// </summary>
    public void Hide()
    {
        _isVisible = false;
    }

    /// <summary>
    /// 입력 아이콘만 업데이트합니다 (입력 장치 변경 시)
    /// </summary>
    /// <param name="icon">새로운 입력 아이콘</param>
    public void UpdateIcon(Sprite icon)
    {
        if (inputIconImage != null && icon != null)
        {
            inputIconImage.sprite = icon;
            inputIconImage.enabled = true;
        }
    }

    #endregion
}