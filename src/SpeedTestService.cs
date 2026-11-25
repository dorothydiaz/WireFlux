using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;

namespace WireFlux
{
    public enum SpeedTestStage
    {
        Idle,
        Latency,
        Download,
        Upload,
        Completed,
        Cancelled,
        Failed
    }

    public class SpeedTestProgressInfo
    {
        public SpeedTestStage Stage { get; set; }
        public string StatusText { get; set; }
        public double? DownloadMbps { get; set; }
        public double? UploadMbps { get; set; }
        public int? LatencyMs { get; set; }
        public float OverallProgress { get; set; } // 0.0 to 1.0
    }

    public static class SpeedTestService
    {
        private const string DownloadUrl = "https://speed.cloudflare.com/__down?bytes=50000000";
        private const string WarmupUrl = "https://speed.cloudflare.com/__down?bytes=1024";
        private const string UploadUrl = "https://speed.cloudflare.com/__up";
        private const int ParallelStreams = 4;
        private const double PhaseDurationSeconds = 3.6;
        private const double WarmupIgnoreSeconds = 0.30;

        static SpeedTestService()
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            if (ServicePointManager.DefaultConnectionLimit < 24)
            {
                ServicePointManager.DefaultConnectionLimit = 24;
            }
            ServicePointManager.Expect100Continue = false;
        }

        public static async Task<SpeedTestProgressInfo> RunAsync(
            Action<SpeedTestProgressInfo> onProgress,
            CancellationToken token)
        {
            var state = new SpeedTestProgressInfo
            {
                Stage = SpeedTestStage.Latency,
                StatusText = "Measuring latency...",
                OverallProgress = 0.03f
            };
            Report(onProgress, state);

            try
            {
                // 1. Warmup TLS connection pool & measure ping latency
                int? latency = await Task.Run(() => MeasureBestPingAndWarmup(token), token);
                token.ThrowIfCancellationRequested();

                state.LatencyMs = latency;
                state.OverallProgress = 0.08f;
                Report(onProgress, state);

                // 2. Multi-stream Download test
                state.Stage = SpeedTestStage.Download;
                state.StatusText = "Testing download...";
                Report(onProgress, state);

                double downMbps = await MeasureDownloadMbpsAsync(
                    (liveMbps, phaseFrac) =>
                    {
                        state.DownloadMbps = liveMbps;
                        state.OverallProgress = 0.08f + phaseFrac * 0.46f;
                        Report(onProgress, state);
                    },
                    token);

                token.ThrowIfCancellationRequested();
                state.DownloadMbps = downMbps;
                state.OverallProgress = 0.54f;
                Report(onProgress, state);

                // 3. Multi-stream Upload test
                state.Stage = SpeedTestStage.Upload;
                state.StatusText = "Testing upload...";
                Report(onProgress, state);

                double upMbps = await MeasureUploadMbpsAsync(
                    (liveMbps, phaseFrac) =>
                    {
                        state.UploadMbps = liveMbps;
                        state.OverallProgress = 0.54f + phaseFrac * 0.46f;
                        Report(onProgress, state);
                    },
                    token);

                token.ThrowIfCancellationRequested();
                state.UploadMbps = upMbps;
                state.Stage = SpeedTestStage.Completed;
                state.StatusText = "Test complete";
                state.OverallProgress = 1.0f;
                Report(onProgress, state);
                return state;
            }
            catch (OperationCanceledException)
            {
                state.Stage = SpeedTestStage.Cancelled;
                state.StatusText = "Cancelled";
                state.OverallProgress = 0f;
                Report(onProgress, state);
                return state;
            }
            catch (Exception ex)
            {
                state.Stage = SpeedTestStage.Failed;
                state.StatusText = "Failed: " + ex.Message;
                state.OverallProgress = 0f;
                Report(onProgress, state);
                return state;
            }
        }

        private static void Report(Action<SpeedTestProgressInfo> callback, SpeedTestProgressInfo info)
        {
            if (callback == null) return;
            callback(new SpeedTestProgressInfo
            {
                Stage = info.Stage,
                StatusText = info.StatusText,
                DownloadMbps = info.DownloadMbps,
                UploadMbps = info.UploadMbps,
                LatencyMs = info.LatencyMs,
                OverallProgress = info.OverallProgress
            });
        }

