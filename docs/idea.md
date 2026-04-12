Да. Ниже — **готовый blueprint MCP-сервера** под .NET profiling/diagnostics, чтобы можно было сразу раскладывать на код.

## 1) Как я бы это собрал

MCP логично использовать тремя слоями:

* **Tools** — для действий: снять dump, собрать trace, запустить monitor, прогнать SOS.
* **Resources** — для артефактов и состояния: список процессов, метаданные trace, summary по dump, сохраненные профили, правила.
* **Prompts** — для готовых workflow: “разобрать high CPU”, “поймать memory leak”, “снять hang bundle”.

Это хорошо совпадает с самим MCP: tools — вызываемые функции, resources — данные по URI, prompts — пользовательские шаблоны/сценарии; в спецификации также отдельно выделены logging и argument completion, которые здесь тоже очень полезны. ([modelcontextprotocol.io][1])

---

## 2) Главная идея API

**Не делай MCP как “тонкую обертку над CLI-командами”.**
Делай его как **единый диагностический домен**:

* цель = **target**
* операция = **session**
* результат = **artifact**
* преднастройка = **profile**
* автосбор = **rule**

Тогда клиенту не нужно знать, где именно внутри использовались `dotnet-trace`, `dotnet-monitor`, `PerfCollect` или `dotnet-dump`. Он работает в стабильных абстракциях.

---

## 3) Канонические сущности

### `TargetRef`

```json
{
  "kind": "process",
  "pid": 12345,
  "processName": "MyService",
  "runtime": ".NET 8.0.5",
  "os": "linux",
  "container": {
    "isContainerized": true,
    "containerId": "9c1d..."
  }
}
```

### `DiagnosticSession`

```json
{
  "sessionId": "sess_01HTR8N9F2L9",
  "kind": "trace",
  "status": "running",
  "target": {
    "pid": 12345,
    "processName": "MyService"
  },
  "startedAt": "2026-04-12T10:03:12Z",
  "profile": "cpu-sampling",
  "toolBackend": "dotnet-trace",
  "artifacts": []
}
```

### `ArtifactRef`

```json
{
  "artifactId": "art_01HTR8P7AF2X",
  "kind": "trace",
  "format": "nettrace",
  "sizeBytes": 18432044,
  "createdAt": "2026-04-12T10:04:03Z",
  "uri": "diag://artifacts/art_01HTR8P7AF2X",
  "downloadUri": "diag://artifacts/art_01HTR8P7AF2X/file"
}
```

### `OperationResult`

```json
{
  "ok": true,
  "sessionId": "sess_01HTR8N9F2L9",
  "artifacts": [
    {
      "artifactId": "art_01HTR8P7AF2X",
      "kind": "trace",
      "format": "nettrace",
      "uri": "diag://artifacts/art_01HTR8P7AF2X"
    }
  ],
  "summary": {
    "message": "Trace collected successfully"
  },
  "warnings": []
}
```

---

## 4) Именование tools

Я бы взял **плоские, но группируемые имена** через точки:

### Базовые

* `runtime.list_targets`
* `runtime.inspect_target`
* `runtime.list_runtimes`
* `session.get`
* `session.cancel`
* `artifact.list`
* `artifact.get_metadata`
* `artifact.read_text`
* `artifact.delete`

### Сбор данных

* `collect.trace`
* `collect.counters`
* `collect.dump`
* `collect.gcdump`
* `collect.stacks`

### Продвинутые

* `monitor.start`
* `monitor.stop`
* `monitor.capture`
* `monitor.apply_rule`
* `symbols.fetch`
* `analyze.dump_sos`
* `linux.perfcollect`
* `mobile.dsrouter_start`
* `mobile.dsrouter_stop`

### Workflow-уровень

* `workflow.capture_high_cpu_bundle`
* `workflow.capture_memory_leak_bundle`
* `workflow.capture_hang_bundle`
* `workflow.postmortem_bundle`

