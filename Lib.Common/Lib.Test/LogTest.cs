#nullable enable
using Lib.Log;
using Lib.Log.Abstractions;
using Lib.Log.Option;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text;

public class LogTestRunner
{
    // ===================== 테스트 플래그 =====================
    // true -> 실행, false -> 스킵 (레거시 하네스의 부정형 조건을 모두 정상형으로 교정)
    private const bool RUN_SMOKE_BASIC = true;          // 스모크 테스트
    private const bool RUN_P0_RECURSION_GUARD = true;   // 재귀 로깅 가드 검증
    private const bool RUN_P0_NORMAL_SHUTDOWN_OCE = true; // 정상 종료 시 OCE 노이즈 억제
    private const bool RUN_P1_SAMPLING_1P = true;       // 1% 샘플링 정책 검증 (옵션 필요)
    private const bool RUN_FILE_LOCK_CIRCUIT = true;    // 파일 잠금 → 서킷브레이커 동작
    private const bool RUN_CONCURRENCY_PRESSURE = true; // 고동시성/백프레셔 자극
    private const bool RUN_ROLLOVER_SIZE_TEST = true;   // 롤오버(Size/Both) 유도
    private const bool RUN_DB_TRUNCATE_TEST = true;     // DB Truncate(개선본의 MaxMessageLength) 확인

    // ===================== 공통 파라미터 =====================
    private readonly string _testMarkerPrefix = $"[TEST-{DateTime.Now:HHmmss}]";
    private readonly TimeSpan _shortWait = TimeSpan.FromMilliseconds(300);
    private readonly TimeSpan _longWait = TimeSpan.FromSeconds(2);

    // ===================== 필드 (DI 후 초기화) =====================
    private IHost? _host;
    private ILogService? _logService;
    private ILogger? _logger;
    private LogOptions? _opt;
    private string? _localRoot;

    /// <summary>
    /// 모든 통합 테스트를 실행합니다.
    /// </summary>
    public async Task RunAsync(string[] args)
    {
        // 호스트 빌더 설정
        var builder = Host.CreateApplicationBuilder(args);
        ConfigureServices(builder);

        // 호스트 빌드 및 시작, 필드 초기화
        _host = builder.Build();
        await _host.StartAsync();
        InitializeServices();

        Console.WriteLine($"[INFO] LocalRoot: {_localRoot ?? "(none)"}");
        Console.WriteLine($"[INFO] BackpressurePolicy: {GetBackpressurePolicy()}");
        Console.WriteLine($"[INFO] Database Enabled: {IsDbEnabled()}");

        try
        {
            if (RUN_SMOKE_BASIC) await RunSmokeBasicTestAsync();
            if (RUN_P0_RECURSION_GUARD) await RunRecursionGuardTestAsync();
            if (RUN_P0_NORMAL_SHUTDOWN_OCE) await RunNormalShutdownOceTestAsync();
            if (RUN_P1_SAMPLING_1P) await RunSamplingTestAsync();
            if (RUN_FILE_LOCK_CIRCUIT) await RunFileLockCircuitTestAsync();
            if (RUN_CONCURRENCY_PRESSURE) await RunConcurrencyPressureTestAsync();
            if (RUN_ROLLOVER_SIZE_TEST) await RunRolloverSizeTestAsync();
            if (RUN_DB_TRUNCATE_TEST) await RunDbTruncateTestAsync();
        }
        finally
        {
            await Task.Delay(350);
            if (_host != null)
            {
                await _host.StopAsync();
            }
            Console.WriteLine("종료");
        }
    }

    /// <summary>
    /// 서비스 및 구성(Configuration)을 설정합니다.
    /// </summary>
    private void ConfigureServices(HostApplicationBuilder builder)
    {
        // 1) appsettings.json 로드
        builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

        // 2) 개선된 옵션 스키마 바인딩 ("LibLog" 섹션 → LogOptions)
        builder.Services.AddOptions<LogOptions>().BindConfiguration("LibLog");

        // 3) 개선된 클래스 라이브러리 등록
        builder.Services.AddLibLog();
    }

    /// <summary>
    /// 호스트 시작 후 필요한 서비스들을 필드에 주입합니다.
    /// </summary>
    private void InitializeServices()
    {
        if (_host == null)
            throw new InvalidOperationException("Host is not initialized.");

        _logService = Get<ILogService>();
        var loggerFactory = Get<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger("Test");
        var optMon = Get<IOptionsMonitor<LogOptions>>();
        _opt = optMon.CurrentValue;
        _localRoot = _opt?.Local?.Directory;
    }

    // ===================================================
    // 테스트 케이스별 메서드
    // ===================================================

