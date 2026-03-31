using System;
using System.Windows;
using System.Windows.Media.Animation;

namespace PassNotes;

/// <summary>
/// Анимация GridLength (в пикселях). Используется для плавного изменения
/// ширины ColumnDefinition при сворачивании/разворачивании дерева папок.
/// </summary>
public sealed class GridLengthAnimation : AnimationTimeline
{
    public static readonly DependencyProperty FromProperty = DependencyProperty.Register(
        nameof(From), typeof(GridLength), typeof(GridLengthAnimation));

    public static readonly DependencyProperty ToProperty = DependencyProperty.Register(
        nameof(To), typeof(GridLength), typeof(GridLengthAnimation));

    public GridLength From
    {
        get => (GridLength)GetValue(FromProperty);
        set => SetValue(FromProperty, value);
    }

    public GridLength To
    {
        get => (GridLength)GetValue(ToProperty);
        set => SetValue(ToProperty, value);
    }

    /// <summary>Опциональная функция сглаживания.</summary>
    public IEasingFunction? EasingFunction { get; set; }

    public override Type TargetPropertyType => typeof(GridLength);

    protected override Freezable CreateInstanceCore() => new GridLengthAnimation();

    public override object GetCurrentValue(
        object defaultOriginValue,
        object defaultDestinationValue,
        AnimationClock animationClock)
    {
        var from = From;
        var to = To;

        // На всякий случай, если кто-то попытается анимировать Auto/Star.
        double fromValue = from.GridUnitType == GridUnitType.Pixel ? from.Value : 0d;
        double toValue = to.GridUnitType == GridUnitType.Pixel ? to.Value : 0d;

        double progress = animationClock.CurrentProgress ?? 0d;
        if (EasingFunction != null)
        {
            progress = EasingFunction.Ease(progress);
        }

        double current = fromValue + (toValue - fromValue) * progress;
        if (double.IsNaN(current) || double.IsInfinity(current)) current = 0d;
        if (current < 0) current = 0d;

        return new GridLength(current, GridUnitType.Pixel);
    }
}