---

## 5) Что должно быть в core tools

## `runtime.list_targets`

Назначение: показать процессы, к которым вообще можно подключаться.

```json
{
  "type": "object",
  "properties": {
    "includeNativeDetails": { "type": "boolean", "default": false },
    "includeContainers": { "type": "boolean", "default": true },
    "filter": { "type": "string" }
  },
  "additionalProperties": false
}
```

**Возвращает** список target-ов с pid, processName, runtimeVersion, user, container info, diagnostic-port status.

---

## `runtime.inspect_target`

Назначение: дать компактный summary перед профилированием.

```json
{
  "type": "object",
  "required": ["pid"],
  "properties": {
    "pid": { "type": "integer", "minimum": 1 },
    "includeModules": { "type": "boolean", "default": false },
    "includeEnvironment": { "type": "boolean", "default": false },
    "includePorts": { "type": "boolean", "default": true }
  },
  "additionalProperties": false
}
```

**Возвращает**:

* runtime info
* архитектуру
* стартовое время
* container / host mode
* примерные риски: “dump может быть тяжелым”, “tmpdir mismatch”, “sidecar не настроен”.

Это особенно полезно, потому что `dotnet-dump` и другие CLI чувствительны к user/TMPDIR/container-условиям. На Linux/macOS `dotnet-dump collect` ожидает одинаковый `TMPDIR`, а запускать его нужно тем же пользователем или от root; в контейнерах `dotnet-dump` и `dotnet-gcdump` могут заметно потреблять память/диск. ([Microsoft Learn][2])

---

## `collect.trace`

Главный инструмент для CPU/event tracing. `dotnet-trace` кроссплатформенный, работает через EventPipe и собирает trace без native profiler. ([Microsoft Learn][3])

```json
{
  "type": "object",
  "required": ["pid"],
  "properties": {
    "pid": { "type": "integer", "minimum": 1 },
    "duration": {
      "type": "string",
      "description": "ISO-8601 duration, e.g. PT30S"
    },
    "profile": {
      "type": "string",
      "enum": [
        "cpu-sampling",
        "gc-verbose",
        "exceptions",
        "aspnetcore",
        "threadpool",
        "custom"
      ],
      "default": "cpu-sampling"
    },
    "providers": {
      "type": "array",
      "items": {
        "type": "object",
        "properties": {
          "name": { "type": "string" },
          "keywords": { "type": "string" },
          "level": { "type": "string" },
          "arguments": { "type": "string" }
        },
        "required": ["name"]
      }
    },
    "outputFormat": {
      "type": "string",
      "enum": ["nettrace", "speedscope", "json"],
      "default": "nettrace"
    },
    "stoppingCondition": {
      "type": "object",
      "properties": {
        "maxSizeMb": { "type": "integer", "minimum": 1 },
        "timeoutSeconds": { "type": "integer", "minimum": 1 }
      },
      "additionalProperties": false
    }
  },
  "additionalProperties": false
}
```

### Что важно по реализации

* `nettrace` — канонический артефакт.
* `speedscope` — удобный экспорт для flamegraph UX.
* длинные операции лучше делать **session-based**: старт, polling через `session.get`, потом артефакт в resources.

`.NET` документация прямо описывает `dotnet-trace` как инструмент на EventPipe, а также поддерживает форматы вроде `.nettrace` и `.speedscope.json`, что делает `speedscope` хорошим форматом для MCP-экспорта. ([Microsoft Learn][3])

---

## `collect.counters`

`dotnet-counters` — для ad-hoc health monitoring и first-level investigation; он читает EventCounter и Meter API. ([Microsoft Learn][4])