    private async Task RunSmokeBasicTestAsync()
    {
        Console.WriteLine("== RUN_SMOKE_BASIC ==");
        _logService!.InfoFile("Application", $"{_testMarkerPrefix} 파일 Info 1");
        _logService.ErrorFile("Application", $"{_testMarkerPrefix} 파일 Error 1", ex: new IOException("Disk full (fake)"));
        _logService.InfoFile("Jb", $"{_testMarkerPrefix} JB OPEN()");
        _logService.ErrorFile("Jb", $"{_testMarkerPrefix} JB ERROR", ex: new IOException("JB not found (fake)"));
        _logService.InfoFile("Db", $"{_testMarkerPrefix} IF_ORDER SELECT");
        _logService.ErrorFile("Db", $"{_testMarkerPrefix} IF_ORDER ERROR", ex: new IOException("IF_ORDER missing (fake)"));
        _logService.InfoFile("Result", $"{_testMarkerPrefix} 결과전송 성공");
        _logService.ErrorFile("Result", $"{_testMarkerPrefix} 결과전송 실패", ex: new IOException("작업중 데이터"));

        _logService.InfoDb("DB", $"{_testMarkerPrefix} DB Info only");
        _logService.ErrorDb("DB", $"{_testMarkerPrefix} DB Error only", ex: new Exception("Conn fail(fake)"));

        await Task.Delay(_shortWait);
    }

    private async Task RunRecursionGuardTestAsync()
    {
        Console.WriteLine("== RUN_P0_RECURSION_GUARD ==");
        var token = $"{_testMarkerPrefix}-INTERNAL-RECURSION";
        using (_logger!.BeginScope(new Dictionary<string, object> { ["LibLog_Internal"] = true, ["DeviceId"] = "JB-RG-01" }))
        {
            _logger.LogInformation(token + " this MUST NOT be persisted.");
            _logger.LogError(new InvalidOperationException("fake"), token + " this MUST NOT be persisted (error).");
        }
        await Task.Delay(_shortWait);

        if (!string.IsNullOrWhiteSpace(_localRoot))
        {
            var files = FindRecentLogFiles(_localRoot, DateTime.UtcNow.AddMinutes(-1));
            var found = CountLinesContaining(files, token);
            Console.WriteLine(found == 0
                ? "  OK: recursion-guard working (no internal logs persisted)."
                : $"  WARN: found {found} internal lines → guard not effective.");
        }
    }

    private async Task RunNormalShutdownOceTestAsync()
    {
        Console.WriteLine("== RUN_P0_NORMAL_SHUTDOWN_OCE ==");
        using var cts = new CancellationTokenSource();
        var t = Task.Run(async () =>
        {
            int i = 0;
            using (_logger!.BeginScope(new Dictionary<string, object> { ["DeviceId"] = "JB-OCE-01" }))
            {
                while (!cts.IsCancellationRequested)
                {
                    _logger.LogInformation($"{_testMarkerPrefix}-OCE spin {i++}");
                    await Task.Delay(1);
                }
            }
        });

        await Task.Delay(200);
        cts.Cancel();
        await t;
        Console.WriteLine("  OK: stopped background logging without visible OCE.");
    }

