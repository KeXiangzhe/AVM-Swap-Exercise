using SwapPricer.Utils;

namespace SwapPricer.Models;

/// <summary>
/// Represents a plain vanilla interest rate swap.
/// </summary>
public class Swap
{
    public DateTime StartDate { get; }
    public DateTime EndDate { get; }
    public double Notional { get; }
    public double FixedRate { get; set; }
    public int FixedFrequencyMonths { get; } = 12;  // Annual
    public int FloatFrequencyMonths { get; } = 6;   // Semi-annual

    public Swap(DateTime startDate, DateTime endDate, double notional, double fixedRate = 0.0)
    {
        StartDate = startDate;
        EndDate = endDate;
        Notional = notional;
        FixedRate = fixedRate;
    }

    /// <summary>
    /// Gets the swap tenor in years.
    /// </summary>
    public double TenorYears => DateUtils.YearFraction(StartDate, EndDate);

    /// <summary>
    /// Generates fixed leg cash flows.
    /// </summary>
    public List<CashFlow> GetFixedLegCashFlows()
    {
        var cashFlows = new List<CashFlow>();
        var payDates = DateUtils.GeneratePaymentDates(StartDate, EndDate, FixedFrequencyMonths);
        DateTime prevDate = StartDate;

        foreach (var payDate in payDates)
        {
            double dayFraction = DateUtils.YearFraction(prevDate, payDate);
            double amount = Notional * FixedRate * dayFraction;
            cashFlows.Add(new CashFlow(prevDate, payDate, payDate, dayFraction, FixedRate, amount));
            prevDate = payDate;
        }

        return cashFlows;
    }

    /// <summary>
    /// Generates floating leg periods (rates to be determined from curve).
    /// </summary>
    public List<FloatPeriod> GetFloatLegPeriods()
    {
        var periods = new List<FloatPeriod>();
        var payDates = DateUtils.GeneratePaymentDates(StartDate, EndDate, FloatFrequencyMonths);
        DateTime prevDate = StartDate;

        foreach (var payDate in payDates)
        {
            double dayFraction = DateUtils.YearFraction(prevDate, payDate);
            periods.Add(new FloatPeriod(prevDate, payDate, payDate, dayFraction));
            prevDate = payDate;
        }

        return periods;
    }
}

/// <summary>
/// Represents a fixed cash flow.
/// </summary>
public record CashFlow(
    DateTime AccrualStart,
    DateTime AccrualEnd,
    DateTime PaymentDate,
    double DayFraction,
    double Rate,
    double Amount
);

/// <summary>
/// Represents a floating rate period.
/// </summary>
public record FloatPeriod(
    DateTime AccrualStart,
    DateTime AccrualEnd,
    DateTime PaymentDate,
    double DayFraction
);
