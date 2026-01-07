using SwapPricer.Models;
using SwapPricer.Utils;

namespace SwapPricer.Services;

/// <summary>
/// Service for pricing interest rate swaps.
/// </summary>
public class SwapPricerService
{
    // Discount spread: -38 bps (added to IBOR rate to get Discount rate)
    private const double DiscountSpread = -0.0038;

    /// <summary>
    /// Gets the discount DF by deriving from the IBOR curve.
    /// Discount_rate = IBOR_rate + spread
    /// Discount_DF = exp(-Discount_rate × t)
    /// </summary>
    private double GetDiscountDFFromIborCurve(Curve iborCurve, double t)
    {
        double iborRate = iborCurve.GetZeroRate(t);
        double discRate = iborRate + DiscountSpread;
        return Math.Exp(-discRate * t);
    }

    /// <summary>
    /// Gets the IBOR DF from the IBOR curve.
    /// IBOR_DF = exp(-IBOR_rate × t)
    /// </summary>
    private double GetIborDF(Curve iborCurve, double t)
    {
        double iborRate = iborCurve.GetZeroRate(t);
        return Math.Exp(-iborRate * t);
    }

    /// <summary>
    /// Calculates forward rate from IBOR DFs.
    /// </summary>
    private double GetForwardRateFromIborCurve(Curve iborCurve, double tStart, double tEnd)
    {
        double iborDfStart = GetIborDF(iborCurve, tStart);
        double iborDfEnd = GetIborDF(iborCurve, tEnd);
        double tau = tEnd - tStart;
        return (iborDfStart / iborDfEnd - 1.0) / tau;
    }

    /// <summary>
    /// Calculates the present value of the fixed leg.
    /// </summary>
    public double CalculateFixedLegPV(Swap swap, Curve iborCurve, DateTime valuationDate)
    {
        double pv = 0.0;
        var cashFlows = swap.GetFixedLegCashFlows();

        foreach (var cf in cashFlows)
        {
            if (cf.PaymentDate <= valuationDate)
                continue; // Already paid

            double t = DateUtils.YearFraction(iborCurve.ReferenceDate, cf.PaymentDate);
            double df = GetDiscountDFFromIborCurve(iborCurve, t);
            pv += cf.Amount * df;
        }

        return pv;
    }

    /// <summary>
    /// Calculates the present value of the floating leg.
    /// Uses IBOR curve for forward rates, derives discount DFs from IBOR + spread.
    /// </summary>
    public double CalculateFloatLegPV(Swap swap, Curve iborCurve, DateTime valuationDate)
    {
        double pv = 0.0;
        var periods = swap.GetFloatLegPeriods();

        foreach (var period in periods)
        {
            if (period.PaymentDate <= valuationDate)
                continue; // Already paid

            double tPay = DateUtils.YearFraction(iborCurve.ReferenceDate, period.PaymentDate);

            // Forward rate for the period
            double forwardRate;

            // Check if this is the first period (0-6M) which uses the market fixing
            double originalTEnd = DateUtils.YearFraction(swap.StartDate, period.AccrualEnd);
            if (originalTEnd <= 0.5 + 0.001) // First 6M period
            {
                // Use the 6M fixing rate from market data
                forwardRate = 0.0411;
            }
            else
            {
                // Calculate forward rate from IBOR curve
                double tStart = DateUtils.YearFraction(iborCurve.ReferenceDate, period.AccrualStart);
                double tEnd = DateUtils.YearFraction(iborCurve.ReferenceDate, period.AccrualEnd);

                if (tStart < 0.0001)
                {
                    forwardRate = GetForwardRateFromIborCurve(iborCurve, 0.0001, tEnd);
                }
                else
                {
                    forwardRate = GetForwardRateFromIborCurve(iborCurve, tStart, tEnd);
                }
            }

            // Discount using DF derived from IBOR curve + spread
            double df = GetDiscountDFFromIborCurve(iborCurve, tPay);
            double amount = swap.Notional * forwardRate * period.DayFraction;
            pv += amount * df;
        }

        return pv;
    }

    /// <summary>
    /// Calculates the swap NPV (Float - Fixed for receiver swap, Fixed - Float for payer).
    /// Convention: Positive NPV means receiving fixed is profitable.
    /// </summary>
    public double CalculateSwapPV(Swap swap, Curve iborCurve, DateTime valuationDate)
    {
        double fixedPV = CalculateFixedLegPV(swap, iborCurve, valuationDate);
        double floatPV = CalculateFloatLegPV(swap, iborCurve, valuationDate);

        // Receiver swap (receive fixed, pay float): PV = Fixed - Float
        return fixedPV - floatPV;
    }

