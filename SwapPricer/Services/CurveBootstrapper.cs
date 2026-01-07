using SwapPricer.Models;
using SwapPricer.Utils;

namespace SwapPricer.Services;

/// <summary>
/// Bootstraps zero rate curves from market par swap rates using dual-curve methodology.
/// </summary>
public class CurveBootstrapper
{
    private const double DiscountSpreadBps = -38.0;
    private const double Tolerance = 1e-10;
    private const int MaxIterations = 100;

    /// <summary>
    /// Market data point with tenor and rate.
    /// </summary>
    public record MarketQuote(double TenorYears, double Rate, bool IsFixing);

    /// <summary>
    /// Bootstraps both IBOR and Discount curves simultaneously using dual-curve methodology.
    /// - Forward rates projected from IBOR curve
    /// - Cash flows discounted using Discount curve (IBOR - 38bps)
    /// </summary>
    public (Curve IborCurve, Curve DiscountCurve) BootstrapCurves(DateTime referenceDate, List<MarketQuote> quotes)
    {
        var iborCurve = new Curve(referenceDate);
        var discountCurve = new Curve(referenceDate);
        double spread = DiscountSpreadBps / 10000.0;

        // Sort quotes by tenor
        var sortedQuotes = quotes.OrderBy(q => q.TenorYears).ToList();

        foreach (var quote in sortedQuotes)
        {
            if (quote.IsFixing)
            {
                // 6M fixing: use directly as simple rate
                iborCurve.AddPoint(quote.TenorYears, quote.Rate);
                discountCurve.AddPoint(quote.TenorYears, quote.Rate + spread);
            }
            else
            {
                // Par swap rate: bootstrap using dual-curve methodology
                double iborZeroRate = BootstrapDualCurve(
                    iborCurve, discountCurve, referenceDate,
                    quote.TenorYears, quote.Rate, spread);

                iborCurve.AddPoint(quote.TenorYears, iborZeroRate);
                discountCurve.AddPoint(quote.TenorYears, iborZeroRate + spread);
            }
        }

        return (iborCurve, discountCurve);
    }

    /// <summary>
    /// Bootstraps a single IBOR zero rate using dual-curve methodology.
    /// Uses Newton-Raphson to find the rate that makes swap NPV = 0.
    /// </summary>
    private double BootstrapDualCurve(Curve iborCurve, Curve discountCurve,
        DateTime referenceDate, double tenorYears, double parRate, double spread)
    {
        // Generate payment schedules
        int tenorMonths = (int)(tenorYears * 12);
        DateTime endDate = referenceDate.AddMonths(tenorMonths);
        var fixedPayDates = DateUtils.GeneratePaymentDates(referenceDate, endDate, 12);  // Annual
        var floatPayDates = DateUtils.GeneratePaymentDates(referenceDate, endDate, 6);   // Semi-annual

        // Initial guess: use par rate as starting point
        double iborRate = parRate;

        // Newton-Raphson iteration
        for (int iter = 0; iter < MaxIterations; iter++)
        {
            double discRate = iborRate + spread;

            // Calculate swap NPV with current guess
            double floatPV = CalculateFloatLegPV(iborCurve, discountCurve, referenceDate,
                floatPayDates, iborRate, discRate, tenorYears);
            double fixedPV = CalculateFixedLegPV(discountCurve, referenceDate,
                fixedPayDates, parRate, discRate, tenorYears);

            double npv = floatPV - fixedPV;

            if (Math.Abs(npv) < Tolerance)
            {
                return iborRate;
            }

            // Numerical derivative for Newton-Raphson
            double bump = 0.0001;
            double floatPVUp = CalculateFloatLegPV(iborCurve, discountCurve, referenceDate,
                floatPayDates, iborRate + bump, discRate + bump, tenorYears);
            double fixedPVUp = CalculateFixedLegPV(discountCurve, referenceDate,
                fixedPayDates, parRate, discRate + bump, tenorYears);
            double npvUp = floatPVUp - fixedPVUp;

            double derivative = (npvUp - npv) / bump;
            if (Math.Abs(derivative) < 1e-15)
                break;

            iborRate = iborRate - npv / derivative;
        }

        return iborRate;
    }

    /// <summary>
    /// Calculates floating leg PV using forward rates from IBOR curve, discounted with discount curve.
    /// </summary>
    private double CalculateFloatLegPV(Curve iborCurve, Curve discountCurve,
        DateTime referenceDate, List<DateTime> payDates,
        double newIborRate, double newDiscRate, double newTenor)
    {
        double pv = 0.0;
        DateTime prevDate = referenceDate;

        foreach (var payDate in payDates)
        {
            double tStart = DateUtils.YearFraction(referenceDate, prevDate);
            double tEnd = DateUtils.YearFraction(referenceDate, payDate);
            double tau = DateUtils.YearFraction(prevDate, payDate);

            // Get forward rate from IBOR curve
            double forwardRate;
            if (tStart < 0.001)
            {
                // First period: forward rate = zero rate at period end
                forwardRate = tEnd >= newTenor - 0.001
                    ? newIborRate
                    : iborCurve.GetZeroRate(tEnd);
            }
            else
            {
                // Forward rate from zero rates
                double dfStart = GetDiscountFactor(iborCurve, tStart, newIborRate, newTenor);
                double dfEnd = GetDiscountFactor(iborCurve, tEnd, newIborRate, newTenor);
                forwardRate = (dfStart / dfEnd - 1.0) / tau;
            }

            // Discount using discount curve
            double dfDisc = GetDiscountFactor(discountCurve, tEnd, newDiscRate, newTenor);
            pv += forwardRate * tau * dfDisc;

            prevDate = payDate;
        }

        return pv;
    }

    /// <summary>
    /// Calculates fixed leg PV discounted with discount curve.
    /// </summary>
    private double CalculateFixedLegPV(Curve discountCurve, DateTime referenceDate,
        List<DateTime> payDates, double fixedRate, double newDiscRate, double newTenor)
    {
        double pv = 0.0;
        DateTime prevDate = referenceDate;

        foreach (var payDate in payDates)
        {
            double t = DateUtils.YearFraction(referenceDate, payDate);
            double tau = DateUtils.YearFraction(prevDate, payDate);

            double df = GetDiscountFactor(discountCurve, t, newDiscRate, newTenor);
            pv += fixedRate * tau * df;

            prevDate = payDate;
        }

        return pv;
    }

    /// <summary>
    /// Gets discount factor, using the new rate if at or beyond the new tenor point.
    /// Uses continuous compounding: DF = exp(-r * t)
    /// </summary>
    private double GetDiscountFactor(Curve curve, double t, double newRate, double newTenor)
    {
        if (t < 0.001)
            return 1.0;

        double rate;
        if (t >= newTenor - 0.001)
        {
            // Use the new rate being bootstrapped
            rate = newRate;
        }
        else
        {
            // Use existing curve
            rate = curve.GetZeroRate(t);
        }

        return Math.Exp(-rate * t);
    }

    /// <summary>
    /// Legacy method for compatibility - creates discount curve from IBOR curve.
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