```json
{
  "type": "object",
  "required": ["pid"],
  "properties": {
    "pid": { "type": "integer", "minimum": 1 },
    "mode": {
      "type": "string",
      "enum": ["monitor", "collect"],
      "default": "monitor"
    },
    "duration": {
      "type": "string",
      "description": "Required for collect mode"
    },
    "refreshIntervalSeconds": {
      "type": "number",
      "minimum": 0.2,
      "default": 1
    },
    "counters": {
      "type": "array",
      "items": { "type": "string" }
    },
    "outputFormat": {
      "type": "string",
      "enum": ["json", "csv"],
      "default": "json"
    }
  },
  "additionalProperties": false
}
```

### Практика

* `monitor` — вернуть **stream-like snapshots** через session/log events.
* `collect` — писать временной ряд в artifact.

---

## `collect.dump`

`dotnet-dump` умеет сбор и анализ дампов на Windows, Linux и macOS без native debugger; типы dump включают `Full`, `Heap`, `Mini`, `Triage`. Это хороший базовый backend для post-mortem и crash/hang сценариев. ([Microsoft Learn][2])

```json
{
  "type": "object",
  "required": ["pid"],
  "properties": {
    "pid": { "type": "integer", "minimum": 1 },
    "dumpType": {
      "type": "string",
      "enum": ["Full", "Heap", "Mini", "Triage"],
      "default": "Heap"
    },
    "compress": { "type": "boolean", "default": true },
    "includeCrashReport": { "type": "boolean", "default": false },
    "reason": { "type": "string" },
    "tags": {
      "type": "array",
      "items": { "type": "string" }
    }
  },
  "additionalProperties": false
}
```

### Рекомендация

Для production по умолчанию:

* `Heap` для memory/hang
* `Mini` для быстрых инцидентов
* `Full` только осознанно

---

## `collect.gcdump`

`dotnet-gcdump` собирает GC dump живого процесса через EventPipe и обычно с меньшим overhead, чем полный dump; это очень хороший default-инструмент для leak/growth сценариев. ([Microsoft Learn][5])

```json
{
  "type": "object",
  "required": ["pid"],
  "properties": {
    "pid": { "type": "integer", "minimum": 1 },
    "reason": { "type": "string" },
    "collectLiveSizeHint": { "type": "boolean", "default": true }
  },
  "additionalProperties": false
}
```

---

## `collect.stacks`

`dotnet-stack` быстро печатает managed stacks всех потоков и даже умеет `symbolicate`; это почти идеальный “быстрый snapshot” при hang/deadlock/starvation. ([Microsoft Learn][6])

```json
{
  "type": "object",
  "required": ["pid"],
  "properties": {
    "pid": { "type": "integer", "minimum": 1 },
    "symbolicate": { "type": "boolean", "default": true },
    "repeat": {
      "type": "object",
      "properties": {
        "count": { "type": "integer", "minimum": 1, "maximum": 20 },
        "intervalMs": { "type": "integer", "minimum": 100 }
      },
      "additionalProperties": false
    }
  },
  "additionalProperties": false
}
```

### Очень полезный режим

Сделай `repeat` для 3–5 stack snapshots подряд.
Это отлично показывает:

* стоит ли поток на одном и том же месте,
* starvation это или просто загрузка,
* есть ли lock convoy.

---

## 6) Advanced tools, которые реально стоит включить

## `monitor.start`

`dotnet-monitor` — один из лучших production-oriented инструментов: может собирать dumps, traces, logs, metrics по запросу и по правилам, плюс есть global tool и Docker image для multi-container сценариев. ([Microsoft Learn][7])

```json
{
  "type": "object",
  "properties": {
    "mode": {
      "type": "string",
      "enum": ["connect", "listen"],
      "default": "connect"
    },
    "diagnosticPort": { "type": "string" },
    "urls": {
      "type": "array",
      "items": { "type": "string" }
    },
    "metricsEnabled": { "type": "boolean", "default": true },
    "metricUrls": {
      "type": "array",
      "items": { "type": "string" }
    },
    "auth": {
      "type": "object",
      "properties": {
        "mode": {
          "type": "string",
          "enum": ["temporary-api-key", "external", "disabled"],
          "default": "temporary-api-key"
        }
      },
      "additionalProperties": false
    },
    "httpEgressEnabled": { "type": "boolean", "default": false }
  },
  "additionalProperties": false
}
```

