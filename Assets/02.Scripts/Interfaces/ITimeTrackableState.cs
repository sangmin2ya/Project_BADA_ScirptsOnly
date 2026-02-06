/// <summary>
/// 이 상태가 시작되고 시작이 얼마나 지났는지 넘겨줘야하는 책임을 가짐
/// [조우석 - 2025.08.14]
/// </summary>
public interface ITimeTrackableState
{
    float Timer { get; }
}