using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using StaffValidator.Checker.Utils;

namespace StaffValidator.Checker.Verifiers
{
    public class PerformanceVerifier
    {
        private readonly string _baseUrl;
        private readonly string _endpoint;
        private readonly int _concurrency;
        private readonly int _durationSec;
        private readonly string? _username;
        private readonly string? _password;
        private readonly bool _allowUnauth;

        public PerformanceVerifier(
            string baseUrl,
            string endpoint,
            int concurrency,
            int durationSec,
            string? username,
            string? password,
            bool allowUnauth)
        {
            _baseUrl = baseUrl;
            _endpoint = endpoint;
            _concurrency = concurrency;
            _durationSec = durationSec;
            _username = username;
            _password = password;
            _allowUnauth = allowUnauth;
        }

        public async Task<PerformanceResult> RunAsync()
        {
            Console.WriteLine($"Running performance test against {_baseUrl}{_endpoint} with concurrency={_concurrency}, duration={_durationSec}s");

            var handler = new SocketsHttpHandler
            {
                PooledConnectionIdleTimeout = TimeSpan.FromSeconds(30),
                PooledConnectionLifetime = TimeSpan.FromMinutes(2),
                MaxConnectionsPerServer = Math.Max(10, _concurrency * 2)
            };
            
            using var http = new HttpClient(handler) { BaseAddress = new Uri(_baseUrl), Timeout = TimeSpan.FromSeconds(15) };

            // Authenticate
            var token = await AuthHelper.AuthenticateAsync(http, _username, _password);
            if (!string.IsNullOrEmpty(token))
            {
                http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            }
            else if (!_allowUnauth && !string.IsNullOrEmpty(_username))
            {
                Console.WriteLine("Auth token not obtained and --allow-unauth not set; aborting perf test.");
                return new PerformanceResult { Success = 0, Errors = 1 };
            }

            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(_durationSec));
            var swTotal = Stopwatch.StartNew();
            var latencies = new List<long>(capacity: Math.Min(200_000, _concurrency * _durationSec * 2));
            var statusCounts = new Dictionary<int, int>();
            int success = 0, errors = 0;

            async Task Worker()
            {
                var rng = new Random();
                while (!cts.IsCancellationRequested)
                {
                    try
                    {
                        var sw = Stopwatch.StartNew();
                        using var resp = await http.GetAsync(_endpoint, cts.Token);
                        sw.Stop();
                        
                        lock (latencies) { latencies.Add(sw.ElapsedMilliseconds); }
                        
                        var code = (int)resp.StatusCode;
                        lock (statusCounts) { statusCounts[code] = statusCounts.TryGetValue(code, out var n) ? n + 1 : 1; }
                        
                        if (resp.IsSuccessStatusCode)
                        {
                            Interlocked.Increment(ref success);
                        }
                        else
                        {
                            Interlocked.Increment(ref errors);
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception)
                    {
                        Interlocked.Increment(ref errors);
                    }

                    await Task.Delay(rng.Next(1, 5));
                }
            }

            var tasks = Enumerable.Range(0, Math.Max(1, _concurrency)).Select(_ => Worker()).ToArray();
            await Task.WhenAll(tasks);
            swTotal.Stop();

            return CalculateMetrics(success, errors, swTotal.Elapsed.TotalSeconds, latencies, statusCounts);
        }

        private PerformanceResult CalculateMetrics(
            int success,
            int errors,
            double seconds,
            List<long> latencies,
            Dictionary<int, int> statusCounts)
        {
            int total = success + errors;
            double rps = total / Math.Max(0.001, seconds);
            
            long p50 = 0, p95 = 0, p99 = 0;
            double avg = 0;
            
            if (latencies.Count > 0)
            {
                latencies.Sort();
                avg = latencies.Average();
                p50 = Percentile(latencies, 50);
                p95 = Percentile(latencies, 95);
                p99 = Percentile(latencies, 99);
            }

            return new PerformanceResult
            {
                Total = total,
                Success = success,
                Errors = errors,
                Rps = rps,
                AvgMs = avg,
                P50Ms = p50,
                P95Ms = p95,
                P99Ms = p99,
                StatusCounts = statusCounts
            };
        }

        private long Percentile(List<long> sortedValues, int percentile)
        {
            if (sortedValues.Count == 0)
            {
                return 0;
            }
            if (percentile <= 0)
            {
                return sortedValues[0];
            }
            if (percentile >= 100)
            {
                return sortedValues[^1];
            }
            
            var rank = (percentile / 100.0) * (sortedValues.Count - 1);
            int low = (int)Math.Floor(rank);
            int high = (int)Math.Ceiling(rank);
            
            if (low == high)
            {
                return sortedValues[low];
            }
            
            double frac = rank - low;
            return (long)Math.Round(sortedValues[low] + frac * (sortedValues[high] - sortedValues[low]));
        }
    }

    public class PerformanceResult
    {
        public int Total { get; set; }
        public int Success { get; set; }
        public int Errors { get; set; }
        public double Rps { get; set; }
        public double AvgMs { get; set; }
        public long P50Ms { get; set; }
        public long P95Ms { get; set; }
        public long P99Ms { get; set; }
        public Dictionary<int, int> StatusCounts { get; set; } = new();
    }
}