### Что возвращать

```json
{
  "ok": true,
  "monitorSessionId": "mon_01HTR...",
  "baseUrl": "https://127.0.0.1:52323",
  "metricsUrl": "http://127.0.0.1:52325/metrics",
  "apiKeyIssued": true
}
```

---

## `monitor.capture`

Единый фасад над `dotnet-monitor` для on-demand сборов.

```json
{
  "type": "object",
  "required": ["monitorSessionId", "artifactKind"],
  "properties": {
    "monitorSessionId": { "type": "string" },
    "artifactKind": {
      "type": "string",
      "enum": ["trace", "dump", "gcdump", "logs", "metrics"]
    },
    "targetPid": { "type": "integer" },
    "duration": { "type": "string" },
    "profile": { "type": "string" }
  },
  "additionalProperties": false
}
```

---

## `monitor.apply_rule`

Нужен для автоматических prod-сценариев.

```json
{
  "type": "object",
  "required": ["monitorSessionId", "rule"],
  "properties": {
    "monitorSessionId": { "type": "string" },
    "rule": {
      "type": "object",
      "required": ["name", "trigger", "action"],
      "properties": {
        "name": { "type": "string" },
        "trigger": {
          "type": "object",
          "properties": {
            "kind": {
              "type": "string",
              "enum": [
                "cpu-above-threshold",
                "gc-heap-growth",
                "exception-rate",
                "counter-threshold"
              ]
            },
            "threshold": { "type": "number" },
            "durationSeconds": { "type": "integer" }
          },
          "required": ["kind"]
        },
        "action": {
          "type": "object",
          "properties": {
            "collect": {
              "type": "array",
              "items": {
                "type": "string",
                "enum": ["trace", "dump", "gcdump", "logs", "metrics"]
              }
            }
          },
          "required": ["collect"]
        }
      }
    }
  },
  "additionalProperties": false
}
```

---

## `symbols.fetch`

`dotnet-symbol` нужен для нормального разбора dump-ов, особенно когда они сняты на другой машине. ([Microsoft Learn][8])

```json
{
  "type": "object",
  "oneOf": [
    {
      "type": "object",
      "required": ["artifactId"],
      "properties": {
        "artifactId": { "type": "string" },
        "symbolServers": {
          "type": "array",
          "items": { "type": "string" }
        }
      }
    },
    {
      "type": "object",
      "required": ["modules"],
      "properties": {
        "modules": {
          "type": "array",
          "items": { "type": "string" }
        },
        "symbolServers": {
          "type": "array",
          "items": { "type": "string" }
        }
      }
    }
  ]
}
```

---

## `analyze.dump_sos`

Это must-have. Пользователь должен уметь прогонять типовые SOS-команды по dump-у, не выходя из MCP.

```json
{
  "type": "object",
  "required": ["artifactId", "commands"],
  "properties": {
    "artifactId": { "type": "string" },
    "commands": {
      "type": "array",
      "items": {
        "type": "string",
        "enum": [
          "threads",
          "clrstack-all",
          "dumpheap-stat",
          "dumpheap-types",
          "gcroot",
          "eeheap-gc",
          "finalizequeue",
          "syncblk",
          "threadpool",
          "analyzeoom"
        ]
      }
    },
    "arguments": {
      "type": "object",
      "properties": {
        "typeName": { "type": "string" },
        "objectAddress": { "type": "string" }
      },
      "additionalProperties": true
    }
  },
  "additionalProperties": false
}
```

### Мой совет

Не давай произвольный raw shell по SOS в первом релизе.
Лучше сначала сделать **whitelist команд** и нормализованные structured outputs.

