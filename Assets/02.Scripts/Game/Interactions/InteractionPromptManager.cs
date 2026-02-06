using System.Collections.Generic;
using UnityEngine;
using Mirror;

/// <summary>
/// 상호작용 프롬프트 UI를 전역적으로 관리하는 싱글톤 클래스
/// [박상민 - 2025.11.20]
/// </summary>
public class InteractionPromptManager : NetworkSingleton<InteractionPromptManager>
{
    #region SerializeField

    [SerializeField] private InteractionPromptUI promptUIPrefab;
    [SerializeField] private InputIconData inputIconData;
    [SerializeField] private Transform uiContainer;
    [Tooltip("파괴된 오브젝트 정리 주기 (초)")]
    [SerializeField] private float cleanupInterval = 0.5f;

    #endregion

    #region Private 필드

    private Dictionary<uint, InteractionPromptUI> _activePrompts = new Dictionary<uint, InteractionPromptUI>();
    private ObjectPool<InteractionPromptUI> _promptPool;
    private bool _isUsingGamepad = false;
    private float _cleanupTimer = 0f;
    private List<uint> _keysToRemove = new List<uint>(); // GC 방지용 재사용 리스트

    #endregion

    #region MonoBehaviour

    protected override void Awake()
    {
        base.Awake();

        // UI 컨테이너가 없으면 생성
        if (uiContainer == null)
        {
            GameObject container = new GameObject("InteractionPromptsContainer");
            uiContainer = container.transform;
            uiContainer.SetParent(transform);
        }

        // 오브젝트 풀 초기화
        InitializePool();
        // 씬 전환 시에도 파괴되지 않도록 설정
        DontDestroyOnLoad(gameObject);
    }

    private void Update()
    {
        // 주기적으로 파괴된 오브젝트 정리
        _cleanupTimer += Time.deltaTime;
        if (_cleanupTimer >= cleanupInterval)
        {
            _cleanupTimer = 0f;
            CleanupDestroyedObjects();
        }
    }

    #endregion

    #region Public 메서드 (클라이언트 전용)

    /// <summary>
    /// 서버로부터 받은 상호작용 정보로 프롬프트를 표시합니다
    /// </summary>
    /// <param name="info">상호작용 정보</param>
    public void ShowPromptFromServer(InteractionInfo info)
    {
        if (!info.isInteractable)
        {
            // 상호작용 불가능하면 숨김
            HidePrompt(info.targetNetId);
            return;
        }

        // 이미 활성화된 프롬프트가 있으면 업데이트
        if (_activePrompts.ContainsKey(info.targetNetId))
        {
            UpdatePrompt(info);
            return;
        }

        // 풀에서 UI 가져오기
        InteractionPromptUI promptUI = _promptPool.Get();

        if (promptUI == null)
        {
            Debug.LogError("InteractionPromptManager: 풀에서 UI를 가져올 수 없습니다!");
            return;
        }

        // 아이콘 가져오기
        Sprite icon = GetIconForAction(info.actionName);

        // UI 표시
        promptUI.transform.SetParent(uiContainer);
        promptUI.ShowAtPosition(info.uiPosition, info.interactionText, icon);

        // 딕셔너리에 추가
        _activePrompts.Add(info.targetNetId, promptUI);
    }

    /// <summary>
    /// 상호작용 프롬프트를 숨깁니다
    /// </summary>
    /// <param name="targetId">타겟 오브젝트의 고유 ID</param>
    public void HidePrompt(uint targetId)
    {
        if (_activePrompts.TryGetValue(targetId, out InteractionPromptUI promptUI))
        {
            promptUI.Hide();
            _promptPool.Return(promptUI);
            _activePrompts.Remove(targetId);
        }
    }

    /// <summary>
    /// 모든 프롬프트를 숨깁니다
    /// </summary>
    public void HideAllPrompts()
    {
        foreach (var kvp in _activePrompts)
        {
            kvp.Value.Hide();
            _promptPool.Return(kvp.Value);
        }

        _activePrompts.Clear();
    }

