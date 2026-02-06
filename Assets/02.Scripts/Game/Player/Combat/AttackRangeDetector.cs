using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 공격 범위 판정 인터페이스 - 다양한 판정 방식을 지원하기 위한 추상화
/// [작성자 - 2025.11.13]
/// </summary>
public interface IAttackRangeDetector
{
    /// <summary>
    /// 공격 범위 내의 적을 감지
    /// </summary>
    /// <param name="attackOrigin">공격 시작 위치</param>
    /// <param name="attackDirection">공격 방향</param>
    /// <param name="range">공격 범위</param>
    /// <param name="layerMask">감지할 레이어</param>
    /// <returns>감지된 콜라이더 리스트</returns>
    List<Collider2D> DetectTargets(Vector2 attackOrigin, Vector2 attackDirection, float range, LayerMask layerMask);

    /// <summary>
    /// 공격 범위를 Scene 뷰에 그리기 (디버그용)
    /// </summary>
    void DrawGizmos(Vector2 origin, Vector2 direction, float range, Color color);
}

/// <summary>
/// 부채꼴 범위 판정 - OverlapCircle + 각도 필터링 방식 (성능 우선)
/// [작성자 - 2025.11.13]
/// </summary>
public class ArcRangeDetector : IAttackRangeDetector
{
    private float _arcAngle;

    public ArcRangeDetector(float arcAngle)
    {
        _arcAngle = arcAngle;
    }

    /// <summary>
    /// 부채꼴 범위 내 타겟 감지
    /// </summary>
    public List<Collider2D> DetectTargets(Vector2 attackOrigin, Vector2 attackDirection, float range, LayerMask layerMask)
    {
        List<Collider2D> hitTargets = new List<Collider2D>();

        // 1단계: 원형 범위로 빠르게 걸러내기 (성능 최적화)
        Collider2D[] colliders = Physics2D.OverlapCircleAll(attackOrigin, range, layerMask);

        // 2단계: 각도로 정밀 필터링
        float halfArc = _arcAngle * 0.5f;
        foreach (Collider2D col in colliders)
        {
            Vector2 dirToTarget = ((Vector2)col.transform.position - attackOrigin).normalized;
            float angle = Vector2.Angle(attackDirection, dirToTarget);

            if (angle <= halfArc)
            {
                hitTargets.Add(col);
            }
        }

        return hitTargets;
    }

    /// <summary>
    /// 부채꼴 범위를 Gizmo로 그리기
    /// </summary>
    public void DrawGizmos(Vector2 origin, Vector2 direction, float range, Color color)
    {
        Gizmos.color = color;

        // 중심선
        Gizmos.DrawLine(origin, origin + direction * range);

        // 부채꼴 외곽선
        float halfArc = _arcAngle * 0.5f;
        int segments = 20;

        Vector3 previousPoint = origin + (Vector2)(Quaternion.Euler(0, 0, -halfArc) * direction) * range;
        for (int i = 1; i <= segments; i++)
        {
            float currentAngle = -halfArc + (_arcAngle / segments) * i;
            Vector3 currentPoint = origin + (Vector2)(Quaternion.Euler(0, 0, currentAngle) * direction) * range;
            Gizmos.DrawLine(previousPoint, currentPoint);
            previousPoint = currentPoint;
        }

        // 부채꼴 양 끝선
        Gizmos.DrawLine(origin, origin + (Vector2)(Quaternion.Euler(0, 0, -halfArc) * direction) * range);
        Gizmos.DrawLine(origin, origin + (Vector2)(Quaternion.Euler(0, 0, halfArc) * direction) * range);

        // 원형 범위 표시 (반투명)
        Color circleColor = color;
        circleColor.a = 0.3f;
        Gizmos.color = circleColor;
        DrawWireCircle(origin, range, 32);
    }

    private void DrawWireCircle(Vector2 center, float radius, int segments)
    {
        float angleStep = 360f / segments;
        Vector3 previousPoint = center + new Vector2(radius, 0);

        for (int i = 1; i <= segments; i++)
        {
            float angle = angleStep * i * Mathf.Deg2Rad;
            Vector3 currentPoint = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            Gizmos.DrawLine(previousPoint, currentPoint);
            previousPoint = currentPoint;
        }
    }
}

/// <summary>
/// 박스 범위 판정 - BoxCast 방식 (정확도 우선)
/// 향후 확장용으로 준비된 클래스
/// </summary>
public class BoxRangeDetector : IAttackRangeDetector
{
    private Vector2 _boxSize;

    public BoxRangeDetector(Vector2 boxSize)
    {
        _boxSize = boxSize;
    }