---

## `linux.perfcollect`

`PerfCollect` полезен на Linux для kernel events, CPU samples и context switches; в контейнерах ему нужны специальные условия. Microsoft отдельно пишет про `DOTNET_PerfMapEnabled=1`, `DOTNET_EnableEventLog=1`, `SYS_ADMIN` и общий process namespace. ([Microsoft Learn][9])

```json
{
  "type": "object",
  "required": ["pid"],
  "properties": {
    "pid": { "type": "integer", "minimum": 1 },
    "duration": { "type": "string" },
    "profile": {
      "type": "string",
      "enum": ["cpu-kernel", "cpu-context-switch", "full"],
      "default": "cpu-kernel"
    },
    "containerMode": {
      "type": "string",
      "enum": ["auto", "same-container", "sidecar", "host"],
      "default": "auto"
    }
  },
  "additionalProperties": false
}
```

### До запуска валидируй

* OS = Linux
* есть ли нужные env vars
* есть ли capability
* shared process namespace / `/tmp` sharing для sidecar-сценариев

---

## `mobile.dsrouter_start`

`dotnet-dsrouter` нужен для Android/iOS/tvOS: он маршрутизирует диагностическое соединение для `dotnet-trace` и `dotnet-counters` к sandboxed apps. ([Microsoft Learn][10])

```json
{
  "type": "object",
  "required": ["platform"],
  "properties": {
    "platform": {
      "type": "string",
      "enum": ["android", "ios", "tvos"]
    },
    "mode": {
      "type": "string",
      "enum": ["server-server", "server-client"],
      "default": "server-server"
    },
    "localPort": { "type": "integer" },
    "remoteEndpoint": { "type": "string" }
  },
  "additionalProperties": false
}
```

---

## 7) Workflow tools — самое ценное для LLM-клиента

Вот здесь начинается магия.
Обычный пользователь не хочет помнить все флаги. Он хочет сказать:
**“поймай high CPU на проде и сохрани всё нужное”**.

### `workflow.capture_high_cpu_bundle`

```json
{
  "type": "object",
  "required": ["pid"],
  "properties": {
    "pid": { "type": "integer", "minimum": 1 },
    "traceDuration": { "type": "string", "default": "PT30S" },
    "includeCounters": { "type": "boolean", "default": true },
    "includeStacks": { "type": "boolean", "default": true },
    "exportSpeedscope": { "type": "boolean", "default": true }
  },
  "additionalProperties": false
}
```

**Что делает внутри**

1. `collect.counters`
2. `collect.stacks`
3. `collect.trace`
4. экспортирует summary + flamegraph-ready artifact

---

### `workflow.capture_memory_leak_bundle`

```json
{
  "type": "object",
  "required": ["pid"],
  "properties": {
    "pid": { "type": "integer", "minimum": 1 },
    "strategy": {
      "type": "string",
      "enum": ["gcdump-first", "heap-dump-first"],
      "default": "gcdump-first"
    },
    "repeat": {
      "type": "object",
      "properties": {
        "count": { "type": "integer", "default": 2 },
        "interval": { "type": "string", "default": "PT5M" }
      }
    },
    "includeCounters": { "type": "boolean", "default": true }
  },
  "additionalProperties": false
}
```

---

### `workflow.capture_hang_bundle`

```json
{
  "type": "object",
  "required": ["pid"],
  "properties": {
    "pid": { "type": "integer", "minimum": 1 },
    "stackSnapshots": { "type": "integer", "default": 3 },
    "snapshotIntervalMs": { "type": "integer", "default": 2000 },
    "includeMiniDump": { "type": "boolean", "default": true }
  },
  "additionalProperties": false
}
```

---

### `workflow.postmortem_bundle`

