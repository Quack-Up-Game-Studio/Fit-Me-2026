using System;
using System.Globalization;

namespace FitMe.GameData
{
    public static class DailyHintRewardPolicy
    {
        private const string DateFormat = "yyyy-MM-dd";

        public static bool TryResolve(DateTime localNow, int currentHints, int freeHintCap,
            int dailyGrant, string lastCheckedDate, out int updatedHints, out string updatedCheckedDate)
        {
            if (currentHints < 0) throw new ArgumentOutOfRangeException(nameof(currentHints));
            if (freeHintCap < 0) throw new ArgumentOutOfRangeException(nameof(freeHintCap));
            if (dailyGrant < 0) throw new ArgumentOutOfRangeException(nameof(dailyGrant));

            var today = localNow.ToString(DateFormat, CultureInfo.InvariantCulture);
            if (DateTime.TryParseExact(lastCheckedDate, DateFormat, CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var lastChecked) && localNow.Date <= lastChecked.Date)
            {
                updatedHints = currentHints;
                updatedCheckedDate = lastCheckedDate;
                return false;
            }

            updatedCheckedDate = today;
            updatedHints = currentHints >= freeHintCap
                ? currentHints
                : currentHints + Math.Min(dailyGrant, freeHintCap - currentHints);
            return true;
        }
    }
}