    /// <summary>
    /// 입력 장치가 변경되었을 때 호출됩니다
    /// </summary>
    /// <param name="isUsingGamepad">게임패드 사용 여부</param>
    public void OnInputDeviceChanged(bool isUsingGamepad)
    {
        if (_isUsingGamepad == isUsingGamepad)
        {
            return;
        }

        _isUsingGamepad = isUsingGamepad;
    }

    #endregion

    #region Private 메서드

    /// <summary>
    /// 오브젝트 풀을 초기화합니다
    /// </summary>
    private void InitializePool()
    {
        if (promptUIPrefab == null)
        {
            Debug.LogError("InteractionPromptManager: promptUIPrefab이 설정되지 않았습니다!");
            return;
        }

        _promptPool = new ObjectPool<InteractionPromptUI>(
            createFunc: () => Instantiate(promptUIPrefab, uiContainer),
            actionOnGet: (ui) => ui.gameObject.SetActive(true),
            actionOnReturn: (ui) => ui.gameObject.SetActive(false),
            initialSize: 10
        );
    }

    /// <summary>
    /// 파괴된 오브젝트의 프롬프트를 정리합니다
    /// </summary>
    private void CleanupDestroyedObjects()
    {
        if (_activePrompts.Count == 0)
        {
            return;
        }

        _keysToRemove.Clear();

        // 파괴된 오브젝트 찾기
        foreach (var kvp in _activePrompts)
        {
            uint netId = kvp.Key;
            
            // NetworkClient.spawned에서 해당 netId의 오브젝트가 존재하는지 확인
            if (!NetworkClient.spawned.ContainsKey(netId))
            {
                _keysToRemove.Add(netId);
            }
        }

        // 파괴된 오브젝트의 프롬프트 제거
        foreach (uint netId in _keysToRemove)
        {
            if (_activePrompts.TryGetValue(netId, out InteractionPromptUI promptUI))
            {
                promptUI.Hide();
                _promptPool.Return(promptUI);
                _activePrompts.Remove(netId);
            }
        }
    }

    /// <summary>
    /// 프롬프트를 업데이트합니다
    /// </summary>
    /// <param name="info">새로운 상호작용 정보</param>
    private void UpdatePrompt(InteractionInfo info)
    {
        if (_activePrompts.TryGetValue(info.targetNetId, out InteractionPromptUI promptUI))
        {
            Sprite icon = GetIconForAction(info.actionName);
            promptUI.ShowAtPosition(info.uiPosition, info.interactionText, icon);
        }
    }

    /// <summary>
    /// 액션 이름에 해당하는 아이콘을 가져옵니다
    /// </summary>
    /// <param name="actionName">액션 이름</param>
    /// <returns>아이콘 스프라이트</returns>
    private Sprite GetIconForAction(string actionName)
    {
        if (inputIconData == null)
        {
            Debug.LogWarning("InteractionPromptManager: InputIconData가 설정되지 않았습니다!");
            return null;
        }

        return inputIconData.GetIconForAction(actionName, _isUsingGamepad);
    }

    #endregion
}

/// <summary>
/// 간단한 오브젝트 풀 구현
/// </summary>
/// <typeparam name="T">풀링할 오브젝트 타입</typeparam>
public class ObjectPool<T> where T : Component
{
    private readonly Stack<T> _pool = new Stack<T>();
    private readonly System.Func<T> _createFunc;
    private readonly System.Action<T> _actionOnGet;
    private readonly System.Action<T> _actionOnReturn;

    public ObjectPool(System.Func<T> createFunc, System.Action<T> actionOnGet, System.Action<T> actionOnReturn, int initialSize = 10)
    {
        _createFunc = createFunc;
        _actionOnGet = actionOnGet;
        _actionOnReturn = actionOnReturn;

        // 초기 오브젝트 생성
        for (int i = 0; i < initialSize; i++)
        {
            T obj = _createFunc();
            _actionOnReturn?.Invoke(obj);
            _pool.Push(obj);
        }
    }

    public T Get()
    {
        T obj = _pool.Count > 0 ? _pool.Pop() : _createFunc();
        _actionOnGet?.Invoke(obj);
        return obj;
    }

    public void Return(T obj)
    {
        _actionOnReturn?.Invoke(obj);
        _pool.Push(obj);
    }
}