```json
{
  "type": "object",
  "required": ["artifactId"],
  "properties": {
    "artifactId": { "type": "string" },
    "fetchSymbols": { "type": "boolean", "default": true },
    "runDefaultSosPack": { "type": "boolean", "default": true }
  },
  "additionalProperties": false
}
```

---

## 8) Resources — что обязательно отдать через URI

Resources в MCP по спецификации адресуются URI и хорошо подходят для “прочитать состояние/артефакт”, а не “выполнить действие”. ([modelcontextprotocol.io][11])

Я бы сделал такие URI:

### Targets

* `diag://targets`
* `diag://targets/{pid}`
* `diag://targets/{pid}/modules`
* `diag://targets/{pid}/environment`
* `diag://targets/{pid}/ports`

### Sessions

* `diag://sessions`
* `diag://sessions/{sessionId}`
* `diag://sessions/{sessionId}/timeline`
* `diag://sessions/{sessionId}/logs`

### Artifacts

* `diag://artifacts`
* `diag://artifacts/{artifactId}`
* `diag://artifacts/{artifactId}/metadata`
* `diag://artifacts/{artifactId}/summary`
* `diag://artifacts/{artifactId}/file`
* `diag://artifacts/{artifactId}/preview`

### Аналитика

* `diag://artifacts/{artifactId}/sos-report`
* `diag://artifacts/{artifactId}/threads`
* `diag://artifacts/{artifactId}/heap-stat`
* `diag://artifacts/{artifactId}/gc-summary`

### Конфиг/справочники

* `diag://profiles`
* `diag://rules`
* `diag://tooling/backends`
* `diag://health`

---

## 9) Prompts — что реально удобно пользователю

Prompts в MCP задуманы как **user-controlled workflows**, и тут они очень к месту. ([modelcontextprotocol.io][12])

Я бы зарегистрировал такие prompts:

### `investigate-high-cpu`

Аргументы:

* `pid`
* `duration`
* `isProduction`
* `needFlamegraph`

### `investigate-memory-leak`

Аргументы:

* `pid`
* `symptom`
* `duration`
* `repeatCount`

### `investigate-hang-or-deadlock`

Аргументы:

* `pid`
* `appType`
* `includeDump`

### `analyze-dump`

Аргументы:

* `artifactId`
* `suspectedIssue` (`oom`, `deadlock`, `gc`, `unknown`)

### `prepare-prod-safe-capture`

Аргументы:

* `pid`
* `env` (`dev`, `staging`, `prod`)
* `allowFullDump`
* `containerized`

### `explain-artifact`

Аргументы:

* `artifactId`
* `audience` (`developer`, `sre`, `manager`)

---

## 10) Profiles — очень советую сделать как отдельную сущность

Чтобы не таскать все провайдеры руками, сделай **diag profiles**.

### Примеры профилей

* `cpu-sampling`
* `aspnetcore-latency`
* `threadpool-starvation`
* `gc-pressure`
* `exception-storm`
* `startup`
* `memory-growth-watch`
* `prod-safe-minimal`

### Resource

* `diag://profiles`

### Пример описания профиля

```json
{
  "name": "threadpool-starvation",
  "description": "Counters + repeated stacks + short trace",
  "steps": [
    { "tool": "collect.counters", "args": { "mode": "monitor" } },
    { "tool": "collect.stacks", "args": { "repeat": { "count": 3, "intervalMs": 1500 } } },
    { "tool": "collect.trace", "args": { "duration": "PT20S", "profile": "threadpool" } }
  ]
}
```

---

## 11) Output contract — сделай единым для всех tools

Это очень поможет LLM-клиенту.

```json
{
  "ok": true,
  "sessionId": "sess_...",
  "target": {
    "pid": 12345,
    "processName": "MyService"
  },
  "artifacts": [
    {
      "artifactId": "art_...",
      "kind": "trace",
      "format": "nettrace",
      "uri": "diag://artifacts/art_..."
    }
  ],
  "summary": {
    "title": "CPU trace collected",
    "highlights": [
      "30s trace completed",
      "speedscope export created"
    ]
  },
  "warnings": [],
  "nextSuggestedTools": [
    "artifact.read_text",
    "analyze.dump_sos"
  ]
}
```

