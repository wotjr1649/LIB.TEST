# Lib.Db Modern Async Roadmap

이 프로젝트는 Bowoo의 레거시 `bowoo.Lib` 데이터 계층을 대체하기 위한 .NET 8 기반 비동기 데이터 액세스 플랫폼입니다. 기존 라이브러리는 수정하지 않고 `Lib.Db` 내부에서만 현대화를 진행합니다.

## Guiding Principles
- 전 구간 `async/await` 및 `IAsyncDisposable` 사용
- Polly v8 `ResiliencePipeline` 으로 재시도/타임아웃/서킷브레이커/벌크헤드/폴백/레이트리밋 정책 구성
- DI/Options/HostedService/파이프라인 구조를 `Lib.Log`와 동일한 스타일로 재현
- Strongly-typed 파라미터/결과, Table-Valued Parameter, JSON, Output 등을 지원
- OpenTelemetry 기반 Observability 및 Health Check 기본 제공

## Phase 개요
1. **Foundation**: 폴더 구조, 옵션/DI 기본형, Async 인터페이스 선언
2. **Resilience**: Polly 파이프라인 구성, Resilience 옵션/프로필 정의
3. **Execution Core**: `DbCommandExecutor`, `DbDataSourceFactory`, ADO.NET 최신 비동기 API 래핑
4. **Binding & Mapping**: 파라미터 바인딩/결과 매핑을 비동기로 재작성, Source Generator 준비
5. **Pipeline**: `IAsyncQueryMiddleware` 기반 미들웨어 체인 구성
6. **Observability**: Activity/Meter, Health Check, HostedService 도입
7. **Advanced**: Batch/Bulk/Streaming, 샤딩/라우팅, 테스트 더블 제공
8. **Adoption**: 레거시 호출을 래퍼로 접속하고 점진적으로 교체

각 Phase는 완료 시 README 기록과 ADR 업데이트를 통해 공유합니다.
