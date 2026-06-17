---
name: Merge boundary diag fix
overview: "Этап 1 реализован: логи [POSC] границ merge в CMD. Этап 2 — фикс после лога с ВНИМАНИЕ."
todos:
  - id: diag-logs
    content: "Этап 1: ReportMergeBoundaryDiagnostic + TryWriteMergeBoundaryLine"
    status: completed
  - id: diag-policy
    content: SpecDiagPolicy.ShouldTraceMergeBoundarySummary + RowDetail
    status: completed
  - id: user-log-test
    content: "Проверка на 028_МВ_AR-rev2: CMD с [POSC] ВНИМАНИЕ"
    status: pending
  - id: fix-boundary
    content: "Этап 2: ResolveNextMarkBoundaryExclusive"
    status: pending
  - id: docs-verify
    content: "DEVELOPER.md обновлён; финальная проверка 0 предупреждений"
    status: pending
isProject: false
---

См. [plans/fix_merge_mark_boundary.md](../../plans/fix_merge_mark_boundary.md)
