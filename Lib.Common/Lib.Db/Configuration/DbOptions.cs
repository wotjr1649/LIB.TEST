using System.Data;

namespace Lib.Db.Configuration;

/// <summary>
/// 데이터 계층 전반에서 공통으로 참조할 기본 연결 및 명령 설정 모음입니다.
/// </summary>
public sealed class DbOptions
{
    /// <summary>
    /// 쿼리 정의에서 별도로 지정하지 않았을 때 사용할 기본 연결 문자열 키입니다.
    /// </summary>
    public string DefaultConnectionName { get; set; } = "defaultDatabase";

    /// <summary>
    /// 명령 실행 기본 타임아웃입니다. 개별 쿼리에서 <see cref="Abstractions.QueryDefinition.CommandTimeout"/>을 지정하면 그 값을 우선합니다.
    /// </summary>
    public TimeSpan CommandTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// 기본 트랜잭션 격리 수준입니다. 쿼리 정의에서 별도로 지정하거나, 트랜잭션 범위에서 재설정할 수 있습니다.
    /// </summary>
    public IsolationLevel DefaultIsolationLevel { get; set; } = IsolationLevel.ReadCommitted;

    /// <summary>
    /// 읽기 전용 시나리오일 경우 별도의 보조 연결(예: 리드 레플리카)을 선택하도록 미들웨어에서 참조하는 플래그입니다.
    /// </summary>
    public bool PreferReadOnlyConnection { get; set; }

    /// <summary>
    /// 구성 파일에서 바인딩되는 논리명-연결 문자열 매핑입니다.
    /// </summary>
    public Dictionary<string, string> ConnectionStrings { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}
