using SwapPricer.Models;

namespace SwapPricer.Services;

/// <summary>
/// Calculates risk metrics for interest rate swaps.
/// </summary>
public class RiskCalculator
{
    private readonly SwapPricerService _pricerService;

    public RiskCalculator(SwapPricerService pricerService)
    {
        _pricerService = pricerService;
    }

    /// <summary>
    /// Calculates DV01 (Dollar Value of 1 basis point).
    /// Uses central difference: DV01 = (PV(+1bp) - PV(-1bp)) / 2
    /// </summary>
    public double CalculateDV01(Swap swap, Curve forwardCurve, Curve discountCurve, DateTime valuationDate)
    {
        const double bumpBps = 1.0;

        // Shift both curves up
        var forwardUp = forwardCurve.ShiftParallel(bumpBps);
        var discountUp = discountCurve.ShiftParallel(bumpBps);
        double pvUp = _pricerService.CalculateSwapPV(swap, forwardUp, discountUp, valuationDate);

        // Shift both curves down
        var forwardDown = forwardCurve.ShiftParallel(-bumpBps);
        var discountDown = discountCurve.ShiftParallel(-bumpBps);
        double pvDown = _pricerService.CalculateSwapPV(swap, forwardDown, discountDown, valuationDate);

        // Central difference
        double dv01 = (pvUp - pvDown) / 2.0;

        return dv01;
    }

    /// <summary>
    /// Calculates Gamma (rate of change of DV01 with respect to rates).
    /// Uses finite difference: Gamma = (PV(+1bp) - 2*PV(0) + PV(-1bp)) / (1bp)^2
    /// </summary>
    public double CalculateGamma(Swap swap, Curve forwardCurve, Curve discountCurve, DateTime valuationDate)
    {
        const double bumpBps = 1.0;
        const double bumpDecimal = bumpBps / 10000.0;

        // Base PV
        double pvBase = _pricerService.CalculateSwapPV(swap, forwardCurve, discountCurve, valuationDate);

        // Shift both curves up
        var forwardUp = forwardCurve.ShiftParallel(bumpBps);
        var discountUp = discountCurve.ShiftParallel(bumpBps);
        double pvUp = _pricerService.CalculateSwapPV(swap, forwardUp, discountUp, valuationDate);

        // Shift both curves down
        var forwardDown = forwardCurve.ShiftParallel(-bumpBps);
        var discountDown = discountCurve.ShiftParallel(-bumpBps);
        double pvDown = _pricerService.CalculateSwapPV(swap, forwardDown, discountDown, valuationDate);

        // Second derivative using central difference
        double gamma = (pvUp - 2 * pvBase + pvDown) / (bumpDecimal * bumpDecimal);

        return gamma;
    }

    /// <summary>
    /// Risk metrics container.
    /// </summary>
    public record RiskMetrics(double DV01, double Gamma);

    /// <summary>
    /// Calculates all risk metrics at once.
    /// </summary>
    public RiskMetrics CalculateRiskMetrics(Swap swap, Curve forwardCurve, Curve discountCurve, DateTime valuationDate)
    {
        double dv01 = CalculateDV01(swap, forwardCurve, discountCurve, valuationDate);
        double gamma = CalculateGamma(swap, forwardCurve, discountCurve, valuationDate);

        return new RiskMetrics(dv01, gamma);
    }
}
