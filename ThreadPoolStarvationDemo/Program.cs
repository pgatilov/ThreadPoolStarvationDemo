using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ThreadPoolStarvationDemo
{
    class Program
    {
        private static readonly TimeSpan WorkMethodDuration = TimeSpan.FromMilliseconds(100);
        private static readonly TimeSpan GarbageMethodDuration = TimeSpan.FromMilliseconds(1000);

        private static long CyclesToPassToMatchWorkMethodDuration;

        private static volatile int NumberOfRequestsExecuting;
        private static volatile int NumberOfWorkMethodsExecuting;
        private static volatile int NumberOfGarbageMethodsExecuting;

        private static volatile int RequestsStarted;
        private static volatile int RequestsCompleted;
        private static volatile int WorkStarted;
        private static volatile int WorkCompleted;
        private static volatile int GarbageStarted;
        private static volatile int GarbageCompleted;

        private static volatile int LastCompletedRequests;
        private static volatile int LastGarbageCompleted;

        private static int RequestsSent;

        static void Main()
        {
            //var set = ThreadPool.SetMaxThreads(100, 100);
            //Debug.Assert(set, "set");

            int minWorkerThreads;
            int maxWorkerThreads;
            int minCompletionThreads;
            int maxCompletionThreads;
            ThreadPool.GetMinThreads(out minWorkerThreads, out minCompletionThreads);
            ThreadPool.GetMaxThreads(out maxWorkerThreads, out maxCompletionThreads);
            Console.WriteLine("Worker threads: {0} - {1}; Completion Port threads: {2} - {3}", minWorkerThreads, maxWorkerThreads, minCompletionThreads, maxCompletionThreads);

            Calibrate();

            const int requestsPerSecond = 20;
            var requestInterval = TimeSpan.FromSeconds(1.0 / requestsPerSecond);

            var statisticsStopwatch = Stopwatch.StartNew();
            var statisticsInterval = TimeSpan.FromSeconds(5);

            var iterations = (int)(CyclesToPassToMatchWorkMethodDuration / (WorkMethodDuration.TotalMilliseconds / requestInterval.TotalMilliseconds));
            while (true)
            {
                SendRequest();
                RequestsSent++;

//                Thread.Sleep(requestInterval);
                Thread.SpinWait(iterations);

                var elapsed = statisticsStopwatch.Elapsed;
                if (elapsed > statisticsInterval)
                {
                    ReportStatistics(elapsed);

                    statisticsStopwatch.Restart();
                }
            }
        }

        private static void Calibrate()
        {
            // warm up
            Thread.SpinWait(100);

            var sw = Stopwatch.StartNew();

            var iterations = 0L;
            do
            {
                Thread.SpinWait(100);
                iterations += 100L;
            } while (sw.Elapsed < WorkMethodDuration);

            CyclesToPassToMatchWorkMethodDuration = iterations;
        }

        private static void ReportStatistics(TimeSpan elapsed)
        {
            var requestsCompleted = RequestsCompleted;
            var requestsCompletedFromLastReport = requestsCompleted - LastCompletedRequests;
            var throughput = requestsCompletedFromLastReport/elapsed.TotalSeconds;

            var garbageCompleted = GarbageCompleted;
            var garbageCompletedFromLastReport = garbageCompleted - LastGarbageCompleted;
            var garbageThroughput = garbageCompletedFromLastReport/elapsed.TotalSeconds;

            Console.WriteLine();
            Console.WriteLine("Throughput: {0:F2}, Logs per second: {1:F2}", throughput, garbageThroughput);
            Console.WriteLine("In Progress: Requests = {0}, Work = {1}, Garbage = {2}", NumberOfRequestsExecuting, NumberOfWorkMethodsExecuting, NumberOfGarbageMethodsExecuting);
            //Console.WriteLine("Requests completed: {0}, Logs completed: {1}", requestsCompleted, garbageCompleted);
            Console.WriteLine("Requests sent: {0}", RequestsSent);
            Console.WriteLine("Total (st/comp): Requests = {0} / {1}, Work = {2} / {3}, Garbage = {4} / {5}", RequestsStarted, requestsCompleted, WorkStarted, WorkCompleted, GarbageStarted, garbageCompleted);

            LastCompletedRequests = requestsCompleted;
            LastGarbageCompleted = garbageCompleted;
        }

        private static void InvokeGarbageMethod(Action spendTime)
        {
            //spendTime();
            var queued = ThreadPool.QueueUserWorkItem(_ => spendTime());
            Debug.Assert(queued, "queued");
        }

        private static void SendRequest()
        {
            var queued = ThreadPool.QueueUserWorkItem(_ => DoRequest());

            Debug.Assert(queued, "queued");
        }

        private static void DoRequest()
        {
            Interlocked.Increment(ref RequestsStarted);
            Interlocked.Increment(ref NumberOfRequestsExecuting);

            DoWork();

            InvokeGarbageMethod(SpendTime);

            Interlocked.Decrement(ref NumberOfRequestsExecuting);
            Interlocked.Increment(ref RequestsCompleted);
        }

        private static void DoWork()
        {
            Interlocked.Increment(ref WorkStarted);
            Interlocked.Increment(ref NumberOfWorkMethodsExecuting);

            var iterations = checked((int)CyclesToPassToMatchWorkMethodDuration);
            Thread.SpinWait(iterations);

            Interlocked.Decrement(ref NumberOfWorkMethodsExecuting);
            Interlocked.Increment(ref WorkCompleted);
        }

        private static void SpendTime()
        {
            Interlocked.Increment(ref GarbageStarted);
            Interlocked.Increment(ref NumberOfGarbageMethodsExecuting);

            Thread.Sleep(GarbageMethodDuration);

            Interlocked.Decrement(ref NumberOfGarbageMethodsExecuting);
            Interlocked.Increment(ref GarbageCompleted);
        }
    }
}
