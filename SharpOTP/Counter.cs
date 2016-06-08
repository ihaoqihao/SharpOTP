using System;
using System.Diagnostics;

namespace SharpOTP
{
    /// <summary>
    /// counter
    /// </summary>
    public sealed class Counter
    {
        /// <summary>
        /// counter category name
        /// </summary>
        public const string CATEGORY_NAME = "sharp_otp";

        private readonly PerformanceCounter _post = null;
        private readonly PerformanceCounter _executing = null;
        private readonly PerformanceCounter _timeTaken = null;
        private readonly PerformanceCounter _failed = null;

        /// <summary>
        /// new
        /// </summary>
        static Counter()
        {
            if (PerformanceCounterCategory.Exists(CATEGORY_NAME)) return;

            //try create counter category
            var coll = new CounterCreationDataCollection();
            coll.Add(new CounterCreationData("Post", "", PerformanceCounterType.RateOfCountsPerSecond32));
            coll.Add(new CounterCreationData("Executing", "", PerformanceCounterType.NumberOfItems32));
            coll.Add(new CounterCreationData("TimeTaken", "", PerformanceCounterType.NumberOfItems32));
            coll.Add(new CounterCreationData("Failed", "", PerformanceCounterType.RateOfCountsPerSecond32));

            try { PerformanceCounterCategory.Create(CATEGORY_NAME, "", PerformanceCounterCategoryType.MultiInstance, coll); }
            catch (Exception ex) { Trace.TraceError(ex.ToString()); }
        }
        /// <summary>
        /// new
        /// </summary>
        /// <param name="actorName"></param>
        public Counter(string actorName)
        {
            if (string.IsNullOrEmpty(actorName))
                throw new ArgumentNullException("actorName");

            try
            {
                this._post = new PerformanceCounter();
                this._post.CategoryName = CATEGORY_NAME;
                this._post.CounterName = "Post";
                this._post.InstanceName = actorName;
                this._post.MachineName = ".";
                this._post.ReadOnly = false;
                this._post.InstanceLifetime = PerformanceCounterInstanceLifetime.Global;
                this._post.RawValue = 0;

                this._executing = new PerformanceCounter();
                this._executing.CategoryName = CATEGORY_NAME;
                this._executing.CounterName = "Executing";
                this._executing.InstanceName = actorName;
                this._executing.MachineName = ".";
                this._executing.ReadOnly = false;
                this._executing.InstanceLifetime = PerformanceCounterInstanceLifetime.Global;
                this._executing.RawValue = 0;

                this._timeTaken = new PerformanceCounter();
                this._timeTaken.CategoryName = CATEGORY_NAME;
                this._timeTaken.CounterName = "TimeTaken";
                this._timeTaken.InstanceName = actorName;
                this._timeTaken.MachineName = ".";
                this._timeTaken.ReadOnly = false;
                this._timeTaken.InstanceLifetime = PerformanceCounterInstanceLifetime.Global;
                this._timeTaken.RawValue = 0;

                this._failed = new PerformanceCounter();
                this._failed.CategoryName = CATEGORY_NAME;
                this._failed.CounterName = "Failed";
                this._failed.InstanceName = actorName;
                this._failed.MachineName = ".";
                this._failed.ReadOnly = false;
                this._failed.InstanceLifetime = PerformanceCounterInstanceLifetime.Global;
                this._failed.RawValue = 0;
            }
            catch (Exception ex)
            {
                Trace.TraceError(ex.ToString());

                this._executing = null;
                this._failed = null;
                this._timeTaken = null;
            }
        }

        /// <summary>
        /// post
        /// </summary>
        public void Post()
        {
            try
            {
                if (this._post != null) this._post.Increment();
                if (this._executing != null) this._executing.Increment();
            }
            catch { }
        }
        /// <summary>
        /// fail
        /// </summary>
        public void Fail()
        {
            try
            {
                if (this._executing != null) this._executing.Decrement();
                if (this._failed != null) this._failed.Increment();
            }
            catch { }
        }
        /// <summary>
        /// complete
        /// </summary>
        /// <param name="duration"></param>
        public void Complete(int duration)
        {
            try
            {
                if (this._executing != null) this._executing.Decrement();
                if (this._timeTaken != null) this._timeTaken.RawValue = duration;
            }
            catch { }
        }
    }
}