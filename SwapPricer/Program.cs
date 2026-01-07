using SwapPricer.Models;
using SwapPricer.Services;
using SwapPricer.Interpolation;
using SwapPricer.Utils;
using static SwapPricer.Services.CurveBootstrapper;

namespace SwapPricer;

/// <summary>
/// AVM Programming Exercise: Swap Curve
/// Implements pricing for interest rate swaps with curve bootstrapping.
/// </summary>
class Program
{
    // Market data
    private static readonly List<MarketQuote> MarketQuotes = new()
    {
        new MarketQuote(0.5, 0.0411, true),    // 6M fixing
        new MarketQuote(1.0, 0.0414, false),   // 1Y par swap
        new MarketQuote(2.0, 0.0373, false),   // 2Y par swap
        new MarketQuote(3.0, 0.0348, false),   // 3Y par swap
        new MarketQuote(5.0, 0.0321, false),   // 5Y par swap
        new MarketQuote(7.0, 0.0311, false),   // 7Y par swap
        new MarketQuote(10.0, 0.0308, false),  // 10Y par swap
    };

    private const double DiscountSpreadBps = -38.0;
    private const double Notional = 1_000_000.0;
    private const int SwapTenorYears = 9;
    private static StreamWriter? _fileWriter;

    static void Main(string[] args)
    {
        // Create output file
        string outputPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "Results.txt");
        outputPath = Path.GetFullPath(outputPath);

        using (_fileWriter = new StreamWriter(outputPath))
        {
            WriteLine("=".PadRight(70, '='));
            WriteLine("AVM Programming Exercise: Swap Curve");
            WriteLine("=".PadRight(70, '='));

            DateTime referenceDate = DateTime.Today;
            WriteLine($"\nReference Date: {referenceDate:yyyy-MM-dd}");
            WriteLine($"Notional: {Notional:N0}");

            // Initialize services
            var bootstrapper = new CurveBootstrapper();
            var pricerService = new SwapPricerService();
            var riskCalculator = new RiskCalculator(pricerService);

            // Question 1: Build IBOR and Discount Curves using dual-curve methodology
            WriteLine("\n" + "=".PadRight(70, '='));
            WriteLine("QUESTION 1: Curve Construction (Dual-Curve Bootstrap)");
            WriteLine("=".PadRight(70, '='));

            var (iborCurve, discountCurve) = bootstrapper.BootstrapCurves(referenceDate, MarketQuotes);

            WriteLine($"\nDiscount spread: {DiscountSpreadBps} bps over IBOR curve");
            WriteLine("Float leg: semi-annual reset/pay");
            WriteLine("Fixed leg: annual pay");
            WriteLine("Day count: Actual/Actual");
            WriteLine("Business day adjustment: None");
            WriteLine("Spot lag: Zero");

            PrintCurve(iborCurve, "IBOR (Forward) Curve");
            PrintCurve(discountCurve, "Discount Curve");

            // Question 2: 9Y Par Swap Pricing
            WriteLine("\n" + "=".PadRight(70, '='));
            WriteLine("QUESTION 2: 9Y Par Swap Pricing");
            WriteLine("=".PadRight(70, '='));

            DateTime swapEndDate = referenceDate.AddYears(SwapTenorYears);
            var swap = new Swap(referenceDate, swapEndDate, Notional);

            double parRate = pricerService.CalculateParRate(swap, iborCurve, discountCurve, referenceDate);
            swap.FixedRate = parRate;

            WriteLine($"\nSwap Details:");
            WriteLine($"  Start Date: {swap.StartDate:yyyy-MM-dd}");
            WriteLine($"  End Date: {swap.EndDate:yyyy-MM-dd}");
            WriteLine($"  Tenor: {SwapTenorYears} years");
            WriteLine($"  Notional: {Notional:N0}");

            WriteLine($"\nResults:");
            WriteLine($"  Par Swap Rate: {parRate * 100:F6}%");

            var riskMetrics = riskCalculator.CalculateRiskMetrics(swap, iborCurve, discountCurve, referenceDate);
            WriteLine($"  DV01: {riskMetrics.DV01:F2}");
            WriteLine($"  Gamma: {riskMetrics.Gamma:F2}");

            // Verify par swap has zero PV
            double parSwapPV = pricerService.CalculateSwapPV(swap, iborCurve, discountCurve, referenceDate);
            WriteLine($"\n  Verification - Par Swap PV: {parSwapPV:F2} (should be ~0)");

            // Question 3: 3 Months Later - Accrual and Clean PV
            WriteLine("\n" + "=".PadRight(70, '='));
            WriteLine("QUESTION 3: Valuation 3 Months Later (Linear Interpolation)");
            WriteLine("=".PadRight(70, '='));

            DateTime valuationDate3M = referenceDate.AddMonths(3);
            WriteLine($"\nValuation Date: {valuationDate3M:yyyy-MM-dd}");
            WriteLine("(Assuming curve unchanged)");

            // Shift curve reference date but keep same rates
            var iborCurve3M = ShiftCurveReferenceDate(iborCurve, valuationDate3M);
            var discountCurve3M = ShiftCurveReferenceDate(discountCurve, valuationDate3M);

            var (cleanPV, fixedAccrual, floatAccrual) = pricerService.CalculateCleanPV(
                swap, iborCurve3M, discountCurve3M, valuationDate3M);

            double dirtyPV = pricerService.CalculateSwapPV(swap, iborCurve3M, discountCurve3M, valuationDate3M);

            WriteLine($"\nResults:");
            WriteLine($"  Fixed Leg Accrual: {fixedAccrual:F2}");
            WriteLine($"  Float Leg Accrual: {floatAccrual:F2}");
            WriteLine($"  Net Accrual (Fixed - Float): {fixedAccrual - floatAccrual:F2}");
            WriteLine($"  Dirty PV: {dirtyPV:F2}");
            WriteLine($"  Clean PV: {cleanPV:F2}");

            // Question 4: Cubic Spline Interpolation
            WriteLine("\n" + "=".PadRight(70, '='));
            WriteLine("QUESTION 4: Cubic Spline Interpolation");
            WriteLine("=".PadRight(70, '='));

            WriteLine("\nBoundary conditions:");
            WriteLine("  f(0) = f(6M)");
            WriteLine("  f''(0) = f''(10Y) = 0");

            // Create cubic spline interpolated curve (shifted to 3M forward)
            var iborCurve3MSpline = ShiftCurveReferenceDate(iborCurve, valuationDate3M, useSpline: true);

            // Use same discount curve from Q1 (shifted to new reference date)
            var (cleanPVSpline, fixedAccrualSpline, floatAccrualSpline) = pricerService.CalculateCleanPV(
                swap, iborCurve3MSpline, discountCurve3M, valuationDate3M);

            double dirtyPVSpline = pricerService.CalculateSwapPV(swap, iborCurve3MSpline, discountCurve3M, valuationDate3M);

            WriteLine($"\nResults (with Cubic Spline for IBOR curve):");
            WriteLine($"  Fixed Leg Accrual: {fixedAccrualSpline:F2}");
            WriteLine($"  Float Leg Accrual: {floatAccrualSpline:F2}");
            WriteLine($"  Net Accrual (Fixed - Float): {fixedAccrualSpline - floatAccrualSpline:F2}");
            WriteLine($"  Dirty PV: {dirtyPVSpline:F2}");
            WriteLine($"  Clean PV: {cleanPVSpline:F2}");

            WriteLine("\n" + "=".PadRight(70, '='));
            WriteLine("Comparison: Linear vs Cubic Spline");
            WriteLine("=".PadRight(70, '='));
            WriteLine($"  Clean PV (Linear):       {cleanPV:F2}");
            WriteLine($"  Clean PV (Cubic Spline): {cleanPVSpline:F2}");
            WriteLine($"  Difference:              {cleanPVSpline - cleanPV:F2}");

            WriteLine("\n" + "=".PadRight(70, '='));
            WriteLine("Exercise Complete");
            WriteLine("=".PadRight(70, '='));
        }

