using SwapPricer.Models;
using SwapPricer.Utils;

namespace SwapPricer.Services;

/// <summary>
/// Bootstraps zero rate curves from market par swap rates.
/// </summary>
public class CurveBootstrapper
{
    /// <summary>
    /// Market data point with tenor and rate.
    /// </summary>
    public record MarketQuote(double TenorYears, double Rate, bool IsFixing);

    /// <summary>
    /// Bootstraps the IBOR zero curve from market quotes.
    /// - 6M fixing is used directly
    /// - Par swap rates are bootstrapped iteratively
    /// </summary>
    public Curve BootstrapIborCurve(DateTime referenceDate, List<MarketQuote> quotes)
    {
        var curve = new Curve(referenceDate);

        // Sort quotes by tenor
        var sortedQuotes = quotes.OrderBy(q => q.TenorYears).ToList();

        foreach (var quote in sortedQuotes)
        {
            if (quote.IsFixing)
            {
                // 6M fixing: convert simple rate to continuous zero rate
                // Simple rate: 1 + r * t = DF^(-1)
                // DF = 1 / (1 + r * t)
                // Continuous: DF = exp(-z * t), so z = -ln(DF) / t
                double df = 1.0 / (1.0 + quote.Rate * quote.TenorYears);
                double zeroRate = -Math.Log(df) / quote.TenorYears;
                curve.AddPoint(quote.TenorYears, zeroRate);
            }
            else
            {
                // Par swap rate: bootstrap the zero rate
                double zeroRate = BootstrapSwapRate(curve, referenceDate, quote.TenorYears, quote.Rate);
                curve.AddPoint(quote.TenorYears, zeroRate);
            }
        }

        return curve;
    }

    /// <summary>
    /// Bootstraps a single zero rate from a par swap rate.
    /// Uses the relationship: 1 - DF_n = S * Annuity
    /// where Annuity = sum of DF_i * day_fraction_i for fixed leg payment dates
    /// </summary>
    private double BootstrapSwapRate(Curve curve, DateTime referenceDate, double tenorYears, double parRate)
    {
        // Generate fixed leg payment dates (annual)
        int tenorMonths = (int)(tenorYears * 12);
        DateTime endDate = referenceDate.AddMonths(tenorMonths);
        var fixedPayDates = DateUtils.GeneratePaymentDates(referenceDate, endDate, 12);

        // Calculate annuity (sum of DF * day_fraction for known discount factors)
        double annuity = 0.0;
        DateTime prevDate = referenceDate;

        foreach (var payDate in fixedPayDates)
        {
            double t = DateUtils.YearFraction(referenceDate, payDate);
            double tau = DateUtils.YearFraction(prevDate, payDate);

            // Only include payments before the final maturity
            if (payDate < endDate)
            {
                double df = curve.GetDiscountFactor(t);
                annuity += df * tau;
            }
            prevDate = payDate;
        }

        // Calculate tau for the final period
        double tauLast = DateUtils.YearFraction(fixedPayDates.Count > 1 ? fixedPayDates[^2] : referenceDate, endDate);
        if (fixedPayDates.Count == 1)
            tauLast = DateUtils.YearFraction(referenceDate, endDate);

        // Par swap equation: 1 - DF_n = parRate * (annuity + DF_n * tau_last)
        // DF_n = (1 - parRate * annuity) / (1 + parRate * tau_last)
        double dfn = (1.0 - parRate * annuity) / (1.0 + parRate * tauLast);
        double zeroRate = -Math.Log(dfn) / tenorYears;

        return zeroRate;
    }

    /// <summary>
    /// Creates the discount curve by applying a spread to the IBOR curve.
    /// </summary>
    public Curve CreateDiscountCurve(Curve iborCurve, double spreadBps)
    {
        double spread = spreadBps / 10000.0;
        var discountCurve = new Curve(iborCurve.ReferenceDate);

        for (int i = 0; i < iborCurve.Times.Count; i++)
        {
            discountCurve.AddPoint(iborCurve.Times[i], iborCurve.ZeroRates[i] + spread);
        }

        return discountCurve;
    }
}