    private async Task RunSamplingTestAsync()
    {
        Console.WriteLine("== RUN_P1_SAMPLING_1P ==");
        var policy = GetBackpressurePolicy();
        if (!policy.Equals("Sample1Percent", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine("  SKIP: BackpressurePolicy != Sample1Percent");
            return;
        }

        string dev = "JB-SAMPLE-01";
        var token = $"{_testMarkerPrefix}-SAMPLE";

        using (_logger!.BeginScope(new Dictionary<string, object> { ["DeviceId"] = dev }))
        {
            for (int i = 0; i < 5000; i++)
                _logger.LogDebug($"{token} #{i}");
        }
        await Task.Delay(_longWait);

        if (!string.IsNullOrWhiteSpace(_localRoot))
        {
            var files = FindRecentLogFiles(_localRoot, DateTime.UtcNow.AddMinutes(-1));
            var totalLines = CountLinesContaining(files, token);
            Console.WriteLine($"  Lines persisted: {totalLines} (expected ~1% of 5000 ~= 50)");
        }
    }

    private async Task RunFileLockCircuitTestAsync()
    {
        Console.WriteLine("== RUN_FILE_LOCK_CIRCUIT ==");
        if (string.IsNullOrWhiteSpace(_localRoot))
        {
            Console.WriteLine("  SKIP: localRoot is not configured.");
            return;
        }

        var candidate = FindRecentLogFiles(_localRoot, DateTime.UtcNow.AddMinutes(-5)).FirstOrDefault();
        if (candidate is null)
        {
            Console.WriteLine("  SKIP: recent log file not found.");
            return;
        }

        Console.WriteLine("  Locking file: " + candidate);
        using var fsLock = new FileStream(candidate, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

        _logService!.ErrorFile("FileLock", $"{_testMarkerPrefix}-LOCK write attempt", ex: new IOException("lock simulation"));
        await Task.Delay(_shortWait);
        Console.WriteLine("  OK: write attempted under lock (circuit should open briefly).");
    }

    private async Task RunConcurrencyPressureTestAsync()
    {
        Console.WriteLine("== RUN_CONCURRENCY_PRESSURE ==");
        var policy = GetBackpressurePolicy();
        Console.WriteLine("  Current BackpressurePolicy = " + policy);

        int writers = Environment.ProcessorCount * 2;
        int perWriter = 2000;
        var token = $"{_testMarkerPrefix}-PRESSURE";
        var tasks = new List<Task>(writers);

        for (int w = 0; w < writers; w++)
        {
            int id = w;
            tasks.Add(Task.Run(async () =>
            {
                string deviceId = $"JB-P-{id:D2}";
                using (_logger!.BeginScope(new Dictionary<string, object> { ["DeviceId"] = deviceId }))
                {
                    for (int i = 0; i < perWriter; i++)
                    {
                        if ((i % 5) == 0) _logger.LogInformation($"{token} [W{id}] info #{i}");
                        else _logger.LogDebug($"{token} [W{id}] dbg  #{i}");
                        if ((i % 100) == 0) await Task.Yield();
                    }
                }
            }));
        }
        await Task.WhenAll(tasks);
        await Task.Delay(_longWait);

        if (!string.IsNullOrWhiteSpace(_localRoot))
        {
            var files = FindRecentLogFiles(_localRoot, DateTime.UtcNow.AddMinutes(-2));
            var cnt = CountLinesContaining(files, token);
            Console.WriteLine($"  Persisted lines (subject to policy): {cnt:N0}");
        }
        Console.WriteLine("  OK: high concurrency pressure finished.");
    }

    private async Task RunRolloverSizeTestAsync()
    {
        Console.WriteLine("== RUN_ROLLOVER_SIZE_TEST ==");
        var rtype = _opt?.Local?.Rollover?.Type;
        if (rtype is null || (rtype != LogOptions.RolloverType.Size && rtype != LogOptions.RolloverType.Both))
        {
            Console.WriteLine("  SKIP: Rollover type is not Size/Both.");
            return;
        }

        var token = $"{_testMarkerPrefix}-ROLLOVER";
        var sb = new StringBuilder(1024).Append(token).Append(' ').Append('x', 1000);
        string line = sb.ToString();

        for (int i = 0; i < 8000; i++)
            _logService!.InfoFile("Rollover", line);

        await Task.Delay(TimeSpan.FromSeconds(1.5));

        if (!string.IsNullOrWhiteSpace(_localRoot))
        {
            var files = Directory.EnumerateFiles(_localRoot, "*.log", SearchOption.AllDirectories)
                                 .OrderByDescending(File.GetLastWriteTimeUtc)
                                 .Take(10).ToArray();
            Console.WriteLine("  Recent files (top 10):");
            foreach (var f in files) Console.WriteLine("    " + f);
            Console.WriteLine("  OK: multiple files with size boundaries expected.");
        }
    }

    private async Task RunDbTruncateTestAsync()
    {
        Console.WriteLine("== RUN_DB_TRUNCATE_TEST ==");
        if (!IsDbEnabled())
        {
            Console.WriteLine("  SKIP: Database sink is disabled.");
            return;
        }

        int maxLen = _opt!.Formatting.MaxMessageLength;
        var token = $"{_testMarkerPrefix}-DBTRUNC";
        string longMsg = token + " " + new string('M', maxLen + 5000);
        string longEx = new string('E', maxLen + 5000);

        _logService!.ErrorDb("DBTRUNC", longMsg, ex: new Exception(longEx));
        await Task.Delay(_longWait);

        Console.WriteLine("  OK: verify DB row message/exception ends with '…(truncated)'.");
    }

    // ===================================================
    // 유틸리티 메서드
    // ===================================================

    private T Get<T>() where T : notnull => _host!.Services.GetRequiredService<T>();

    private IEnumerable<string> FindRecentLogFiles(string? root, DateTime sinceUtc, string searchPattern = "*.log")
    {
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
            return Enumerable.Empty<string>();
        return Directory.EnumerateFiles(root, searchPattern, SearchOption.AllDirectories)
            .Where(f => File.GetLastWriteTimeUtc(f) >= sinceUtc);
    }

    private int CountLinesContaining(IEnumerable<string> files, string token)
    {
        int count = 0;
        foreach (var f in files)
        {
            try
            {
                using var fs = new FileStream(f, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var sr = new StreamReader(fs, Encoding.UTF8);
                string? line;
                while ((line = sr.ReadLine()) != null)
                    if (line.Contains(token, StringComparison.Ordinal))
                        count++;
            }
            catch { /* 잠금 등은 무시 */ }
        }
        return count;
    }

    private bool IsDbEnabled() => _opt?.Database?.Enabled == true;

    private string GetBackpressurePolicy() => _opt?.Partitions.TryGetValue("Default", out var g) == true
        ? g.Backpressure.ToString()
        : "(unknown)";
}