### Почему это важно

LLM сможет:

* понять, что произошло,
* прочитать нужный resource,
* сам выбрать следующий шаг.

---

## 12) Completions и logging — очень стоит добавить

В MCP есть отдельные utilities для **logging** и **argument completion**. Для диагностического сервера это прямо полезно. ([modelcontextprotocol.io][1])

### Completion suggestions

* `pid`
* `processName`
* `profile`
* `dumpType`
* `SOS command`
* `counter names`
* `provider names`

### Structured logs

Примеры:

```json
{
  "level": "info",
  "event": "trace_started",
  "sessionId": "sess_...",
  "pid": 12345
}
```

```json
{
  "level": "warning",
  "event": "container_sidecar_tmp_not_shared",
  "pid": 12345
}
```

---

## 13) Guardrails, без которых сервер будет больно использовать

## Контейнеры

В .NET контейнерные сценарии официально поддерживаются, но их надо явно валидировать: tools можно запускать в том же контейнере, с хоста или из sidecar; при этом `dotnet-dump`/`dotnet-gcdump` могут быть тяжелыми по памяти и диску. Для sidecar-диагностики CLI tools нужен общий `/tmp`, а для `PerfCollect` — общий process namespace и `SYS_ADMIN`; также `PerfCollect` требует `DOTNET_PerfMapEnabled=1` и `DOTNET_EnableEventLog=1`. ([Microsoft Learn][13])

## Dump safety

`dotnet-dump` не native debugger, поэтому native stack frames он не покажет; это надо сразу отражать в summary. Кроме того, dump collection на Linux/macOS чувствительна к `TMPDIR` и пользователю процесса. ([Microsoft Learn][2])

## Monitoring posture

`dotnet-monitor` умеет выдавать metrics и диагностические артефакты on-demand или по rules, а его `--no-auth` Microsoft явно не рекомендует для production. ([Microsoft Learn][7])

---

## 14) Минимальный v1, который я бы реально выпускал

Если хочешь **сразу всё**, я бы всё равно собирал в таком порядке:

### Этап 1 — backbone

* `runtime.list_targets`
* `runtime.inspect_target`
* `collect.trace`
* `collect.counters`
* `collect.dump`
* `collect.gcdump`
* `collect.stacks`
* `artifact.*`
* `session.*`

### Этап 2 — production

* `monitor.start`
* `monitor.capture`
* `monitor.apply_rule`
* `symbols.fetch`
* `analyze.dump_sos`

### Этап 3 — platform-specific

* `linux.perfcollect`
* `mobile.dsrouter_start/stop`

### Этап 4 — UX

* `workflow.*`
* prompts
* completions
* summaries / recommendations

---

## 15) Моя рекомендуемая структура проекта

```text
src/
  Server/
    McpServer.cs
    ToolRegistry.cs
    ResourceRegistry.cs
    PromptRegistry.cs

  Domain/
    Targets/
    Sessions/
    Artifacts/
    Profiles/
    Rules/

  Backends/
    DotnetTraceBackend/
    DotnetCountersBackend/
    DotnetDumpBackend/
    DotnetGcDumpBackend/
    DotnetStackBackend/
    DotnetMonitorBackend/
    DotnetSymbolBackend/
    PerfCollectBackend/
    DsRouterBackend/
    SosBackend/

  Workflows/
    HighCpuWorkflow.cs
    MemoryLeakWorkflow.cs
    HangWorkflow.cs
    PostmortemWorkflow.cs

  Summaries/
    TraceSummaryBuilder.cs
    DumpSummaryBuilder.cs
    GcDumpSummaryBuilder.cs
    StackSummaryBuilder.cs

  Storage/
    ArtifactStore.cs
    SessionStore.cs
    ResourceProjector.cs
```