    /// <summary>
    /// Calculates the par swap rate (rate that makes NPV = 0).
    /// </summary>
    public double CalculateParRate(Swap swap, Curve iborCurve, DateTime valuationDate)
    {
        // Par rate = Float PV / Fixed Annuity
        double floatPV = CalculateFloatLegPV(swap, iborCurve, valuationDate);
        double annuity = CalculateFixedAnnuity(swap, iborCurve, valuationDate);

        return floatPV / (swap.Notional * annuity);
    }

    /// <summary>
    /// Calculates the fixed leg annuity (sum of DF * day_fraction).
    /// </summary>
    public double CalculateFixedAnnuity(Swap swap, Curve iborCurve, DateTime valuationDate)
    {
        double annuity = 0.0;
        var payDates = DateUtils.GeneratePaymentDates(swap.StartDate, swap.EndDate, swap.FixedFrequencyMonths);
        DateTime prevDate = swap.StartDate;

        foreach (var payDate in payDates)
        {
            if (payDate <= valuationDate)
            {
                prevDate = payDate;
                continue;
            }

            double t = DateUtils.YearFraction(iborCurve.ReferenceDate, payDate);
            double tau = DateUtils.YearFraction(prevDate, payDate);
            double df = GetDiscountDFFromIborCurve(iborCurve, t);

            annuity += df * tau;
            prevDate = payDate;
        }

        return annuity;
    }

    /// <summary>
    /// Calculates accrued interest on the fixed leg as of valuation date.
    /// </summary>
    public double CalculateFixedAccrual(Swap swap, DateTime valuationDate)
    {
        var cashFlows = swap.GetFixedLegCashFlows();

        foreach (var cf in cashFlows)
        {
            if (cf.AccrualStart <= valuationDate && valuationDate < cf.AccrualEnd)
            {
                // We're in this accrual period
                double accrualDays = DateUtils.YearFraction(cf.AccrualStart, valuationDate);
                double periodDays = cf.DayFraction;
                double accrualFraction = accrualDays / periodDays;

                return cf.Amount * accrualFraction;
            }
        }

        return 0.0;
    }

    /// <summary>
    /// Calculates accrued interest on the floating leg as of valuation date.
    /// </summary>
    public double CalculateFloatAccrual(Swap swap, Curve iborCurve, DateTime valuationDate)
    {
        var periods = swap.GetFloatLegPeriods();

        foreach (var period in periods)
        {
            if (period.AccrualStart <= valuationDate && valuationDate < period.AccrualEnd)
            {
                // We're in this accrual period - use the fixing rate
                double originalTEnd = DateUtils.YearFraction(swap.StartDate, period.AccrualEnd);

                double forwardRate;
                if (originalTEnd <= 0.5 + 0.001) // First 6M period
                {
                    // Use the 6M fixing rate
                    forwardRate = 0.0411;
                }
                else
                {
                    // Calculate forward rate from IBOR curve
                    double tStart = DateUtils.YearFraction(iborCurve.ReferenceDate, period.AccrualStart);
                    double tEnd = DateUtils.YearFraction(iborCurve.ReferenceDate, period.AccrualEnd);
                    forwardRate = GetForwardRateFromIborCurve(iborCurve, Math.Max(tStart, 0.0001), tEnd);
                }

                double accrualDays = DateUtils.YearFraction(period.AccrualStart, valuationDate);
                double fullAmount = swap.Notional * forwardRate * period.DayFraction;
                double accrualFraction = accrualDays / period.DayFraction;

                return fullAmount * accrualFraction;
            }
        }

        return 0.0;
    }

    /// <summary>
    /// Calculates the clean PV (dirty PV minus accrued interest).
    /// </summary>
    public (double CleanPV, double FixedAccrual, double FloatAccrual) CalculateCleanPV(
        Swap swap, Curve iborCurve, DateTime valuationDate)
    {
        double dirtyPV = CalculateSwapPV(swap, iborCurve, valuationDate);
        double fixedAccrual = CalculateFixedAccrual(swap, valuationDate);
        double floatAccrual = CalculateFloatAccrual(swap, iborCurve, valuationDate);

        // Clean PV for receiver: remove accrued fixed (we'll receive it) and add accrued float (we'll pay it)
        double cleanPV = dirtyPV - fixedAccrual + floatAccrual;

        return (cleanPV, fixedAccrual, floatAccrual);
    }
}
