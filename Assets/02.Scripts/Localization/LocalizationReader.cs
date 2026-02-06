using UnityEngine;
using System.ComponentModel;
using UnityEngine.Rendering;
using TMPro;
using System;
using System.Collections.Generic;
/// <summary>
/// 번역본을 CSV에서 읽어와서 게임에서 불러올수 있도록 저장하는 클래스
/// /// [박상민 - 2025.06.11]
/// </summary>
public class LocalizationReader : Singleton<LocalizationReader>
{
    [Header("<color=green>현재 언어")]
    [Tooltip("게임에서 현재 사용중인 언어 타입")]
    [SerializeField]
    private LanguageType _currentLanguage;

    [Header("텍스트 데이터")]
    [Tooltip("번역 데이터set (언어 타입 - 텍스트 코드 - 실제 텍스트)")]
    [ReadOnly(true)] private SerializedDictionary<LanguageType, SerializedDictionary<string, string>> _localizationData = new SerializedDictionary<LanguageType, SerializedDictionary<string, string>>();

    [Header("언어별 폰트설정")]
    [Tooltip("언어별 폰트 설정 (언어타입 - 폰트에셋)")]
    [ReadOnly(true)] private SerializedDictionary<LanguageType, TMP_FontAsset> _languageFonts = new SerializedDictionary<LanguageType, TMP_FontAsset>();

    [Header("CSV 파일 경로")]
    [Tooltip("텍스트 데이터 CSV 파일 경로 (Resources 폴더 기준)")]
    private const string _textDataCSVPath = "Localization/TextData";

    [Tooltip("폰트 설정 CSV 파일 경로 (Resources 폴더 기준)")]
    private const string _fontDataCSVPath = "Localization/FontData";

    protected override void Awake()
    {
        base.Awake();

        // 시스템 언어설정
        _currentLanguage = Application.systemLanguage switch
        {
            SystemLanguage.Korean => LanguageType.한국어,
            SystemLanguage.English => LanguageType.English,
            SystemLanguage.Japanese => LanguageType.日本語,
            _ => LanguageType.English,
        };

        // 초기화
        LoadTextDataFromCSV();
        LoadFontDataFromCSV();
    }

    /// <summary>
    /// CSV에서 텍스트 데이터를 읽어와서 _localizationData에 저장
    /// </summary>
    private void LoadTextDataFromCSV()
    {
        TextAsset csvFile = Resources.Load<TextAsset>(_textDataCSVPath);
        if (csvFile == null)
        {
            Debug.LogError($"CSV 파일을 찾을 수 없습니다: {_textDataCSVPath}");
            return;
        }

        string[] lines = csvFile.text.Split('\n');
        if (lines.Length < 2)
        {
            Debug.LogError("CSV 파일이 비어있거나 헤더만 있습니다.");
            return;
        }

        // 첫 번째 줄에서 언어 헤더 파싱 (code, 한국어, English, 日本語)
        string[] headers = ParseCSVLine(lines[0]);
        List<LanguageType> languages = new List<LanguageType>();

        // 헤더에서 언어 타입 추출 (첫 번째 컬럼은 code 제외)
        for (int i = 1; i < headers.Length; i++)
        {
            if (Enum.TryParse<LanguageType>(headers[i], out LanguageType langType))
            {
                languages.Add(langType);
                if (!_localizationData.ContainsKey(langType))
                {
                    _localizationData[langType] = new SerializedDictionary<string, string>();
                }
            }
        }

        // 데이터 행 파싱
        for (int i = 1; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (string.IsNullOrEmpty(line)) continue;

            string[] values = ParseCSVLine(line);
            if (values.Length < 2) continue;

            string code = values[0].Trim();
            if (string.IsNullOrEmpty(code)) continue;

            // 각 언어별로 텍스트 저장
            for (int j = 0; j < languages.Count && j + 1 < values.Length; j++)
            {
                LanguageType lang = languages[j];
                string text = values[j + 1].Trim();

                if (!string.IsNullOrEmpty(text))
                {
                    _localizationData[lang][code] = text;
                }
            }
        }

        Debug.Log($"텍스트 데이터 로드 완료: {languages.Count}개 언어, 총 {_localizationData[languages[0]].Count}개 키");
    }

