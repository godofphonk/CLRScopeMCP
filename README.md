# CLRScope MCP Server

.NET diagnostics MCP server — MCP (Model Context Protocol) сервер для диагностики .NET процессов через EventPipe, memory dumps и CLI tools.

## Статус

**Версия:** 0.1.0
**Статус:** Core MCP Slice Ready for Testing

Завершены стадии:
- ✅ Stage 0a: Foundation + Native Core
- ✅ Stage 0c: MCP Integration Slice + Hardening
- ✅ Stage 0b: Management Layer

## Реализованные MCP Tools

Всего: **11 MCP tools** (placeholder CLI tools не зарегистрированы в MCP surface)

**Core Diagnostics (Stage 0a):**
- `system.health` — Проверка здоровья сервера
- `runtime.list_targets` — Список .NET процессов
- `runtime.inspect_target` — Детали процесса
- `collect.dump` — Сбор memory dump
- `collect.trace` — Сбор EventPipe trace (experimental)

**Management (Stage 0b):**
- `session.get` — Информация о сессии
- `artifact.get_metadata` — Метаданные артефакта
- `artifact.list` — Список артефактов
- `artifact.read_text` — Чтение текста артефакта
- `artifact.delete` — Удаление артефакта

**System Capabilities (Stage 0c.1):**
- `system.capabilities` — Отчёт о доступных возможностях

## Быстрый старт

### Требования
- .NET 10.0 SDK
- dotnet-counters, dotnet-gcdump, dotnet-stack (для CLI fallback)

### Сборка
```bash
dotnet build src/ClrScope.Mcp
```

### Запуск (MCP mode)
```bash
dotnet run --project src/ClrScope.Mcp
```

### Демо режим
```bash
dotnet run --project src/ClrScope.Mcp -- --demo
```

## Конфигурация

Создайте `appsettings.json`:
```json
{
  "ClrScope": {
    "ArtifactRoot": "~/.clrscope/artifacts",
    "DatabasePath": "~/.clrscope/clrscope.db"
  }
}
```

Или используйте environment variables:
- `CLRSCOPE__ArtifactRoot`
- `CLRSCOPE__DatabasePath`

## Документация

- [SUMMARY.md](docs/SUMMARY.md) — Обзор проекта
- [IMPLEMENTATION-ORDER.md](docs/IMPLEMENTATION-ORDER.md) — Порядок реализации
- [OVERVIEW.md](docs/architecture/OVERVIEW.md) — Архитектура
- [DECISIONS.md](docs/architecture/DECISIONS.md) — Архитектурные решения

**Completion reports:**
- [STAGE-0A-COMPLETION-REPORT.md](docs/reports/STAGE-0A-COMPLETION-REPORT.md)
- [STAGE-0C-MCP-INTEGRATION-REPORT.md](docs/reports/STAGE-0C-MCP-INTEGRATION-REPORT.md)
- [STAGE-0B-COMPLETION-REPORT.md](docs/reports/STAGE-0B-COMPLETION-REPORT.md)

## Известные ограничения

1. **collect.trace experimental** — имеет workaround для PC2 (session.Stop() висит)
2. **CLI fallback tools** — не реализованы, placeholder tools удалены из MCP surface (Stage 0c.1)
3. **Нет session.cancel** — запланирован для Stage 1
4. **Inspector verification** — отложено на Stage 1

## Технический стек

- C# 10 / .NET 10.0
- ModelContextProtocol SDK 1.2.0
- Microsoft.Diagnostics.NETCore.Client 0.2.661903
- SQLite (Microsoft.Data.Sqlite 9.0.0)
- Microsoft.Extensions.Hosting 9.0.0

## Лицензия

TODO
