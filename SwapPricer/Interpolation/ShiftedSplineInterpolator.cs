namespace SwapPricer.Interpolation;

/// <summary>
/// Wraps a spline interpolator and applies a time shift when querying.
/// Used for forward valuation where the spline is built on original knots
/// but queries are made relative to a forward date.
/// </summary>
public class ShiftedSplineInterpolator : IInterpolator
{
    private readonly IInterpolator _originalSpline;
    private readonly double _timeShift;

    /// <summary>
    /// Creates a shifted spline interpolator.
    /// </summary>
    /// <param name="originalSpline">The spline built on original time points</param>
    /// <param name="timeShift">The time to add when querying (e.g., 0.25 for 3 months forward)</param>
    public ShiftedSplineInterpolator(IInterpolator originalSpline, double timeShift)
    {
        _originalSpline = originalSpline;
        _timeShift = timeShift;
    }

    /// <summary>
    /// Interpolates at time t by querying the original spline at t + timeShift.
    /// </summary>
    public double Interpolate(double t)
    {
        // Query at original time = current time + shift
        double originalTime = t + _timeShift;
        return _originalSpline.Interpolate(originalTime);
    }
}
