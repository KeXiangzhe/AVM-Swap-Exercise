namespace SwapPricer.Utils;

/// <summary>
/// Utility class for date calculations using Actual/Actual day count convention.
/// </summary>
public static class DateUtils
{
    /// <summary>
    /// Calculates the year fraction between two dates using Actual/Actual (ISDA) convention.
    /// Each year contributes: (days in that year) / (365 or 366 for leap year)
    /// </summary>
    public static double YearFraction(DateTime startDate, DateTime endDate)
    {
        if (startDate >= endDate)
            return 0.0;

        double fraction = 0.0;
        DateTime current = startDate;

        while (current.Year < endDate.Year)
        {
            // Days remaining in this year
            DateTime yearEnd = new DateTime(current.Year + 1, 1, 1);
            int daysInYear = DateTime.IsLeapYear(current.Year) ? 366 : 365;
            int daysRemaining = (yearEnd - current).Days;
            fraction += (double)daysRemaining / daysInYear;
            current = yearEnd;
        }

        // Days in the final year
        int finalYearDays = DateTime.IsLeapYear(endDate.Year) ? 366 : 365;
        int daysInFinalPeriod = (endDate - current).Days;
        fraction += (double)daysInFinalPeriod / finalYearDays;

        return fraction;
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