    /// <summary>
    /// CSV에서 폰트 데이터를 읽어와서 _languageFonts에 저장
    /// </summary>
    private void LoadFontDataFromCSV()
    {
        TextAsset csvFile = Resources.Load<TextAsset>(_fontDataCSVPath);
        if (csvFile == null)
        {
            Debug.LogWarning($"폰트 CSV 파일을 찾을 수 없습니다: {_fontDataCSVPath}, 기본 폰트를 사용합니다.");
            InitializeDefaultFonts();
            return;
        }

        string[] lines = csvFile.text.Split('\n');
        if (lines.Length < 2)
        {
            Debug.LogWarning("폰트 CSV 파일이 비어있습니다. 기본 폰트를 사용합니다.");
            InitializeDefaultFonts();
            return;
        }

        // 데이터 행 파싱 (첫 번째 줄은 헤더이므로 제외)
        for (int i = 1; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (string.IsNullOrEmpty(line)) continue;

            string[] values = ParseCSVLine(line);
            if (values.Length < 2) continue;

            string languageStr = values[0].Trim();
            string fontPath = values[1].Trim();

            if (Enum.TryParse<LanguageType>(languageStr, out LanguageType langType))
            {
                TMP_FontAsset font = Resources.Load<TMP_FontAsset>(fontPath);
                if (font != null)
                {
                    _languageFonts[langType] = font;
                    Debug.Log($"폰트 로드 성공: {langType} -> {fontPath}");
                }
                else
                {
                    Debug.LogWarning($"폰트를 찾을 수 없습니다: {fontPath}");
                }
            }
        }

        // 로드되지 않은 언어는 기본 폰트 설정
        InitializeDefaultFonts();
    }

    /// <summary>
    /// 기본 폰트 초기화
    /// </summary>
    private void InitializeDefaultFonts()
    {
        foreach (LanguageType language in Enum.GetValues(typeof(LanguageType)))
        {
            if (!_languageFonts.ContainsKey(language) || _languageFonts[language] == null)
            {
                // 기본 폰트 경로 설정 (필요에 따라 수정)
                string defaultFontPath = language switch
                {
                    LanguageType.한국어 => "Fonts/NotoSansKR-SemiBold SDF",
                    LanguageType.日本語 => "Fonts/NotoSansJP-Regular SDF",
                    _ => "Fonts/NotoSansKR-SemiBold SDF"
                };

                TMP_FontAsset defaultFont = Resources.Load<TMP_FontAsset>(defaultFontPath);
                _languageFonts[language] = defaultFont;
            }
        }
    }
    /// <summary>
    /// CSV 한 줄을 파싱 (쉼표로 구분, 따옴표 처리)
    /// </summary>
    private string[] ParseCSVLine(string line)
    {
        List<string> result = new List<string>();
        bool inQuotes = false;
        string currentField = "";

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];

            if (c == '"')
            {
                inQuotes = !inQuotes;
            }
            else if (c == ',' && !inQuotes)
            {
                result.Add(currentField);
                currentField = "";
            }
            else
            {
                currentField += c;
            }
        }
        result.Add(currentField);
        return result.ToArray();
    }

    /// <summary>
    /// 언어 변경
    /// </summary>
    public void ChangeLanguage(LanguageType newLanguage)
    {
        if (_localizationData.ContainsKey(newLanguage))
        {
            _currentLanguage = newLanguage;
            // 언어 변경 이벤트 발생 (필요시)
            OnLanguageChanged?.Invoke();

            Debug.Log($"언어가 변경되었습니다: {newLanguage}");
        }
        else
        {
            Debug.LogError($"언어 '{newLanguage}'에 대한 데이터가 없습니다.");
        }
    }
    /// <summary>
    /// 버튼용 테스트 언어변경 코드
    /// </summary>
    public void ChangeLanguage()
    {
        // 현재 언어를 순환하여 변경
        LanguageType[] languages = (LanguageType[])Enum.GetValues(typeof(LanguageType));
        int currentIndex = Array.IndexOf(languages, _currentLanguage);
        int nextIndex = (currentIndex + 1) % languages.Length;

        ChangeLanguage(languages[nextIndex]);
    }

    /// <summary>
    /// 언어 변경 이벤트
    /// </summary>
    public event System.Action OnLanguageChanged;

    /// <summary>
    /// 현재 언어 가져오기
    /// </summary>
    public LanguageType CurrentLanguage => _currentLanguage;

    /// <summary>
    /// 현재 언어의 폰트 가져오기
    /// </summary>
    public TMP_FontAsset CurrentFont => _languageFonts.ContainsKey(_currentLanguage) ? _languageFonts[_currentLanguage] : null;

    /// <summary>
    /// 현재 언어의 텍스트 가져오기
    /// </summary>
    public string GetText(string code)
    {
        if (_localizationData.ContainsKey(_currentLanguage) && _localizationData[_currentLanguage].TryGetValue(code, out string value))
        {
            return value;
        }
        else
        {
            Debug.LogWarning($"Code '{code}' not found in language '{_currentLanguage}'.");
            return code;
        }
    }
}