---

## 16) Что бы я сделал твоим “золотым стандартом”

Если коротко, я бы считал эталонным такой набор:

* **CLI-backed collectors**: `trace / counters / dump / gcdump / stacks`
* **production orchestrator**: `dotnet-monitor`
* **postmortem support**: `dotnet-symbol + SOS`
* **Linux deep profiling**: `PerfCollect`
* **mobile bridge**: `dotnet-dsrouter`
* **LLM UX layer**: `workflow.* + prompts + resources + summaries`

То есть не просто “MCP для запуска команд”, а **MCP-оператор диагностики .NET**.

Ниже — хороший стартовый набор имён tools, который можно почти без изменений внедрять:

```text
runtime.list_targets
runtime.inspect_target

collect.trace
collect.counters
collect.dump
collect.gcdump
collect.stacks

monitor.start
monitor.stop
monitor.capture
monitor.apply_rule

symbols.fetch
analyze.dump_sos
linux.perfcollect
mobile.dsrouter_start
mobile.dsrouter_stop

session.get
session.cancel

artifact.list
artifact.get_metadata
artifact.read_text
artifact.delete

workflow.capture_high_cpu_bundle
workflow.capture_memory_leak_bundle
workflow.capture_hang_bundle
workflow.postmortem_bundle
```

Следующим сообщением могу дать тебе **прямо готовый C#-скелет MCP сервера**: интерфейсы backend-ов, DTO, registry tools и пример реализации `collect.trace` и `collect.dump`.

[1]: https://modelcontextprotocol.io/specification/2025-06-18/basic "Overview - Model Context Protocol"
[2]: https://learn.microsoft.com/en-us/dotnet/core/diagnostics/dotnet-dump "dotnet-dump diagnostic tool - .NET CLI - .NET | Microsoft Learn"
[3]: https://learn.microsoft.com/en-us/dotnet/core/diagnostics/dotnet-trace?utm_source=chatgpt.com "dotnet-trace diagnostic tool - .NET CLI"
[4]: https://learn.microsoft.com/en-us/dotnet/core/diagnostics/dotnet-counters "dotnet-counters diagnostic tool - .NET CLI - .NET | Microsoft Learn"
[5]: https://learn.microsoft.com/en-us/dotnet/core/diagnostics/dotnet-gcdump?utm_source=chatgpt.com "dotnet-gcdump diagnostic tool - .NET CLI"
[6]: https://learn.microsoft.com/en-us/dotnet/core/diagnostics/dotnet-stack?utm_source=chatgpt.com "dotnet-stack diagnostic tool - .NET CLI"
[7]: https://learn.microsoft.com/en-us/dotnet/core/diagnostics/dotnet-monitor "dotnet-monitor diagnostic tool - .NET | Microsoft Learn"
[8]: https://learn.microsoft.com/en-us/dotnet/core/diagnostics/dotnet-symbol?utm_source=chatgpt.com "dotnet-symbol diagnostic tool - .NET CLI"
[9]: https://learn.microsoft.com/en-us/dotnet/core/diagnostics/trace-perfcollect-lttng?utm_source=chatgpt.com "Tracing .NET applications with PerfCollect."
[10]: https://learn.microsoft.com/en-us/dotnet/core/diagnostics/dotnet-dsrouter?utm_source=chatgpt.com "dotnet-dsrouter - .NET"
[11]: https://modelcontextprotocol.io/specification/2025-06-18/server/resources "Resources - Model Context Protocol"
[12]: https://modelcontextprotocol.io/specification/2025-06-18/server/prompts "Prompts - Model Context Protocol"
[13]: https://learn.microsoft.com/en-us/dotnet/core/diagnostics/diagnostics-in-containers "Collect diagnostics in Linux containers - .NET | Microsoft Learn"