        private static int? MeasureBestPingAndWarmup(CancellationToken token)
        {
            // Warm up HTTPS connection pool to Cloudflare edge
            try
            {
                var req = (HttpWebRequest)WebRequest.Create(WarmupUrl);
                req.Timeout = 4000;
                req.ReadWriteTimeout = 4000;
                req.KeepAlive = true;
                using (var resp = req.GetResponse())
                using (var s = resp.GetResponseStream())
                {
                    if (s != null)
                    {
                        byte[] buf = new byte[1024];
                        s.Read(buf, 0, buf.Length);
                    }
                }
            }
            catch
            {
                // Will surface in download phase if offline
            }

            int? bestMs = null;
            for (int i = 0; i < 3; i++)
            {
                if (token.IsCancellationRequested) break;
                try
                {
                    using (var ping = new Ping())
                    {
                        var reply = ping.Send("1.1.1.1", 1000);
                        if (reply != null && reply.Status == IPStatus.Success)
                        {
                            int rtt = (int)reply.RoundtripTime;
                            if (!bestMs.HasValue || rtt < bestMs.Value)
                                bestMs = rtt;
                        }
                    }
                }
                catch
                {
                }
            }

            return bestMs;
        }

        private static async Task<double> MeasureDownloadMbpsAsync(
            Action<double, float> onSample,
            CancellationToken outerToken)
        {
            using (var phaseCts = CancellationTokenSource.CreateLinkedTokenSource(outerToken))
            {
                long measuredBytes = 0;
                int measurementActive = 0;
                var phaseWatch = Stopwatch.StartNew();
                var measureWatch = new Stopwatch();

                var workers = new Task[ParallelStreams];
                for (int i = 0; i < ParallelStreams; i++)
                {
                    int streamIdx = i;
                    workers[i] = Task.Run(() =>
                    {
                        byte[] buffer = new byte[65536];
                        while (!phaseCts.IsCancellationRequested && phaseWatch.Elapsed.TotalSeconds < PhaseDurationSeconds)
                        {
                            HttpWebRequest req = null;
                            try
                            {
                                req = (HttpWebRequest)WebRequest.Create(DownloadUrl + "&s=" + streamIdx + "&t=" + Environment.TickCount);
                                req.Method = "GET";
                                req.KeepAlive = true;
                                req.Timeout = 6000;
                                req.ReadWriteTimeout = 6000;

                                using (phaseCts.Token.Register(() => { try { req.Abort(); } catch { } }))
                                using (var resp = req.GetResponse())
                                using (var stream = resp.GetResponseStream())
                                {
                                    if (stream == null) break;
                                    while (!phaseCts.IsCancellationRequested && phaseWatch.Elapsed.TotalSeconds < PhaseDurationSeconds)
                                    {
                                        int read = stream.Read(buffer, 0, buffer.Length);
                                        if (read <= 0) break;
                                        if (Volatile.Read(ref measurementActive) == 1)
                                        {
                                            Interlocked.Add(ref measuredBytes, read);
                                        }
                                    }
                                }
                            }
                            catch
                            {
                                if (phaseCts.IsCancellationRequested) break;
                                Thread.Sleep(40);
                            }
                        }
                    });
                }

                double lastMbps = 0;
                bool anyData = false;

                while (phaseWatch.Elapsed.TotalSeconds < PhaseDurationSeconds)
                {
                    outerToken.ThrowIfCancellationRequested();
                    await Task.Delay(80, outerToken);

                    double elapsedTotal = phaseWatch.Elapsed.TotalSeconds;
                    if (Volatile.Read(ref measurementActive) == 0 && elapsedTotal >= WarmupIgnoreSeconds)
                    {
                        measureWatch.Start();
                        Volatile.Write(ref measurementActive, 1);
                    }

                    if (Volatile.Read(ref measurementActive) == 1 && measureWatch.Elapsed.TotalSeconds > 0.08)
                    {
                        long bytes = Interlocked.Read(ref measuredBytes);
                        if (bytes > 0) anyData = true;
                        lastMbps = (bytes * 8.0) / (measureWatch.Elapsed.TotalSeconds * 1000000.0);
                        float frac = (float)Math.Min(1.0, elapsedTotal / PhaseDurationSeconds);
                        onSample(lastMbps, frac);
                    }
                }

                phaseCts.Cancel();
                try { await Task.WhenAll(workers); } catch { }

                outerToken.ThrowIfCancellationRequested();
                if (!anyData)
                {
                    throw new InvalidOperationException("Could not reach speed test server.");
                }

                double finalSecs = Math.Max(0.1, measureWatch.Elapsed.TotalSeconds);
                return (Interlocked.Read(ref measuredBytes) * 8.0) / (finalSecs * 1000000.0);
            }
        }

