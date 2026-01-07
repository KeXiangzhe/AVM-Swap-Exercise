namespace SwapPricer.Utils;

/// <summary>
/// Utility class for date calculations using Actual/Actual day count convention.
/// </summary>
public static class DateUtils
{
    /// <summary>
    /// Calculates the year fraction between two dates using Actual/Actual convention.
    /// </summary>
    public static double YearFraction(DateTime startDate, DateTime endDate)
    {
        if (startDate >= endDate)
            return 0.0;

        double totalYearFraction = 0.0;
        DateTime current = startDate;

        while (current < endDate)
        {
            int year = current.Year;
            DateTime yearEnd = new DateTime(year + 1, 1, 1);
            DateTime periodEnd = endDate < yearEnd ? endDate : yearEnd;

            int daysInYear = DateTime.IsLeapYear(year) ? 366 : 365;
            int daysInPeriod = (periodEnd - current).Days;

            totalYearFraction += (double)daysInPeriod / daysInYear;
            current = periodEnd;
        }

        return totalYearFraction;
    }

    /// <summary>
    /// Calculates the year fraction from a reference date (time in years).
    /// </summary>
    public static double TimeInYears(DateTime referenceDate, DateTime targetDate)
    {
        if (targetDate >= referenceDate)
            return YearFraction(referenceDate, targetDate);
        else
            return -YearFraction(targetDate, referenceDate);
    }

    /// <summary>
    /// Generates a schedule of dates at regular intervals.
    /// </summary>
    public static List<DateTime> GenerateSchedule(DateTime startDate, DateTime endDate, int frequencyMonths)
    {
        var schedule = new List<DateTime> { startDate };
        DateTime current = startDate.AddMonths(frequencyMonths);

        while (current < endDate)
        {
            schedule.Add(current);
            current = current.AddMonths(frequencyMonths);
        }

        if (schedule.Last() != endDate)
            schedule.Add(endDate);

        return schedule;
    }

    /// <summary>
    /// Generates payment dates (excludes start date, includes end date).
    /// </summary>
    public static List<DateTime> GeneratePaymentDates(DateTime startDate, DateTime endDate, int frequencyMonths)
    {
        var dates = new List<DateTime>();
        DateTime current = startDate.AddMonths(frequencyMonths);

        while (current <= endDate)
        {
            dates.Add(current);
            current = current.AddMonths(frequencyMonths);
        }

        // Ensure end date is included if not already
        if (dates.Count == 0 || dates.Last() != endDate)
        {
            if (dates.Count == 0 || dates.Last() < endDate)
                dates.Add(endDate);
        }

        return dates;
    }
}
