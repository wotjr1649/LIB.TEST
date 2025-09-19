using Lib.Db.Abstractions;
using Lib.Db.Configuration;
using Lib.Db.Execution;
using Lib.Db.Resilience;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Lib.Db.Extensions;

/// <summary>
/// Lib.Log 과 유사한 개발 경험을 제공하기 위한 DI 등록 도우미입니다.
/// </summary>
using Microsoft.Extensions.Configuration;

namespace Lib.Db.Extensions;

/// <summary>
/// Lib.Log 과 유사한 개발 경험을 제공하기 위한 DI 등록 도우미입니다.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Lib.Db 데이터 계층을 구성 요소로 등록합니다.
    /// </summary>
    /// <param name="services">DI 컨테이너</param>
    /// <param name="configure">일반 데이터 옵션 바인딩 콜백</param>
    /// <param name="configureResilience">Polly 복원력 옵션 바인딩 콜백</param>
    public static IServiceCollection AddLibDb(
        this IServiceCollection services,
        Action<DbOptions>? configure = null,
        Action<DbResilienceOptions>? configureResilience = null)
    {
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IConfigureOptions<DbOptions>, ConfigureDbOptions>());

        var dbOptionsBuilder = services.AddOptions<DbOptions>();
        if (configure is not null)
        {
            dbOptionsBuilder.Configure(configure);
        }

        dbOptionsBuilder.PostConfigure(options =>
        {
            if (string.IsNullOrWhiteSpace(options.DefaultConnectionName))
            {
                options.DefaultConnectionName = "defaultDatabase";
            }
        });

        dbOptionsBuilder.Validate(static options => options.CommandTimeout > TimeSpan.Zero, "CommandTimeout must be positive");
        dbOptionsBuilder.Validate(static options => options.ConnectionStrings.Count >= 0, "ConnectionStrings must not be null");
        dbOptionsBuilder.ValidateOnStart();

        var resilienceBuilder = services.AddOptions<DbResilienceOptions>();
        if (configureResilience is not null)
        {
            resilienceBuilder.Configure(configureResilience);
        }

        resilienceBuilder.Validate(static options => options.Retry.MaxRetryAttempts >= 0, "MaxRetryAttempts must be non-negative");
        resilienceBuilder.ValidateOnStart();

        services.TryAddSingleton<DbResiliencePipelineProvider>();
        services.TryAddSingleton<IDbResiliencePipelineProvider>(sp => sp.GetRequiredService<DbResiliencePipelineProvider>());

        services.TryAddSingleton<SqlDataSourceFactory>();

        services.TryAddSingleton<DefaultDbClient>();
        services.TryAddSingleton<IDbClient>(sp => sp.GetRequiredService<DefaultDbClient>());

        return services;
    }
}

