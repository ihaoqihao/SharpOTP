using System;

namespace SharpOTP
{
    /// <summary>
    /// date time slim
    /// </summary>
    static public class DateTimeSlim
    {
        static private int lastTicks = -1;
        static private DateTime lastDateTime;

        /// <summary>
        /// Gets the current utc time in an optimized fashion.
        /// </summary>
        public static DateTime UtcNow
        {
            get
            {
                int tickCount = Environment.TickCount;
                if (tickCount == lastTicks) return lastDateTime;

                DateTime dt = DateTime.UtcNow;
                lastTicks = tickCount;
                lastDateTime = dt;
                return dt;
            }
        }
    }
}