    public List<Collider2D> DetectTargets(Vector2 attackOrigin, Vector2 attackDirection, float range, LayerMask layerMask)
    {
        List<Collider2D> hitTargets = new List<Collider2D>();

        float angle = Mathf.Atan2(attackDirection.y, attackDirection.x) * Mathf.Rad2Deg;
        RaycastHit2D[] hits = Physics2D.BoxCastAll(attackOrigin, _boxSize, angle, attackDirection, range, layerMask);

        foreach (RaycastHit2D hit in hits)
        {
            if (hit.collider != null)
            {
                hitTargets.Add(hit.collider);
            }
        }

        return hitTargets;
    }

    public void DrawGizmos(Vector2 origin, Vector2 direction, float range, Color color)
    {
        Gizmos.color = color;

        float angle = Mathf.Atan2(direction.y, direction.x);
        Vector2 endPoint = origin + direction * range;

        // 박스 4개 코너 계산
        Vector2 perpendicular = new Vector2(-direction.y, direction.x).normalized;
        Vector2 halfExtent = perpendicular * (_boxSize.y * 0.5f);

        Vector2 corner1 = origin + halfExtent;
        Vector2 corner2 = origin - halfExtent;
        Vector2 corner3 = endPoint + halfExtent;
        Vector2 corner4 = endPoint - halfExtent;

        // 박스 그리기
        Gizmos.DrawLine(corner1, corner2);
        Gizmos.DrawLine(corner2, corner4);
        Gizmos.DrawLine(corner4, corner3);
        Gizmos.DrawLine(corner3, corner1);
    }
}

/// <summary>
/// 커스텀 폴리곤 범위 판정
/// 완전히 자유로운 형태의 공격 범위를 위한 클래스
/// </summary>
public class CustomPolygonRangeDetector : IAttackRangeDetector
{
    private Vector2[] _localPolygonPoints;  // 로컬 좌표계 폴리곤 점들

    public CustomPolygonRangeDetector(Vector2[] polygonPoints)
    {
        _localPolygonPoints = polygonPoints;
    }

    public List<Collider2D> DetectTargets(Vector2 attackOrigin, Vector2 attackDirection, float range, LayerMask layerMask)
    {
        List<Collider2D> hitTargets = new List<Collider2D>();

        // 방향에 따라 폴리곤 회전
        float angle = Mathf.Atan2(attackDirection.y, attackDirection.x);
        Vector2[] worldPoints = new Vector2[_localPolygonPoints.Length];

        for (int i = 0; i < _localPolygonPoints.Length; i++)
        {
            Vector2 rotated = RotatePoint(_localPolygonPoints[i] * range, angle);
            worldPoints[i] = attackOrigin + rotated;
        }

        // 모든 적을 순회하며 폴리곤 내부 체크 (성능 낮음, 하지만 완전한 자유도)
        Collider2D[] allColliders = Physics2D.OverlapCircleAll(attackOrigin, range * 2f, layerMask);

        foreach (Collider2D col in allColliders)
        {
            if (IsPointInPolygon(col.transform.position, worldPoints))
            {
                hitTargets.Add(col);
            }
        }

        return hitTargets;
    }

    public void DrawGizmos(Vector2 origin, Vector2 direction, float range, Color color)
    {
        Gizmos.color = color;

        float angle = Mathf.Atan2(direction.y, direction.x);
        Vector2[] worldPoints = new Vector2[_localPolygonPoints.Length];

        for (int i = 0; i < _localPolygonPoints.Length; i++)
        {
            Vector2 rotated = RotatePoint(_localPolygonPoints[i] * range, angle);
            worldPoints[i] = origin + rotated;
        }

        // 폴리곤 그리기
        for (int i = 0; i < worldPoints.Length; i++)
        {
            int nextIndex = (i + 1) % worldPoints.Length;
            Gizmos.DrawLine(worldPoints[i], worldPoints[nextIndex]);
        }
    }

    private Vector2 RotatePoint(Vector2 point, float angleRad)
    {
        float cos = Mathf.Cos(angleRad);
        float sin = Mathf.Sin(angleRad);
        return new Vector2(
            point.x * cos - point.y * sin,
            point.x * sin + point.y * cos
        );
    }

    private bool IsPointInPolygon(Vector2 point, Vector2[] polygon)
    {
        int intersections = 0;
        for (int i = 0; i < polygon.Length; i++)
        {
            int j = (i + 1) % polygon.Length;
            if (((polygon[i].y > point.y) != (polygon[j].y > point.y)) &&
                (point.x < (polygon[j].x - polygon[i].x) * (point.y - polygon[i].y) / (polygon[j].y - polygon[i].y) + polygon[i].x))
            {
                intersections++;
            }
        }
        return (intersections % 2) == 1;
    }
}
