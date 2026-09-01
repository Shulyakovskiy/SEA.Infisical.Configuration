# Infisical configuration for ASP.NET Core

- `MonixOne.Infisical.Configuration` — переиспользуемая библиотека для .NET 9/10;

Библиотека один раз загружает секреты из Infisical во время старта приложения и:

1. добавляет их в `IConfiguration`;
2. копирует их в `EnvironmentVariableTarget.Process`;
3. выполняет обязательное периодическое обновление с повторным Universal Auth login.

Библиотека читает настройки из секции `Infisical` в `IConfiguration`; если значение в секции отсутствует, используется прежний fallback к переменной окружения `INFISICAL_*`. Делегат `configure` в `AddInfisical` применяется последним и может переопределить оба источника. Копирование полученных секретов выполняется только в окружение текущего процесса приложения.

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

## appsettings.json

Помимо переменных окружения можно задать настройки в `appsettings.json`:

```json
{
  "Infisical": {
    "ClientId": "replace-with-machine-identity-client-id",
    "ClientSecret": "replace-with-machine-identity-client-secret",
    "ProjectId": "replace-with-project-id",
    "EnvironmentSlug": "dev",
    "SecretPath": "/",
    "RefreshIntervalSeconds": 86400,
    "Url": "http://infisical01.infra.home.arpa:8888",
    "Recursive": false
  }
}
```

`EnvironmentSlug` остаётся обязательным параметром Infisical API. Не добавляйте рабочий `ClientSecret` в репозиторий: для production предпочтительнее передать его через secret store хоста или переменную окружения.

Чтобы полностью отключить библиотеку для конкретного запуска, передайте `Enabled = false` при подключении:

```csharp
builder.Services.AddInfisical(builder.Configuration, options => options.Enabled = false);
```

В этом режиме библиотека не читает переменные `INFISICAL_*`, не валидирует credentials, не обращается к Infisical, не добавляет provider в `IConfiguration` и не регистрирует background refresh.

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

Если Infisical вернул ошибку, отмену или пустой список секретов, обновление считается неуспешным: библиотека сохраняет последнее успешно загруженное значение конфигурации и повторит попытку на следующем интервале.
