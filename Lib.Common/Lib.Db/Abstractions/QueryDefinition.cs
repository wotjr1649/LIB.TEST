using System.Data;

namespace Lib.Db.Abstractions;

/// <summary>
/// SQL 텍스트 혹은 저장 프로시저를 실행하기 위해 필요한 모든 메타데이터를 캡슐화한 모델입니다.
/// </summary>
public sealed class QueryDefinition
{
    /// <summary>
    /// 실행할 SQL 텍스트 또는 저장 프로시저 이름입니다.
    /// </summary>
    public required string CommandText { get; init; }

    /// <summary>
    /// 실행 유형(텍스트/저장 프로시저 등)을 지정합니다. 기본값은 <see cref="CommandType.Text"/> 입니다.
    /// </summary>
    public CommandType CommandType { get; init; } = CommandType.Text;

    /// <summary>
    /// 명령에 바인딩할 파라미터 목록입니다.
    /// </summary>
    public IReadOnlyList<QueryParameter> Parameters { get; init; } = Array.Empty<QueryParameter>();

    /// <summary>
    /// 명령 실행 시간 제한 값입니다. 지정하지 않으면 <see cref="Configuration.DbOptions.CommandTimeout"/>이 사용됩니다.
    /// </summary>
    public TimeSpan? CommandTimeout { get; init; }

    /// <summary>
    /// 트랜잭션 격리 수준 힌트입니다. 지정하지 않으면 <see cref="Configuration.DbOptions.DefaultIsolationLevel"/>이 적용됩니다.
    /// </summary>
    public IsolationLevel? IsolationLevel { get; init; }

    /// <summary>
    /// 사용할 연결 문자열 이름입니다. 지정하지 않으면 <see cref="Configuration.DbOptions.DefaultConnectionName"/>을 사용합니다.
    /// </summary>
    public string? ConnectionName { get; init; }

    /// <summary>
    /// 파이프라인 미들웨어에서 사용할 수 있는 부가 정보(예: 로깅 태그)를 저장합니다.
    /// </summary>
    public object? Tag { get; init; }

    /// <summary>
    /// 일반 텍스트 쿼리를 생성합니다.
    /// </summary>
    public static QueryDefinition Text(string commandText, params QueryParameter[] parameters)
        => new() { CommandText = commandText, CommandType = CommandType.Text, Parameters = parameters };

    /// <summary>
    /// 저장 프로시저 호출 정의를 생성합니다.
    /// </summary>
    public static QueryDefinition StoredProcedure(string commandText, params QueryParameter[] parameters)
        => new() { CommandText = commandText, CommandType = CommandType.StoredProcedure, Parameters = parameters };
}

/// <summary>
/// 단일 데이터베이스 파라미터의 이름·값·형식을 정의합니다.
/// </summary>
public sealed record QueryParameter
{
    /// <summary>
    /// 파라미터 이름(예: "@UserId").
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// 전달할 값입니다. null일 경우 DB NULL로 바인딩됩니다.
    /// </summary>
    public object? Value { get; init; }

    /// <summary>
    /// 명시적 형 변환이 필요한 경우 설정할 <see cref="DbType"/> 입니다.
    /// </summary>
    public DbType? DbType { get; init; }

    /// <summary>
    /// Direction(입력/출력/입출력/ReturnValue) 설정입니다. 기본값은 입력입니다.
    /// </summary>
    public ParameterDirection Direction { get; init; } = ParameterDirection.Input;

    /// <summary>
    /// 문자열/바이너리 형식 등에 적용될 크기(Size)입니다.
    /// </summary>
    public int? Size { get; init; }

    /// <summary>
    /// 소수점 자릿수를 표현하기 위한 정밀도 값입니다.
    /// </summary>
    public byte? Precision { get; init; }

    /// <summary>
    /// 소수 부분 길이를 의미하는 스케일 값입니다.
    /// </summary>
    public byte? Scale { get; init; }
};
