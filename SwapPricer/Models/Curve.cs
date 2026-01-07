using SwapPricer.Interpolation;

namespace SwapPricer.Models;

/// <summary>
/// Represents a zero rate curve with interpolation support.
/// </summary>
public class Curve
{
    private readonly List<double> _times;
    private readonly List<double> _zeroRates;
    private IInterpolator? _interpolator;

    public DateTime ReferenceDate { get; }
    public IReadOnlyList<double> Times => _times;
    public IReadOnlyList<double> ZeroRates => _zeroRates;

    public Curve(DateTime referenceDate)
    {
        ReferenceDate = referenceDate;
        _times = new List<double>();
        _zeroRates = new List<double>();
    }

    /// <summary>
    /// Adds a zero rate point to the curve.
    /// </summary>
    public void AddPoint(double timeInYears, double zeroRate)
    {
        // Insert in sorted order by time
        int index = _times.BinarySearch(timeInYears);
        if (index < 0) index = ~index;

        _times.Insert(index, timeInYears);
        _zeroRates.Insert(index, zeroRate);
        _interpolator = null; // Reset interpolator
    }

    /// <summary>
    /// Sets a custom interpolator (e.g., cubic spline).
    /// </summary>
    public void SetInterpolator(IInterpolator interpolator)
    {
        _interpolator = interpolator;
    }

    /// <summary>
    /// Gets the zero rate at a given time using interpolation.
    /// </summary>
    public double GetZeroRate(double timeInYears)
    {
        if (_times.Count == 0)
            throw new InvalidOperationException("Curve has no data points.");

        if (_interpolator == null)
            _interpolator = new LinearInterpolator(_times.ToArray(), _zeroRates.ToArray());

        return _interpolator.Interpolate(timeInYears);
    }

    /// <summary>
    /// Gets the discount factor at a given time.
    /// DF(t) = 1 / (1 + r(t) * t)  [simple rate convention]
    /// </summary>
    public double GetDiscountFactor(double timeInYears)
    {
        if (timeInYears <= 0)
            return 1.0;

        double zeroRate = GetZeroRate(timeInYears);
        return 1.0 / (1.0 + zeroRate * timeInYears);
    }

    /// <summary>
    /// Gets the forward rate between two times (simple compounding).
    /// </summary>
    public double GetForwardRate(double t1, double t2)
    {
        if (t2 <= t1)
            throw new ArgumentException("t2 must be greater than t1.");

        double df1 = GetDiscountFactor(t1);
        double df2 = GetDiscountFactor(t2);
        double tau = t2 - t1;

        return (df1 / df2 - 1.0) / tau;
    }

    /// <summary>
    /// Creates a copy of this curve with a parallel shift applied.
    /// </summary>
    public Curve ShiftParallel(double shiftBps)
    {
        double shift = shiftBps / 10000.0;
        var shiftedCurve = new Curve(ReferenceDate);

        for (int i = 0; i < _times.Count; i++)
        {
            shiftedCurve.AddPoint(_times[i], _zeroRates[i] + shift);
        }

        return shiftedCurve;
    }

    /// <summary>
    /// Creates a deep copy of this curve.
    /// </summary>
    public Curve Clone()
    {
        var clone = new Curve(ReferenceDate);
        for (int i = 0; i < _times.Count; i++)
        {
            clone.AddPoint(_times[i], _zeroRates[i]);
        }
        return clone;
    }

    /// <summary>
    /// Prints the curve data points.
    /// </summary>
    public void Print(string name)
    {
        Console.WriteLine($"\n{name}:");
        Console.WriteLine($"{"Time (Y)",-10} {"Zero Rate (%)",-15} {"Discount Factor",-15}");
        Console.WriteLine(new string('-', 40));

        foreach (var t in _times)
        {
            double rate = GetZeroRate(t);
            double df = GetDiscountFactor(t);
            Console.WriteLine($"{t,-10:F4} {rate * 100,-15:F6} {df,-15:F8}");
        }
    }
}
