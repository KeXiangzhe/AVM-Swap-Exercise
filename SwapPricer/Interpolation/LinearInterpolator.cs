namespace SwapPricer.Interpolation;

/// <summary>
/// Linear interpolation for curve values.
/// </summary>
public class LinearInterpolator : IInterpolator
{
    private readonly double[] _times;
    private readonly double[] _values;

    public LinearInterpolator(double[] times, double[] values)
    {
        if (times.Length != values.Length)
            throw new ArgumentException("Times and values arrays must have the same length.");

        _times = times;
        _values = values;
    }

    public double Interpolate(double t)
    {
        if (_times.Length == 0)
            throw new InvalidOperationException("No data points for interpolation.");

        if (_times.Length == 1)
            return _values[0];

        // Flat extrapolation for values outside the range
        if (t <= _times[0])
            return _values[0];

        if (t >= _times[^1])
            return _values[^1];

        // Find the interval containing t
        int i = 0;
        while (i < _times.Length - 1 && _times[i + 1] < t)
            i++;

        // Linear interpolation
        double t1 = _times[i];
        double t2 = _times[i + 1];
        double v1 = _values[i];
        double v2 = _values[i + 1];

        double weight = (t - t1) / (t2 - t1);
        return v1 + weight * (v2 - v1);
    }
}
