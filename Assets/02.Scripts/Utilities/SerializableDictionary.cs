using System;
using System.Collections.Generic;
using UnityEngine;

// TKey, TValue 제네릭 타입을 받는 SerializableDictionary 클래스
// ISerializationCallbackReceiver를 구현하여 직렬화/역직렬화 콜백을 받음
[Serializable]
public class SerializableDictionary<TKey, TValue> : ISerializationCallbackReceiver
{
    // 1. 에디터/저장용 데이터: Key-Value 쌍을 담는 리스트
    // Unity는 List를 직렬화할 수 있으므로, [SerializeField]를 사용
    [SerializeField]
    private List<SerializableKeyValuePair> list = new List<SerializableKeyValuePair>();

    // Key-Value 쌍을 저장하기 위한 내부 구조체. 이 역시 [Serializable]이어야 함
    [Serializable]
    private struct SerializableKeyValuePair
    {
        public TKey key;
        public TValue value;
    }

    // 2. 런타임용 데이터: 실제 Dictionary
    // 빠른 조회를 위해 사용하며, 직렬화 대상이 아니므로 [NonSerialized] 처리
    [NonSerialized]
    private Dictionary<TKey, TValue> dictionary = new Dictionary<TKey, TValue>();

    #region ISerializationCallbackReceiver 구현

    // 직렬화(저장) 직전에 호출됨
    public void OnBeforeSerialize()
    {
        // 런타임 Dictionary의 데이터를 직렬화용 List로 복사
        list.Clear();
        foreach (KeyValuePair<TKey, TValue> pair in dictionary)
        {
            list.Add(new SerializableKeyValuePair { key = pair.Key, value = pair.Value });
        }
    }

    // 역직렬화(로딩) 직후에 호출됨
    public void OnAfterDeserialize()
    {
        // 직렬화된 List의 데이터를 런타임 Dictionary로 복원
        dictionary = new Dictionary<TKey, TValue>();
        foreach (var pair in list)
        {
            // 인스펙터에서 중복 키를 입력했을 경우를 대비
            if (!dictionary.ContainsKey(pair.key))
            {
                dictionary.Add(pair.key, pair.value);
            }
            else
            {
                Debug.LogWarning($"SerializableDictionary: 키 '{pair.key}'가 중복되어 마지막 값으로 덮어씁니다.");
                dictionary[pair.key] = pair.value;
            }
        }
    }

    #endregion

    #region Dictionary 기능 구현 (Wrapper)

    // Dictionary의 주요 기능들을 래핑하여 외부에서 쉽게 사용하도록 함

    // Indexer
    public TValue this[TKey key]
    {
        get => dictionary[key];
        set => dictionary[key] = value;
    }

    // 메서드
    public void Add(TKey key, TValue value) => dictionary.Add(key, value);
    public bool Remove(TKey key) => dictionary.Remove(key);
    public bool ContainsKey(TKey key) => dictionary.ContainsKey(key);
    public bool TryGetValue(TKey key, out TValue value) => dictionary.TryGetValue(key, out value);
    public void Clear() => dictionary.Clear();

    // 속성
    public int Count => dictionary.Count;
    public ICollection<TKey> Keys => dictionary.Keys;
    public ICollection<TValue> Values => dictionary.Values;
    public Dictionary<TKey, TValue>.Enumerator GetEnumerator() => dictionary.GetEnumerator();

    #endregion
}