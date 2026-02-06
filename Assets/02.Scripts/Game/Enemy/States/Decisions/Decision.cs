using UnityEngine;

/// <summary>
/// AI가 상태를 전환할지 결정하는 로직의 기반이 되는 ScriptableObject
/// [조우석 - 2025.08.11.]
/// </summary>\
[CreateAssetMenu(fileName = "New Decision", menuName = "ScriptableObjects/Enemy/Decisions/Decision")]
public abstract class Decision : ScriptableObject
{
    /// <summary>
    /// 이 Decision이 참인지 거짓인지 판단
    /// </summary>
    /// <param name="controller">판단에 필요한 정보를 가진 컨트롤러</param>
    /// <returns>조건 충족 시 true</returns>
    public abstract bool Decide(IEnemyController controller);
}