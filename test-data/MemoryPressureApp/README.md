# Memory Pressure Test Application

Приложение для генерации больших heap dumps (>100MB) для memory pressure тестов.

## Назначение

Это приложение создаёт большие объекты в памяти для тестирования производительности CLRScopeMCP при работе с большими heap dumps.

## Использование

### Сборка приложения

```bash
cd test-data/MemoryPressureApp
dotnet build
```

### Запуск приложения

```bash
dotnet run
```

Приложение выведет PID процесса, который можно использовать для сбора gcdump:

```
Memory Pressure Test Application
PID: 12345
This application allocates >100MB of heap memory for testing
Press Ctrl+C to exit...
```

### Генерация gcdump

После запуска приложения, используйте `dotnet-gcdump` для создания дампа:

```bash
dotnet-gcdump collect -p <PID> -o memory-pressure-large.gcdump
```

Например:

```bash
dotnet-gcdump collect -p 12345 -o memory-pressure-large.gcdump
```

### Перемещение файла в test-data

После создания дампа переместите его в директорию test-data:

```bash
mv memory-pressure-large.gcdump ../../test-data/
```

## Характеристики приложения

Приложение выделяет:
- **150MB** больших объектов (чанки по 10MB)
- **1,000,000** мелких объектов для увеличения количества объектов в heap

Всего создаётся более 150MB heap памяти для тестирования.

## Запуск тестов

После генерации gcdump файла, memory pressure тесты можно запустить:

```bash
cd ../../tests/ClrScope.Mcp.Tests
dotnet test --filter "FullyQualifiedName~MemoryPressureTests"
```

## Примечания

- gcdump файлы большого размера (>100MB) не должны коммититься в репозиторий
- Тесты автоматически пропускаются, если файл `memory-pressure-large.gcdump` не существует
- Для CI/CD рекомендуется генерировать gcdump файлы как артефакты сборки
