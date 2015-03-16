using System;

namespace SharpOTP
{
    /// <summary>
    /// date time slim, 精确到毫秒
    /// </summary>
    static public class DateTimeSlim
    {
        static private int lastTicks = -1;
        static private DateTime lastDateTime;

        /// <summary>
        /// Gets the current utc time in an optimized fashion.
        /// </summary>
        static public DateTime UtcNow
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