using System.Data;

namespace Lib.Db.Abstractions;

/// <summary>
/// 데이터베이스 명령을 비동기 방식으로 실행하기 위한 최상위 진입점 인터페이스입니다.
/// 모든 구현은 Polly 기반 복원력 파이프라인과 연결 수명 주기를 내부적으로 처리해야 합니다.
/// </summary>
public interface IDbClient : IAsyncDisposable
{
    /// <summary>
    /// INSERT/UPDATE/DELETE 계열과 같이 결과 집합이 필요 없는 명령을 실행합니다.
    /// </summary>
    /// <param name="query">실행할 SQL 또는 저장 프로시저 정보를 담은 <see cref="QueryDefinition"/> 인스턴스</param>
    /// <param name="cancellationToken">작업 취소를 제어하기 위한 토큰</param>
    /// <returns>영향을 받은 행의 수</returns>
    Task<int> ExecuteNonQueryAsync(QueryDefinition query, CancellationToken cancellationToken = default);

    /// <summary>
    /// 단일 스칼라 값을 반환하는 명령을 실행합니다.
    /// </summary>
    /// <typeparam name="T">반환 받을 스칼라 형식</typeparam>
    /// <param name="query">실행할 SQL 또는 저장 프로시저 메타데이터</param>
    /// <param name="cancellationToken">작업 취소를 제어하기 위한 토큰</param>
    /// <returns>첫 번째 행/첫 번째 열의 값을 형식 <typeparamref name="T"/>로 변환한 결과</returns>
    Task<T?> ExecuteScalarAsync<T>(QueryDefinition query, CancellationToken cancellationToken = default);

    /// <summary>
    /// 결과 집합을 비동기 열거 형태로 스트리밍합니다.
    /// </summary>
    /// <typeparam name="T">각 레코드를 투영할 대상 형식</typeparam>
    /// <param name="query">실행할 SQL 또는 저장 프로시저 메타데이터</param>
    /// <param name="projector">IDataRecord를 <typeparamref name="T"/>로 변환하는 투영 함수</param>
    /// <param name="cancellationToken">작업 취소를 제어하기 위한 토큰</param>
    /// <returns>데이터 행을 순차적으로 방출하는 <see cref="IAsyncEnumerable{T}"/></returns>
    IAsyncEnumerable<T> QueryAsync<T>(QueryDefinition query, Func<IDataRecord, T> projector, CancellationToken cancellationToken = default);
}
