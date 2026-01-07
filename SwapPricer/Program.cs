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

    static void Main(string[] args)
    {
        Console.WriteLine("=".PadRight(70, '='));
        Console.WriteLine("AVM Programming Exercise: Swap Curve");
        Console.WriteLine("=".PadRight(70, '='));

        DateTime referenceDate = DateTime.Today;
        Console.WriteLine($"\nReference Date: {referenceDate:yyyy-MM-dd}");
        Console.WriteLine($"Notional: {Notional:N0}");

        // Initialize services
        var bootstrapper = new CurveBootstrapper();
        var pricerService = new SwapPricerService();
        var riskCalculator = new RiskCalculator(pricerService);

        // Question 1: Build IBOR and Discount Curves
        Console.WriteLine("\n" + "=".PadRight(70, '='));
        Console.WriteLine("QUESTION 1: Curve Construction");
        Console.WriteLine("=".PadRight(70, '='));

        var iborCurve = bootstrapper.BootstrapIborCurve(referenceDate, MarketQuotes);
        var discountCurve = bootstrapper.CreateDiscountCurve(iborCurve, DiscountSpreadBps);

        Console.WriteLine($"\nDiscount spread: {DiscountSpreadBps} bps over IBOR curve");
        Console.WriteLine("Float leg: semi-annual reset/pay");
        Console.WriteLine("Fixed leg: annual pay");
        Console.WriteLine("Day count: Actual/Actual");
        Console.WriteLine("Business day adjustment: None");
        Console.WriteLine("Spot lag: Zero");

        iborCurve.Print("IBOR (Forward) Curve");
        discountCurve.Print("Discount Curve");

        // Question 2: 9Y Par Swap Pricing
        Console.WriteLine("\n" + "=".PadRight(70, '='));
        Console.WriteLine("QUESTION 2: 9Y Par Swap Pricing");
        Console.WriteLine("=".PadRight(70, '='));

        DateTime swapEndDate = referenceDate.AddYears(SwapTenorYears);
        var swap = new Swap(referenceDate, swapEndDate, Notional);

        double parRate = pricerService.CalculateParRate(swap, iborCurve, discountCurve, referenceDate);
        swap.FixedRate = parRate;

        Console.WriteLine($"\nSwap Details:");
        Console.WriteLine($"  Start Date: {swap.StartDate:yyyy-MM-dd}");
        Console.WriteLine($"  End Date: {swap.EndDate:yyyy-MM-dd}");
        Console.WriteLine($"  Tenor: {SwapTenorYears} years");
        Console.WriteLine($"  Notional: {Notional:N0}");

        Console.WriteLine($"\nResults:");
        Console.WriteLine($"  Par Swap Rate: {parRate * 100:F6}%");

        var riskMetrics = riskCalculator.CalculateRiskMetrics(swap, iborCurve, discountCurve, referenceDate);
        Console.WriteLine($"  DV01: {riskMetrics.DV01:F2}");
        Console.WriteLine($"  Gamma: {riskMetrics.Gamma:F2}");

        // Verify par swap has zero PV
        double parSwapPV = pricerService.CalculateSwapPV(swap, iborCurve, discountCurve, referenceDate);
        Console.WriteLine($"\n  Verification - Par Swap PV: {parSwapPV:F2} (should be ~0)");

        // Question 3: 3 Months Later - Accrual and Clean PV
        Console.WriteLine("\n" + "=".PadRight(70, '='));
        Console.WriteLine("QUESTION 3: Valuation 3 Months Later (Linear Interpolation)");
        Console.WriteLine("=".PadRight(70, '='));

        DateTime valuationDate3M = referenceDate.AddMonths(3);
        Console.WriteLine($"\nValuation Date: {valuationDate3M:yyyy-MM-dd}");
        Console.WriteLine("(Assuming curve unchanged)");

        // Shift curve reference date but keep same rates
        var iborCurve3M = ShiftCurveReferenceDate(iborCurve, valuationDate3M);
        var discountCurve3M = ShiftCurveReferenceDate(discountCurve, valuationDate3M);

        var (cleanPV, fixedAccrual, floatAccrual) = pricerService.CalculateCleanPV(
            swap, iborCurve3M, discountCurve3M, valuationDate3M);

        double dirtyPV = pricerService.CalculateSwapPV(swap, iborCurve3M, discountCurve3M, valuationDate3M);

        Console.WriteLine($"\nResults:");
        Console.WriteLine($"  Fixed Leg Accrual: {fixedAccrual:F2}");
        Console.WriteLine($"  Float Leg Accrual: {floatAccrual:F2}");
        Console.WriteLine($"  Net Accrual (Fixed - Float): {fixedAccrual - floatAccrual:F2}");
        Console.WriteLine($"  Dirty PV: {dirtyPV:F2}");
        Console.WriteLine($"  Clean PV: {cleanPV:F2}");

        // Question 4: Cubic Spline Interpolation
        Console.WriteLine("\n" + "=".PadRight(70, '='));
        Console.WriteLine("QUESTION 4: Cubic Spline Interpolation");
        Console.WriteLine("=".PadRight(70, '='));

        Console.WriteLine("\nBoundary conditions:");
        Console.WriteLine("  f(0) = f(6M)");
        Console.WriteLine("  f''(0) = f''(10Y) = 0");

        // Create cubic spline interpolated curve (shifted to 3M forward)
        var iborCurve3MSpline = ShiftCurveReferenceDate(iborCurve, valuationDate3M, useSpline: true);

        // Use same discount curve from Q1 (shifted to new reference date)
        var (cleanPVSpline, fixedAccrualSpline, floatAccrualSpline) = pricerService.CalculateCleanPV(
            swap, iborCurve3MSpline, discountCurve3M, valuationDate3M);

        double dirtyPVSpline = pricerService.CalculateSwapPV(swap, iborCurve3MSpline, discountCurve3M, valuationDate3M);

        Console.WriteLine($"\nResults (with Cubic Spline for IBOR curve):");
        Console.WriteLine($"  Fixed Leg Accrual: {fixedAccrualSpline:F2}");
        Console.WriteLine($"  Float Leg Accrual: {floatAccrualSpline:F2}");
        Console.WriteLine($"  Net Accrual (Fixed - Float): {fixedAccrualSpline - floatAccrualSpline:F2}");
        Console.WriteLine($"  Dirty PV: {dirtyPVSpline:F2}");
        Console.WriteLine($"  Clean PV: {cleanPVSpline:F2}");

        Console.WriteLine("\n" + "=".PadRight(70, '='));
        Console.WriteLine("Comparison: Linear vs Cubic Spline");
        Console.WriteLine("=".PadRight(70, '='));
        Console.WriteLine($"  Clean PV (Linear):       {cleanPV:F2}");
        Console.WriteLine($"  Clean PV (Cubic Spline): {cleanPVSpline:F2}");
        Console.WriteLine($"  Difference:              {cleanPVSpline - cleanPV:F2}");

        Console.WriteLine("\n" + "=".PadRight(70, '='));
        Console.WriteLine("Exercise Complete");
        Console.WriteLine("=".PadRight(70, '='));
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
