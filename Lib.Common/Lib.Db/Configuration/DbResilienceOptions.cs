namespace Lib.Db.Configuration;

/// <summary>
/// Polly 복원력 파이프라인을 구성하기 위한 옵션 집합입니다.
/// </summary>
public sealed class DbResilienceOptions
{
    /// <summary>
    /// 복원력 기능 활성화 여부입니다. false이면 빈 파이프라인이 제공됩니다.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// 재시도 전략 설정입니다.
    /// </summary>
    public RetrySettings Retry { get; set; } = new();

    /// <summary>
    /// 타임아웃 전략 설정입니다.
    /// </summary>
    public TimeoutSettings Timeout { get; set; } = new();

    /// <summary>
    /// 회로 차단기(Circuit Breaker) 설정입니다.
    /// </summary>
    public CircuitBreakerSettings CircuitBreaker { get; set; } = new();

    /// <summary>
    /// 동시 수행량을 제한하는 벌크헤드 설정입니다.
    /// </summary>
    public BulkheadSettings Bulkhead { get; set; } = new();

    /// <summary>
    /// 단위 시간당 호출 횟수를 제한하는 레이트 리미터 설정입니다.
    /// </summary>
    public RateLimiterSettings RateLimiter { get; set; } = new();

    public sealed class RetrySettings
    {
        /// <summary>
        /// 최대 재시도 횟수입니다.
        /// </summary>
        public int MaxRetryAttempts { get; set; } = 3;

        /// <summary>
        /// 첫 번째 재시도 지연 시간입니다.
        /// </summary>
        public TimeSpan BaseDelay { get; set; } = TimeSpan.FromMilliseconds(200);

        /// <summary>
        /// 지수 백오프 곡선의 기울기입니다.
        /// </summary>
        public double BackoffExponent { get; set; } = 2.0d;

        /// <summary>
        /// 지연 시간에 난수 지터를 적용할지 여부입니다.
        /// </summary>
        public bool UseJitter { get; set; } = true;
    }

    public sealed class TimeoutSettings
    {
        /// <summary>
        /// 타임아웃 전략 사용 여부입니다.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// 단일 시도 당 허용할 최대 실행 시간입니다.
        /// </summary>
        public TimeSpan PerAttempt { get; set; } = TimeSpan.FromSeconds(30);
    }

    public sealed class CircuitBreakerSettings
    {
        /// <summary>
        /// 회로 차단기 활성화 여부입니다.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// 실패로 간주할 이벤트 누적 횟수입니다.
        /// </summary>
        public int FailureThreshold { get; set; } = 5;

        /// <summary>
        /// 실패율을 평가할 샘플링 윈도우입니다.
        /// </summary>
        public TimeSpan SamplingWindow { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>
        /// 회로가 열린 뒤 재시도까지 기다릴 시간입니다.
        /// </summary>
        public TimeSpan BreakDuration { get; set; } = TimeSpan.FromSeconds(15);
    }

    public sealed class BulkheadSettings
    {
        /// <summary>
        /// 벌크헤드 전략 사용 여부입니다.
        /// </summary>
        public bool Enabled { get; set; }

        /// <summary>
        /// 동시에 허용할 최대 실행 수입니다.
        /// </summary>
        public int MaxConcurrentExecutions { get; set; } = Environment.ProcessorCount * 4;

        /// <summary>
        /// 대기열에 쌓을 수 있는 최대 요청 수입니다.
        /// </summary>
        public int MaxQueuedActions { get; set; } = 1024;
    }

    public sealed class RateLimiterSettings
    {
        /// <summary>
        /// 레이트 리미터 사용 여부입니다.
        /// </summary>
        public bool Enabled { get; set; }

        /// <summary>
        /// 보유할 수 있는 허용량(permit) 개수입니다.
        /// </summary>
        public int PermitLimit { get; set; } = 100;

        /// <summary>
        /// 허용량을 다시 채우는 주기입니다.
        /// </summary>
        public TimeSpan ReplenishmentPeriod { get; set; } = TimeSpan.FromSeconds(1);
    }
}
