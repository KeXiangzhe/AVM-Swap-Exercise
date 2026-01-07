namespace SwapPricer.Interpolation;

/// <summary>
/// Cubic spline interpolation with natural boundary conditions.
/// f''(0) = f''(end) = 0
/// Optionally supports f(0) = f(t_first) constraint.
/// </summary>
public class CubicSplineInterpolator : IInterpolator
{
    private readonly double[] _x;
    private readonly double[] _y;
    private readonly double[] _a;
    private readonly double[] _b;
    private readonly double[] _c;
    private readonly double[] _d;

    /// <summary>
    /// Creates a natural cubic spline with f''(0) = f''(end) = 0.
    /// </summary>
    /// <param name="x">Knot points (times)</param>
    /// <param name="y">Values at knot points</param>
    /// <param name="addZeroPoint">If true, adds point at t=0 with value equal to first point</param>
    public CubicSplineInterpolator(double[] x, double[] y, bool addZeroPoint = false)
    {
        if (x.Length != y.Length)
            throw new ArgumentException("Arrays must have same length.");

        if (x.Length < 2)
            throw new ArgumentException("Need at least 2 points for spline.");

        // Optionally add t=0 point with f(0) = f(first_point)
        if (addZeroPoint && x[0] > 0)
        {
            var xList = new List<double> { 0.0 };
            var yList = new List<double> { y[0] }; // f(0) = f(first_point)
            xList.AddRange(x);
            yList.AddRange(y);
            _x = xList.ToArray();
            _y = yList.ToArray();
        }
        else
        {
            _x = (double[])x.Clone();
            _y = (double[])y.Clone();
        }

        int n = _x.Length;
        _a = new double[n];
        _b = new double[n];
        _c = new double[n];
        _d = new double[n];

        BuildSpline();
    }

    private void BuildSpline()
    {
        int n = _x.Length;

        // Copy y values to a (the spline passes through these points)
        Array.Copy(_y, _a, n);

        if (n == 2)
        {
            // Linear interpolation for 2 points
            _b[0] = (_y[1] - _y[0]) / (_x[1] - _x[0]);
            _c[0] = 0;
            _d[0] = 0;
            return;
        }

        // Calculate h (intervals) and alpha
        double[] h = new double[n - 1];
        for (int i = 0; i < n - 1; i++)
            h[i] = _x[i + 1] - _x[i];

        double[] alpha = new double[n - 1];
        for (int i = 1; i < n - 1; i++)
            alpha[i] = (3.0 / h[i]) * (_a[i + 1] - _a[i]) - (3.0 / h[i - 1]) * (_a[i] - _a[i - 1]);

        // Solve tridiagonal system for c (second derivatives / 2)
        double[] l = new double[n];
        double[] mu = new double[n];
        double[] z = new double[n];

        // Natural spline: c[0] = c[n-1] = 0 (second derivative = 0 at boundaries)
        l[0] = 1;
        mu[0] = 0;
        z[0] = 0;

        for (int i = 1; i < n - 1; i++)
        {
            l[i] = 2 * (_x[i + 1] - _x[i - 1]) - h[i - 1] * mu[i - 1];
            mu[i] = h[i] / l[i];
            z[i] = (alpha[i] - h[i - 1] * z[i - 1]) / l[i];
        }

        l[n - 1] = 1;
        z[n - 1] = 0;
        _c[n - 1] = 0;

        // Back substitution
        for (int j = n - 2; j >= 0; j--)
        {
            _c[j] = z[j] - mu[j] * _c[j + 1];
            _b[j] = (_a[j + 1] - _a[j]) / h[j] - h[j] * (_c[j + 1] + 2 * _c[j]) / 3;
            _d[j] = (_c[j + 1] - _c[j]) / (3 * h[j]);
        }
    }

    public double Interpolate(double t)
    {
        int n = _x.Length;

        // Handle extrapolation
        if (t <= _x[0])
            return _y[0];

        if (t >= _x[n - 1])
            return _y[n - 1];

        // Find the interval containing t
        int i = 0;
        while (i < n - 1 && _x[i + 1] < t)
            i++;

        // Evaluate cubic polynomial
        double dx = t - _x[i];
        return _a[i] + _b[i] * dx + _c[i] * dx * dx + _d[i] * dx * dx * dx;
    }

    /// <summary>
    /// Gets the second derivative at a given point.
    /// </summary>
    public double SecondDerivative(double t)
    {
        int n = _x.Length;

        if (t <= _x[0] || t >= _x[n - 1])
            return 0; // Natural boundary condition

        int i = 0;
        while (i < n - 1 && _x[i + 1] < t)
            i++;

        double dx = t - _x[i];
        return 2 * _c[i] + 6 * _d[i] * dx;
    }
}
