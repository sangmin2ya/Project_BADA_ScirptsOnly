using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;
using System.Threading;
using System.Collections.Generic;

/// <summary>
/// 게임내의 UI 참조를 관리하는 매니저
/// </summary>
public class UIManager : MonoBehaviour
{
    [Header("게임내 UI")]
    [Tooltip("시간 텍스트")]
    public TMP_Text TimerText;
    [Tooltip("밤 표시 UI")]
    public List<Transform> NightIndicators;
    [Tooltip("현재 골드 텍스트")]
    public TMP_Text GoldText;
    [Tooltip("현재 바다 타입 텍스트")]
    public TMP_Text OceanTypeText;

    [Header("맵 관련 UI")]
    [Tooltip("맵 UI")]
    public Transform MapUI;
    [Tooltip("내부 맵 UI")]
    public Transform InnerMapUI;
    [Tooltip("비밀기지 표시 UI")]
    public List<GameObject> SecretBaseIndicators;
    [Tooltip("퀘스트 텍스트 UI")]
    public TMP_Text QuestText;

    // 스태틱 접근 인스턴스
    public static UIManager Instance { get; private set; }

    private void Awake()
    {
        // 싱글톤 설정
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public void InitializeUI()
    {
        // 타이머 UI 초기화 (세션씬)
        if (TimerText != null)
        {
            TimerText.SetText($"00:00");
        }
        // 골드 UI 초기화 (베이스캠프씬)
        if (GoldText != null)
        {
            GoldText.SetText($"골드: {BaseCampManager.Instance.Gold}G");
        }
        // 밤 표시 UI 초기화 (세션씬)
        if (NightIndicators != null)
        {
            foreach (var nightIndicator in NightIndicators)
            {
                nightIndicator.gameObject.SetActive(false);
            }
        }
        // 맵 UI 초기화 (세션씬)
        if (MapUI != null)
        {
            MapUI.gameObject.SetActive(false);
        }
        if (InnerMapUI != null)
        {
            InnerMapUI.gameObject.SetActive(false);
        }
    }
}