using SwapPricer.Models;
using static SwapPricer.Services.CurveBootstrapper;

namespace SwapPricer.Services;

/// <summary>
/// Calculates risk metrics for interest rate swaps.
/// </summary>
public class RiskCalculator
{
    private readonly SwapPricerService _pricerService;
    private readonly CurveBootstrapper _bootstrapper;

    public RiskCalculator(SwapPricerService pricerService)
    {
        _pricerService = pricerService;
        _bootstrapper = new CurveBootstrapper();
    }

    /// <summary>
    /// Calculates DV01 by shocking par swap rates and re-bootstrapping.
    /// This matches market-standard methodology:
    /// - Shock par swap rates by +1bp
    /// - Keep 6M fixing unchanged
    /// - Re-bootstrap curves
    /// - Calculate PV change
    /// </summary>
    public double CalculateDV01(Swap swap, List<MarketQuote> marketQuotes, DateTime referenceDate)
    {
        const double bumpBps = 0.0001; // 1bp = 0.01% = 0.0001

        // Base curves and PV (only IBOR curve needed; discount is derived from IBOR + spread)
        var (iborBase, _) = _bootstrapper.BootstrapCurves(referenceDate, marketQuotes);
        double pvBase = _pricerService.CalculateSwapPV(swap, iborBase, referenceDate);

        // Shocked quotes: bump par swap rates by +1bp, keep fixing unchanged
        var shockedQuotes = marketQuotes.Select(q =>
            q.IsFixing
                ? q  // Keep 6M fixing unchanged
                : new MarketQuote(q.TenorYears, q.Rate + bumpBps, q.IsFixing)
        ).ToList();

        // Re-bootstrap with shocked quotes
        var (iborShocked, _) = _bootstrapper.BootstrapCurves(referenceDate, shockedQuotes);
        double pvShocked = _pricerService.CalculateSwapPV(swap, iborShocked, referenceDate);

        // DV01 = change in PV for +1bp shock
        double dv01 = pvShocked - pvBase;

        return dv01;
    }

    /// <summary>
    /// Calculates Gamma by shocking par swap rates and re-bootstrapping.
    /// Gamma = PV(+1bp) - 2*PV(0) + PV(-1bp)
    /// </summary>
    public double CalculateGamma(Swap swap, List<MarketQuote> marketQuotes, DateTime referenceDate)
    {
        const double bumpBps = 0.0001; // 1bp

        // Base curves and PV
        var (iborBase, _) = _bootstrapper.BootstrapCurves(referenceDate, marketQuotes);
        double pvBase = _pricerService.CalculateSwapPV(swap, iborBase, referenceDate);

        // Shocked up quotes
        var shockedUpQuotes = marketQuotes.Select(q =>
            q.IsFixing
                ? q
                : new MarketQuote(q.TenorYears, q.Rate + bumpBps, q.IsFixing)
        ).ToList();
        var (iborUp, _) = _bootstrapper.BootstrapCurves(referenceDate, shockedUpQuotes);
        double pvUp = _pricerService.CalculateSwapPV(swap, iborUp, referenceDate);

        // Shocked down quotes
        var shockedDownQuotes = marketQuotes.Select(q =>
            q.IsFixing
                ? q
                : new MarketQuote(q.TenorYears, q.Rate - bumpBps, q.IsFixing)
        ).ToList();
        var (iborDown, _) = _bootstrapper.BootstrapCurves(referenceDate, shockedDownQuotes);
        double pvDown = _pricerService.CalculateSwapPV(swap, iborDown, referenceDate);

        // Gamma = convexity measure
        double gamma = pvUp - 2 * pvBase + pvDown;

        return gamma;
    }

    /// <summary>
    /// Risk metrics container.
    /// </summary>
    public record RiskMetrics(double DV01, double Gamma);

    /// <summary>
    /// Calculates all risk metrics at once.
    /// </summary>
    public RiskMetrics CalculateRiskMetrics(Swap swap, List<MarketQuote> marketQuotes, DateTime referenceDate)
    {
        double dv01 = CalculateDV01(swap, marketQuotes, referenceDate);
        double gamma = CalculateGamma(swap, marketQuotes, referenceDate);

        return new RiskMetrics(dv01, gamma);
    }
}
