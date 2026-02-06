using UnityEngine;
using TMPro;
/// <summary>
/// 키를 통해서 현재 언어의 텍스트를 로드하는 클래스
/// /// [박상민 - 2025.06.11]
/// </summary>
[RequireComponent(typeof(TMP_Text))]
public class TextLoader : MonoBehaviour
{
    [Header("<color=yellow>텍스트 코드")]
    [Tooltip("로컬라이즈할 텍스트의 키")]
    [SerializeField] private string _textCode;
    private TMP_Text _textComponent;
    private void Start()
    {
        // 언어변경에 따른 이벤트 구독
        LocalizationReader.Instance.OnLanguageChanged += LoadText;
        _textComponent = GetComponent<TMP_Text>();
        LoadText();
    }

    /// <summary>
    /// 현재 언어에 맞는 텍스트를 로드
    /// </summary>
    private void LoadText()
    {
        if (string.IsNullOrEmpty(_textCode))
        {
            Debug.LogError("텍스트 코드가 설정되지 않았습니다.");
            return;
        }
        // 폰트를 언어에 맞게 설정
        TMP_FontAsset fontAsset = LocalizationReader.Instance.CurrentFont;
        if (fontAsset != null)
        {
            _textComponent.font = fontAsset;
        }
        else
        {
            Debug.LogWarning($"'{LocalizationReader.Instance.CurrentLanguage}' 언어에 대한 폰트가 설정되지 않았습니다.");
        }
        // 현재 언어에 맞는 텍스트를 로드
        string localizedText = LocalizationReader.Instance.GetText(_textCode);
        if (localizedText != null)
        {
            _textComponent.text = localizedText;
        }
        else
        {
            Debug.LogWarning($"'{_textCode}'에 대한 번역이 없습니다.");
        }
    }
}