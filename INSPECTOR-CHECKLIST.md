# Inspector Verification Checklist

## Команда для запуска Inspector

```bash
npx @modelcontextprotocol/inspector dotnet run --project src/ClrScope.Mcp
```

Запустить эту команду в отдельном терминале (требуется интерактивный режим).

## TestTargetApp

TestTargetApp уже запущен в фоне. Для получения PID:
```bash
pgrep -f TestTargetApp
```

Используйте этот PID для тестирования collect.dump/collect.trace.

---

## 1. tools/list verification

После запуска Inspector:
- [ ] Перейти во вкладку "Tools"
- [ ] Проверить, что все tool names в dotted формате:
  - system.health
  - system.capabilities
  - runtime.list_targets
  - runtime.inspect_target
  - collect.dump
  - collect.trace
  - artifact.get_metadata
  - artifact.list
  - artifact.delete
  - artifact.read_text
  - session.get
- [ ] Проверить titles на всех tools
- [ ] Проверить descriptions на всех tools и parameters
- [ ] Проверить metadata flags:
  - ReadOnly: system.health, system.capabilities, runtime.list_targets, runtime.inspect_target, artifact.get_metadata, artifact.list, artifact.read_text, session.get
  - Destructive: artifact.delete
  - Idempotent: system.health, system.capabilities, runtime.list_targets, runtime.inspect_target, artifact.get_metadata, artifact.list, artifact.read_text, session.get
  - OpenWorld: false на всех
  - UseStructuredContent: true на read-only tools
- [ ] Проверить JSON schema параметров на корректность

---

## 2. Success cases

### system.health
- [ ] Вызвать system.health
- [ ] Проверить ответ: IsHealthy, ArtifactRoot, FreeDiskSpaceBytes, DiagnosticsClientAvailable

### system.capabilities
- [ ] Вызвать system.capabilities
- [ ] Проверить ответ: NativeDumpAvailable, NativeTraceAvailable, TraceStatus, DotnetCountersInstalled, DotnetGcDumpInstalled, DotnetStackInstalled

### runtime.list_targets
- [ ] Вызвать runtime.list_targets
- [ ] Проверить ответ: список .NET процессов с Pid и ProcessName
- [ ] TestTargetApp должен быть в списке

### runtime.inspect_target
- [ ] Получить PID TestTargetApp через pgrep
- [ ] Вызвать runtime.inspect_target с этим PID
- [ ] Проверить ответ: Found=true, Attachable=true, ProcessName, CommandLine, OS, Architecture

### session.get
- [ ] Вызвать collect.dump на TestTargetApp (для создания сессии)
- [ ] Получить SessionId из ответа
- [ ] Вызвать session.get с этим SessionId
- [ ] Проверить ответ: Found=true, Kind=Dump, Status=Completed, ArtifactCount=1

### artifact.list
- [ ] Вызвать artifact.list
- [ ] Проверить ответ: список артефактов с ArtifactId, Kind, Status, FilePath, SizeBytes
- [ ] Вызвать artifact.list с фильтром kind=Dump
- [ ] Вызвать artifact.list с фильтром status=Completed

### collect.dump на TestTargetApp
- [ ] Получить PID TestTargetApp
- [ ] Вызвать collect.dump с pid=<PID>, includeHeap=true
- [ ] Проверить ответ: Success=true, SessionId, ArtifactId, FilePath, SizeBytes, Sha256
- [ ] Проверить, что файл dump создан в artifacts/dumps/

### collect.trace на TestTargetApp (experimental)
- [ ] Получить PID TestTargetApp
- [ ] Вызвать collect.trace с pid=<PID>, duration=00:00:30 (30 секунд)
- [ ] Проверить ответ: Success=true, SessionId, ArtifactId, FilePath, SizeBytes, Sha256
- [ ] Проверить, что файл trace создан в artifacts/traces/

---

## 3. Negative cases

### pid = -1 для collect.dump
- [ ] Вызвать collect.dump с pid=-1
- [ ] Проверить ответ: Success=false, Error="PID must be greater than 0"

### Несуществующий pid для runtime.inspect_target
- [ ] Вызвать runtime.inspect_target с pid=999999
- [ ] Проверить ответ: Found=false, Error содержит "process not found" или "not a .NET process"

### Несуществующий sessionId для session.get
- [ ] Вызвать session.get с sessionId="nonexistent-session-id"
- [ ] Проверить ответ: Found=false, Error="Session not found"

### Несуществующий artifactId для artifact.get_metadata
- [ ] Вызвать artifact.get_metadata с artifactId="nonexistent-artifact-id"
- [ ] Проверить ответ: Found=false, Error="Artifact not found"

### artifact.read_text на binary artifact
- [ ] Вызвать collect.dump для создания binary artifact
- [ ] Вызвать artifact.read_text с ArtifactId из dump
- [ ] Проверить ответ: Success=false, Error="File too large for text reading" или подобное

### Повторный artifact.delete
- [ ] Вызвать artifact.delete с ArtifactId
- [ ] Проверить ответ: Success=true
- [ ] Вызвать artifact.delete с тем же ArtifactId снова
- [ ] Проверить ответ: Success=false, Error="Artifact not found"

---

## 4. Logs / notifications

- [ ] Открыть вкладку "Logs" в Inspector
- [ ] Проверить, что server logs видны в stderr
- [ ] Проверить, что ошибки на boundary возвращаются как structured error responses
- [ ] (опционально) Если включен progress - проверить progress notifications

---

## 5. Артефакты для сохранения

После завершения:
- [ ] Screenshot вкладки "Tools" (список всех tools)
- [ ] Screenshot одного success call (например, collect.dump)
- [ ] Screenshot одного failure call (например, pid=-1)
- [ ] Создать INSPECTOR-VERIFICATION.md с отчётом

---

## Статус после прохождения

- [ ] PASS - все проверки пройдены
- [ ] NEEDS FIX - есть issues для исправления

Если PASS - можно переходить к Спринту 2 (Production Safety).
Если NEEDS FIX - исправить issues и повторить Inspector pass.
