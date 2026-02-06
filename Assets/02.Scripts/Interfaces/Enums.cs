/// <summary>
/// 게임에서 사용하는 언어 타입
/// [박상민 - 2025.06.11]
/// </summary>
public enum LanguageType
{
    한국어, English, 日本語
}
/// <summary>
/// 게임 상태 타입
/// [박상민 - 2025.06.13]
/// </summary>
public enum GameStateType
{
    대기, 게임중, 게임종료
}
/// <summary>
/// LTD 오브젝트의 타입
/// [박상민 - 2025.06.11]
/// </summary>
public enum LTDObjectType
{
    모듈박스, 고철, 플레이어, 몬스터, 수리도구, 포탄, 그물망, 근접무기, 원거리무기, 가방, 안개등, 기타
}
/// <summary>
/// 플레이어의 상태 타입
/// [박상민 - 2025.06.12]
/// </summary>
public enum PlayerStateType
{
    일반, 기절, 사망
}
/// <summary>
/// 모듈의 타입
/// [박상민 - 2025.06.18]
/// </summary>
public enum ModuleType
{
    없음, 조향, 추진, 함포, 드릴, 창고, 방어막, 포탄보관함, 수리키트보관함, 크레인
}
/// <summary>
/// LTD 오브젝트의 크기
/// [박상민 - 2025.06.18]
/// </summary>
public enum LTDObjectSize
{
    소, 중, 대
}
/// <summary>
/// 고철의 타입
/// [박상민 - 2025.10.02]
/// </summary>
public enum ScrapType
{
    일반고철1, 일반고철2, 일반고철3, 특수고철
}
/// <summary>
/// 플레이어가 도킹후 진입 가능한 오브젝트 타입
/// [박상민 - 2025.07.09]
/// </summary>
public enum EnterableType
{
    적함, 건물, 난파선
}
/// <summary>
/// 방어막 상태
/// [김도형 - 2025.07.28]
/// </summary>
public enum ShieldState
{
    Inactive,   // 비활성
    Parry,      // 패링 단계
    Defense     // 방어 단계
}
/// <summary>
/// 적 오브젝트가 타겟을 감지하는 우선 순위
/// [조우석 - 2025.08.07.]
/// </summary>
public enum TargetingPriority
{
    가까운대상,
    체력낮은대상,
    무작위대상
}
/// <summary>
/// Physics Handler를 가지는 오브젝트의 타입
/// [조우석 - 2025.08.07.]
/// </summary>
public enum PhysicsObjectType
{
    없음,
    플레이어,
    적_내부,
    LTD
}
/// <summary>
/// 내부 적 스턴 상태
/// [조우석 - 2025.08.07.]
/// </summary>
public enum InternalEnemyStunState
{
    기본,
    스턴,
    사망
}
/// <summary>
/// 적 함선 타입
/// [박상민 - 2025.08.20]
/// </summary>
public enum EnemyShipType
{
    미니함선 = 0,
    자코함선 = 1,
    전투함선 = 2,
    잠수함 = 3,
    //도주함선 = 1,
}
/// <summary>
/// 적 캐릭터 타입
/// [박상민 - 2025.10.21]
/// </summary>
public enum EnemyCharacterType
{
    케넨, 직스, 사이온
}
/// <summary>
/// 세션 바다 타입
/// [박상민 - 2025.10.30]
/// </summary>
public enum OceanType
{
    잔잔한바다 = 0,
    안개낀바다 = 1,
    얼어붙은바다 = 2
}
/// <summary>
/// 맵 구역 타입
/// [박상민 - 2025.11.12]
/// </summary>
public enum AreaType
{
    일반구역 = 0,
    위험구역 = 1,
    초위험구역 = 2,
    안전구역 = 3
}
/// <summary>
/// 시간대 타입
/// [박상민 - 2025.11.12]
/// </summary>
public enum TimeType
{
    낮, 황혼, 밤, 심야
}