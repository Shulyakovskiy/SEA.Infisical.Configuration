# Infisical configuration for ASP.NET Core

В репозитории два проекта:

- `SEA.Infisical.Configuration` — переиспользуемая библиотека для .NET 9/10;

Библиотека один раз загружает секреты из Infisical во время старта приложения и:

1. добавляет их в `IConfiguration`;
2. копирует их в `EnvironmentVariableTarget.Process`;
3. выполняет обязательное периодическое обновление с повторным Universal Auth login.

Библиотека не читает и не создаёт `.env`. Credentials должны быть установлены хостом до вызова `AddInfisical`: через CI/CD, Docker `environment`/`env_file`, Kubernetes Secret или локальный загрузчик `.env`. Копирование полученных секретов выполняется только в окружение текущего процесса приложения.

```csharp
builder.Services.AddInfisical(builder.Configuration);
builder.Services.Configure<DemoOptions>(builder.Configuration.GetSection("Demo"));

// Старый consumer-код продолжает работать.
public sealed class SomeService(IOptions<DemoOptions> options)
{
    public string? ApiUrl => options.Value.ApiUrl;
}
```

`AddInfisical` нужно вызвать до `Configure<T>`, `BindConfiguration` и других регистраций, которые читают секретные настройки.

## Доступ Infisical

В Infisical создайте Machine Identity с Universal Auth, создайте для неё Client Secret и добавьте identity в проект с ролью `read`. `EnvironmentSlug` — точный slug окружения из `Project Settings → Environments` (`dev`, `staging`, `prod` или другой slug проекта).

Скопируйте шаблон:

```bash
cp .env.example .env
```

Обязательные значения:

```dotenv
INFISICAL_CLIENT_ID=<Machine Identity Client ID>
INFISICAL_CLIENT_SECRET=<Machine Identity Client Secret>
INFISICAL_PROJECT_ID=<Project ID>
INFISICAL_ENVIRONMENT=dev
INFISICAL_SECRET_PATH=/
INFISICAL_REFRESH_INTERVAL_SECONDS=86400
```

`INFISICAL_REFRESH_INTERVAL_SECONDS` — обязательный параметр контракта: положительное целое количество секунд. Значение `86400` — один день и используется библиотекой по умолчанию, если переменная не была установлена хостом.

`INFISICAL_URL` нужен для self-hosted Infisical; для Cloud его можно не указывать. До вызова `AddInfisical` эти переменные должны уже находиться в process environment.

## Обновление

Access Token является короткоживущим. `AddInfisical` не сохраняет его на диске: при каждом `RefreshAsync` выполняется login по постоянным `CLIENT_ID` и `CLIENT_SECRET`.

Background refresh регистрируется всегда. По умолчанию он выполняется раз в сутки; для другого интервала задайте переменную окружения хоста:

```dotenv
INFISICAL_REFRESH_INTERVAL_SECONDS=43200
```

Каждый refresh получает новый Access Token через Universal Auth. Поэтому даже редко используемый сервис не зависит от токена, который мог истечь в памяти.

Обновление вызывает `IConfigurationRoot.Reload`. `IOptionsMonitor<T>` увидит новые значения, а уже созданный `IOptions<T>` остаётся снимком, как и в стандартной модели ASP.NET Core.

