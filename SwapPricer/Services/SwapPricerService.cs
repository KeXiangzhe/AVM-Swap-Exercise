using SwapPricer.Models;
using SwapPricer.Utils;

namespace SwapPricer.Services;

/// <summary>
/// Service for pricing interest rate swaps.
/// </summary>
public class SwapPricerService
{
    /// <summary>
    /// Calculates the present value of the fixed leg.
    /// </summary>
    public double CalculateFixedLegPV(Swap swap, Curve discountCurve, DateTime valuationDate)
    {
        double pv = 0.0;
        var cashFlows = swap.GetFixedLegCashFlows();

        foreach (var cf in cashFlows)
        {
            if (cf.PaymentDate <= valuationDate)
                continue; // Already paid

            double t = DateUtils.YearFraction(discountCurve.ReferenceDate, cf.PaymentDate);
            double df = discountCurve.GetDiscountFactor(t);
            pv += cf.Amount * df;
        }

        return pv;
    }

    /// <summary>
    /// Calculates the present value of the floating leg.
    /// </summary>
    public double CalculateFloatLegPV(Swap swap, Curve forwardCurve, Curve discountCurve, DateTime valuationDate)
    {
        double pv = 0.0;
        var periods = swap.GetFloatLegPeriods();

        foreach (var period in periods)
        {
            if (period.PaymentDate <= valuationDate)
                continue; // Already paid

            double tStart = DateUtils.YearFraction(forwardCurve.ReferenceDate, period.AccrualStart);
            double tEnd = DateUtils.YearFraction(forwardCurve.ReferenceDate, period.AccrualEnd);
            double tPay = DateUtils.YearFraction(discountCurve.ReferenceDate, period.PaymentDate);

            // Forward rate for the period
            double forwardRate;
            if (period.AccrualStart <= valuationDate)
            {
                // Period has started, rate is fixed at period start
                // For simplicity, use the forward rate as of curve reference date
                tStart = Math.Max(tStart, 0.0001); // Avoid division by zero
            }

            if (tStart < 0.0001)
            {
                // First period starting now, use the short rate
                forwardRate = forwardCurve.GetZeroRate(tEnd);
            }
            else
            {
                forwardRate = forwardCurve.GetForwardRate(tStart, tEnd);
            }

            double df = discountCurve.GetDiscountFactor(tPay);
            double amount = swap.Notional * forwardRate * period.DayFraction;
            pv += amount * df;
        }

        return pv;
    }

    /// <summary>
    /// Calculates the swap NPV (Float - Fixed for receiver swap, Fixed - Float for payer).
    /// Convention: Positive NPV means receiving fixed is profitable.
    /// </summary>
    public double CalculateSwapPV(Swap swap, Curve forwardCurve, Curve discountCurve, DateTime valuationDate)
    {
        double fixedPV = CalculateFixedLegPV(swap, discountCurve, valuationDate);
        double floatPV = CalculateFloatLegPV(swap, forwardCurve, discountCurve, valuationDate);

        // Receiver swap (receive fixed, pay float): PV = Fixed - Float
        return fixedPV - floatPV;
    }

    /// <summary>
    /// Calculates the par swap rate (rate that makes NPV = 0).
    /// </summary>
    public double CalculateParRate(Swap swap, Curve forwardCurve, Curve discountCurve, DateTime valuationDate)
    {
        // Par rate = Float PV / Fixed Annuity
        double floatPV = CalculateFloatLegPV(swap, forwardCurve, discountCurve, valuationDate);
        double annuity = CalculateFixedAnnuity(swap, discountCurve, valuationDate);

        return floatPV / (swap.Notional * annuity);
    }

    /// <summary>
    /// Calculates the fixed leg annuity (sum of DF * day_fraction).
    /// </summary>
    public double CalculateFixedAnnuity(Swap swap, Curve discountCurve, DateTime valuationDate)
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

            double t = DateUtils.YearFraction(discountCurve.ReferenceDate, payDate);
            double tau = DateUtils.YearFraction(prevDate, payDate);
            double df = discountCurve.GetDiscountFactor(t);

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
    public double CalculateFloatAccrual(Swap swap, Curve forwardCurve, DateTime valuationDate)
    {
        var periods = swap.GetFloatLegPeriods();

        foreach (var period in periods)
        {
            if (period.AccrualStart <= valuationDate && valuationDate < period.AccrualEnd)
            {
                // We're in this accrual period
                double tStart = DateUtils.YearFraction(forwardCurve.ReferenceDate, period.AccrualStart);
                double tEnd = DateUtils.YearFraction(forwardCurve.ReferenceDate, period.AccrualEnd);

                double forwardRate;
                if (tStart < 0.0001)
                    forwardRate = forwardCurve.GetZeroRate(tEnd);
                else
                    forwardRate = forwardCurve.GetForwardRate(tStart, tEnd);

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
        Swap swap, Curve forwardCurve, Curve discountCurve, DateTime valuationDate)
    {
        double dirtyPV = CalculateSwapPV(swap, forwardCurve, discountCurve, valuationDate);
        double fixedAccrual = CalculateFixedAccrual(swap, valuationDate);
        double floatAccrual = CalculateFloatAccrual(swap, forwardCurve, valuationDate);

        // Clean PV for receiver: remove accrued fixed (we'll receive it) and add accrued float (we'll pay it)
        double cleanPV = dirtyPV - fixedAccrual + floatAccrual;

        return (cleanPV, fixedAccrual, floatAccrual);
    }
}
