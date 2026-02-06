using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// DoTween으로 색을 변경시키는 상태 인터페이스
/// [조우석 - 2025.10.24.]
/// </summary>
public interface IDoColorState
{
    /// <summary>
    /// 색상 변경 전 기본 색
    /// </summary>
    Color OriginalColor { get; }

    /// <summary>
    /// 변경할 색
    /// </summary>
    Color TargetColor { get; }

    /// <summary>
    /// DoColor를 담을 Tween
    /// </summary>
    Tween ColorTween { get; }

    /// <summary>
    /// DoColor 수행 시간
    /// </summary>
    float TweenTime { get; }

    /// <summary>
    /// 색상 변경 DoTween 시작
    /// </summary>
    void StartDoColor();
    
    /// <summary>
    /// 색상 변경 DoTween 정지
    /// </summary>
    void StopDoColor();
}