        private static async Task<double> MeasureUploadMbpsAsync(
            Action<double, float> onSample,
            CancellationToken outerToken)
        {
            using (var phaseCts = CancellationTokenSource.CreateLinkedTokenSource(outerToken))
            {
                long measuredBytes = 0;
                int measurementActive = 0;
                var phaseWatch = Stopwatch.StartNew();
                var measureWatch = new Stopwatch();

                byte[] sharedChunk = new byte[16384];
                new Random(42).NextBytes(sharedChunk);
                const int PayloadPerPost = 4 * 1024 * 1024; // 4 MB per streamed POST

                var workers = new Task[ParallelStreams];
                for (int i = 0; i < ParallelStreams; i++)
                {
                    workers[i] = Task.Run(() =>
                    {
                        while (!phaseCts.IsCancellationRequested && phaseWatch.Elapsed.TotalSeconds < PhaseDurationSeconds)
                        {
                            HttpWebRequest req = null;
                            try
                            {
                                req = (HttpWebRequest)WebRequest.Create(UploadUrl);
                                req.Method = "POST";
                                req.ContentType = "application/octet-stream";
                                req.KeepAlive = true;
                                req.AllowWriteStreamBuffering = false;
                                req.SendChunked = false;
                                req.ContentLength = PayloadPerPost;
                                req.Timeout = 6000;
                                req.ReadWriteTimeout = 6000;

                                using (phaseCts.Token.Register(() => { try { req.Abort(); } catch { } }))
                                {
                                    using (Stream reqStream = req.GetRequestStream())
                                    {
                                        int remaining = PayloadPerPost;
                                        while (remaining > 0)
                                        {
                                            if (phaseCts.IsCancellationRequested || phaseWatch.Elapsed.TotalSeconds >= PhaseDurationSeconds)
                                            {
                                                req.Abort();
                                                break;
                                            }

                                            int toWrite = Math.Min(sharedChunk.Length, remaining);
                                            reqStream.Write(sharedChunk, 0, toWrite);
                                            remaining -= toWrite;

                                            if (Volatile.Read(ref measurementActive) == 1)
                                            {
                                                Interlocked.Add(ref measuredBytes, toWrite);
                                            }
                                        }
                                    }

                                    if (!phaseCts.IsCancellationRequested && phaseWatch.Elapsed.TotalSeconds < PhaseDurationSeconds)
                                    {
                                        using (var resp = req.GetResponse())
                                        {
                                        }
                                    }
                                }
                            }
                            catch
                            {
                                if (phaseCts.IsCancellationRequested) break;
                                Thread.Sleep(40);
                            }
                        }
                    });
                }

                double lastMbps = 0;
                bool anyData = false;

                while (phaseWatch.Elapsed.TotalSeconds < PhaseDurationSeconds)
                {
                    outerToken.ThrowIfCancellationRequested();
                    await Task.Delay(80, outerToken);

                    double elapsedTotal = phaseWatch.Elapsed.TotalSeconds;
                    if (Volatile.Read(ref measurementActive) == 0 && elapsedTotal >= WarmupIgnoreSeconds)
                    {
                        measureWatch.Start();
                        Volatile.Write(ref measurementActive, 1);
                    }

                    if (Volatile.Read(ref measurementActive) == 1 && measureWatch.Elapsed.TotalSeconds > 0.08)
                    {
                        long bytes = Interlocked.Read(ref measuredBytes);
                        if (bytes > 0) anyData = true;
                        lastMbps = (bytes * 8.0) / (measureWatch.Elapsed.TotalSeconds * 1000000.0);
                        float frac = (float)Math.Min(1.0, elapsedTotal / PhaseDurationSeconds);
                        onSample(lastMbps, frac);
                    }
                }

                phaseCts.Cancel();
                try { await Task.WhenAll(workers); } catch { }

                outerToken.ThrowIfCancellationRequested();
                if (!anyData)
                {
                    throw new InvalidOperationException("Upload test failed.");
                }

                double finalSecs = Math.Max(0.1, measureWatch.Elapsed.TotalSeconds);
                return (Interlocked.Read(ref measuredBytes) * 8.0) / (finalSecs * 1000000.0);
            }
        }
    }
}