        Console.WriteLine($"\nResults saved to: {outputPath}");
    }

    /// <summary>
    /// Writes to both console and file.
    /// </summary>
    private static void WriteLine(string text)
    {
        Console.WriteLine(text);
        _fileWriter?.WriteLine(text);
    }

    /// <summary>
    /// Prints curve data to both console and file.
    /// </summary>
    private static void PrintCurve(Curve curve, string name)
    {
        WriteLine($"\n{name}:");
        WriteLine($"{"Time (Y)",-10} {"Zero Rate (%)",-15} {"Discount Factor",-15}");
        WriteLine(new string('-', 40));

        foreach (var t in curve.Times)
        {
            double rate = curve.GetZeroRate(t);
            double df = curve.GetDiscountFactor(t);
            WriteLine($"{t,-10:F4} {rate * 100,-15:F6} {df,-15:F8}");
        }
    }

    /// <summary>
    /// Creates a new curve with shifted reference date but same zero rates.
    /// This simulates "curve unchanged" scenario.
    /// </summary>
    private static Curve ShiftCurveReferenceDate(Curve originalCurve, DateTime newReferenceDate, bool useSpline = false)
    {
        var newCurve = new Curve(newReferenceDate);

        // Calculate time shift
        double timeShift = DateUtils.YearFraction(originalCurve.ReferenceDate, newReferenceDate);

        // Add points with adjusted times
        for (int i = 0; i < originalCurve.Times.Count; i++)
        {
            double newTime = originalCurve.Times[i] - timeShift;
            if (newTime > 0)
            {
                newCurve.AddPoint(newTime, originalCurve.ZeroRates[i]);
            }
        }

        // Apply cubic spline interpolation if requested
        if (useSpline && newCurve.Times.Count >= 2)
        {
            var spline = new CubicSplineInterpolator(
                newCurve.Times.ToArray(),
                newCurve.ZeroRates.ToArray(),
                addZeroPoint: true);
            newCurve.SetInterpolator(spline);
        }

        return newCurve;
    }
}
