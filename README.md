# Курсы валют ЦБ РФ — Аналитика и генерация отчётов

Десктопное приложение на **.NET 8 (`Photino.Blazor`)** для загрузки ежедневных курсов валют Центрального банка РФ, расчёта аналитики динамики относительно предыдущего торгового дня, формирования **PDF-отчёта (`QuestPDF`)** и его отправки по ** электронной почте (`MailKit` / SMTP)** с поддержкой работы в офлайн-режиме.

---

## Содержание

1. [Описание решения и архитектура](#1-описание-решения-и-архитектура)
2. [Подводные камни и инженерные решения](#2-подводные-камни-и-инженерные-решения)
3. [Выполненные дополнительные задания](#3-выполненные-дополнительные-задания)
4. [Использованные библиотеки и инструменты](#4-использованные-библиотеки-и-инструменты)
5. [Инструкция по настройке конфигурации и запуску](#5-инструкция-по-настройке-конфигурации-и-запуску)

---

## 1. Описание решения и архитектура

Решение разделено на два проекта в составе [`RussianCentralBankExchangeRates.slnx`](./RussianCentralBankExchangeRates.slnx):

```text
russian-central-bank-exchange-rates/
├── Core/                                           # Бизнес-логика, доменная модель и инфраструктура
│   ├── DependencyInjection.cs                      # Единственная точка регистрации сервисов Core (AddCoreServices)
│   ├── Domain/
│   │   ├── Helpers/LinqExtensions.cs               # Кастомный LeftJoin для сопоставления котировок
│   │   └── Models/                                 # Rich Domain Model (Value Objects с фабриками ErrorOr<T>)
│   │       ├── Currency.cs                         # Валюта (NumCode, CharCode, Name)
│   │       ├── CurrencyRate.cs                     # Курс за 1 единицу валюты (internal)
│   │       ├── ExchangeRateReport.cs               # Срез курсов ЦБ РФ на конкретную дату (internal)
│   │       ├── CurrencyRateDynamics.cs             # Динамика валюты относительно предыдущего дня
│   │       ├── DailyExchangeRatesReport.cs         # Итоговый аналитический отчёт за день
│   │       └── EmailAddress.cs                     # Value Object email-адреса получателя
│   ├── Services/                                   # Контракты и доменные сервисы
│   │   ├── ICbrClient.cs                           # Контракт HTTP-клиента ЦБ РФ (internal)
│   │   ├── IExchangeRateAnalyticsService.cs        # Публичный контракт сервиса аналитики
│   │   ├── IPdfReportService.cs                    # Публичный контракт генератора PDF
│   │   ├── IEmailService.cs                        # Публичный контракт отправки отчёта по SMTP
│   │   └── ExchangeRateAnalyticsService.cs         # Оркестратор расчёта дневной аналитики (internal)
│   └── Infrastructure/                             # Внешние интеграции, кэширование, PDF, SMTP
│       ├── CbrClient.cs                            # HTTP-клиент к XML_daily.asp ЦБ РФ (windows-1251)
│       ├── CachedCbrClient.cs                      # Декоратор L1-кэширования ответов ЦБ РФ в IMemoryCache
│       ├── CachedExchangeRateAnalyticsService.cs   # Декоратор L1/L2-кэширования отчётов с Fail-Safe режимом
│       ├── LastReportFileCache.cs                  # Потокобезопасный файловый кэш (last-report.json)
│       ├── PdfReportService.cs                     # Генерация табличного PDF-отчёта (QuestPDF)
│       ├── SmtpEmailService.cs                     # Отправка письма с вложением PDF (MailKit)
│       ├── schema.xsd                              # XSD-схема ответа ЦБ РФ для кодогенерации DTO
│       ├── Dto/                                    # XML DTO (ValCurs) и JSON Cache DTO + Source Generator Context
│       ├── Helpers/FormattingExtensions.cs         # Единое форматирование чисел, дат и имён файлов
│       └── Mappers/                                # Мапперы XML/JSON DTO <-> Domain Models
└── UI/                                             # Десктопный клиент (Photino.Blazor)
    ├── Program.cs                                  # Точка входа, настройка Serilog и загрузка config.json
    ├── App.razor                                   # Одностраничный интерфейс аналитики, фильтрации и отправки
    ├── config.example.json                         # Шаблон конфигурационного файла (в репозитории)
    └── wwwroot/                                    # Статические ресурсы и стили (app.css)
```

### Ключевые архитектурные принципы

1. **Строгая инкапсуляция публичного API `Core`:**
   - Слой `UI` имеет доступ **только** к необходимым публичным контрактам (`IExchangeRateAnalyticsService`, `IPdfReportService`, `IEmailService`), методу расширения `AddCoreServices`, хелперам форматирования и выходным доменным моделям (`DailyExchangeRatesReport`, `CurrencyRateDynamics`, `Currency`, `EmailAddress`).
   - Все детали реализации (`CbrClient`, `CachedCbrClient`, `ExchangeRateAnalyticsService`, `CachedExchangeRateAnalyticsService`, `LastReportFileCache`, `PdfReportService`, `SmtpEmailService`, `ICbrClient`, промежуточные модели `ExchangeRateReport` и `CurrencyRate`, классы `Options`, DTO и мапперы) имеют модификатор **`internal`**.

2. **Always-Valid Domain Model (Value Objects) и отказ от исключений в бизнес-логике (`ErrorOr<T>`):**
   - Все модели в `Core/Domain/Models` реализованы как `sealed record` с **приватными конструкторами** и статическими фабричными методами `Create(...)`, возвращающими `ErrorOr<T>`.
   - Создать невалидный доменный объект извне невозможно: проверка кодов валют, номиналов, дат, расчёт абсолютного/процентного изменения, выборка Топ-3 роста/падения и среднего процента изменения инкапсулированы внутри самих моделей.
   - Ожидаемые ошибки (недоступность сети, таймаут ЦБ РФ, неверные учётные данные SMTP, некорректный `config.json`, ошибка парсинга XML) возвращаются через `ErrorOr<T>` с понятным пользователю описанием на русском языке.

3. **Паттерн Decorator для прозрачного кэширования:**
   - `ExchangeRateAnalyticsService` и `CbrClient` не знают о существовании кэша и решают только свою бизнес-задачу.
   - Кэширование навешивается поверх них через декораторы `CachedCbrClient` и `CachedExchangeRateAnalyticsService`, зарегистрированные в DI-контейнере.

---

## 2. Подводные камни и инженерные решения

В ходе разработки и тестирования на реальных исторических данных ЦБ РФ был выявлен и решён ряд нетривиальных проблем:

### 1. Различие между календарной датой и фактической датой курса (`ValCurs.Date`)
- **Проблема:** ЦБ РФ не устанавливает курсы по выходным и праздничным дням. При запросе курса на воскресенье (например, `date_req=11/10/2026`) или новогодние праздники (`date_req=08/01/2025`) сервис возвращает последний действовавший курс (за субботу `10.10.2026` или за `29.12.2024`). Если искать «предыдущий день» простым вычитанием дня из запрошенной даты (`requestedDate.AddDays(-1)`), то для воскресенья и субботы вернётся **один и тот же** субботний курс, а вся динамика за день превратится в `0,0000%`.
- **Решение:** В `ExchangeRateAnalyticsService` дата предыдущего среза всегда вычисляется от **фактической даты полученного курса** из атрибута `Date` корневого элемента `<ValCurs>`:
  ```csharp
  var prevTargetDate = currentReport.Date.AddDays(-1);
  ```
  Благодаря этому даже посреди длинных праздников приложение безошибочно находит именно предыдущий торговый день котировок. В UI при несовпадении `RequestedDate` и `CurrentDate` выводится поясняющий информационный баннер.

### 2. Умное кэширование нерабочих интервалов (`CacheReportInterval`)
- **Проблема:** Если пользователь запросил курс на понедельник или воскресенье, а ЦБ вернул курс от субботы (`report.Date < requestedDate`), то при последующем выборе в календаре субботы или воскресенья обычный кэш по ключу `requestedDate` дал бы промах и снова пошёл в сеть за тем же самым XML.
- **Решение:** Метод `CachedCbrClient.CacheReportInterval` при получении ответа с `report.Date < requestedDate` сразу записывает срез в `IMemoryCache` для всех календарных дат внутри интервала `[report.Date .. requestedDate]`.

### 3. Изменение состава валют ЦБ РФ (кейс `19.01.2023`) и фолбэк расчёта `UnitRate`
- **Проблема:** 19 января 2023 года ЦБ РФ одномоментно добавил в выгрузку 9 новых валют (дирхам ОАЭ, бат, донг и др.), которых не существовало в выгрузке за 18 января 2023 года. Кроме того, в архивных выгрузках (с `02.07.1992`) может отсутствовать тег `<VunitRate>`.
- **Решение:**
  - Слияние текущего и предыдущего срезов в `DailyExchangeRatesReport.Create` выполняется через кастомный `LeftJoin` по `Currency`. Если валюта появилась впервые (`previous is null`), её `PreviousUnitRate`, `AbsoluteChange` и `PercentChange` получают значение `null` (в UI и PDF отображается прочерк `-`), не искажая расчёт Топ-3 и среднего изменения по остальным валютам.
  - В `CurrencyRate.Create` и `CbrMapperExtensions` реализован фолбэк: если `<VunitRate>` отсутствует или не парсится, курс за единицу рассчитывается как `Value / Nominal` с учётом русской локали (`ru-RU`, десятичный разделитель — запятая).

### 4. Кодировка `windows-1251` и утечки памяти `XmlSerializer`
- **Проблема:** XML-сервис ЦБ РФ отдаёт ответы в кодировке `windows-1251`, которая по умолчанию отсутствует в .NET 8 (`NotSupportedException: No data is available for encoding 1251`). Кроме того, создание `new XmlSerializer(typeof(ValCurs))` на каждый запрос может приводить к росту памяти, если не используется кэширующий конструктор.
- **Решение:** В статическом конструкторе `CbrClient` регистрируется `Encoding.RegisterProvider(CodePagesEncodingProvider.Instance)`, а экземпляр `XmlSerializer(typeof(ValCurs))` регистрируется в DI как `Singleton`. Сами классы `ValCurs` и `ValCursValute` сгенерированы по `Core/Infrastructure/schema.xsd` с помощью `XmlSchemaClassGenerator` (команда в `Makefile`).

### 5. Особенность C# Source Generator для `IConfiguration` (`EnableConfigurationBindingGenerator`) и `TimeSpan` в `[Range]`
- **Проблема:**
  1. В `Core.csproj` включён генератор исходного кода привязки конфигурации (`<EnableConfigurationBindingGenerator>true</EnableConfigurationBindingGenerator>`). Если свойства в классах настроек (`CbrClientOptions`, `SmtpOptions`, `CacheOptions`) объявлены с аксессором `{ get; init; }`, генератор `BindingExtensions.g.cs` не может присвоить их в методе `BindCore(IConfiguration, ref T instance, ...)` на уже созданном экземпляре и генерирует **пустой метод**, оставляя настройки пустыми.
  2. В атрибуте `[Range(typeof(TimeSpan), "00:01:00", "24:00:00")]` строка `"24:00:00"` вызывает `FormatException` при вызове `TimeSpan.Parse`, так как компонент часов ограничен диапазоном `0..23`.
- **Решение:** Свойства в классах `Options` переведены на `{ get; set; }`, а суточные границы в `[Range]` заданы в корректном формате `d.hh:mm:ss` (`"1.00:00:00"` и `"7.00:00:00"`).

### 6. Атомарная запись файлового кэша и защита от повреждённого JSON
- **Проблема:** Прямая перезапись файла `cache/last-report.json` при аварийном завершении процесса оставляет битый файл на диске.
- **Решение:** `LastReportFileCache` сериализует данные через `System.Text.Json` Source Generator (`LastDailyExchangeRatesReportCacheJsonContext`) сначала во временный файл `last-report.json.tmp`, после чего выполняет атомарную замену `File.Move(tempPath, cacheFilePath, overwrite: true)` внутри `SemaphoreSlim`. При чтении с диска DTO проходит полную проверку инвариантов через `dto.ToDomain()` — если файл был испорчен вручную, он логируется как Warning и игнорируется без падения программы.

---

## 3. Выполненные дополнительные задания

1. **Кэширование последних загруженных данных с возможностью сформировать отчёт без подключения к сети:**
   - Реализована двухуровневая система кэширования (**L1 `IMemoryCache`** + **L2 файловый кэш `cache/last-report.json`**).
   - При полном отсутствии интернета (даже после перезапуска приложения) срабатывает механизм **Fail-Safe**: `CachedExchangeRateAnalyticsService` возвращает последний сохранённый отчёт из `cache/last-report.json`, а также прогревает `IMemoryCache`.
   - В интерфейсе добавлена кнопка **«Сохранить PDF»**, позволяющая сформировать и сохранить PDF-отчёт на диск полностью офлайн по загруженным или закэшированным данным.
2. **Логирование операций в файл:**
   - Настроено структурированное логирование через **Serilog (`Serilog.Sinks.File`)** с выводом в директорию `logs/app-YYYYMMDD.log` (ежедневная ротация).
   - Логируются все ключевые этапы: запросы к API ЦБ РФ, попадания в L1/L2 кэш, срабатывание Fail-Safe режима, генерация PDF (с размером документа в байтах), отправка писем по SMTP, а также ошибки валидации и конфигурации.

---

## 4. Использованные библиотеки и инструменты

### Проект `Core`
| Пакет / Инструмент | Версия | Назначение |
|---|---|---|
| [`ErrorOr`](https://github.com/amantinband/error-or) | `2.1.1` | Реализация паттерна Result / Discriminated Union для явной обработки ошибок без исключений |
| [`QuestPDF`](https://www.questpdf.com/) | `2026.9.1` | Вёрстка и генерация PDF-отчётов (лицензия `Community`, поддержка системных шрифтов) |
| [`MailKit`](https://github.com/jstedfast/MailKit) | `4.18.1` | Асинхронный SMTP-клиент (`SmtpClient`) и сборка MIME-писем с вложениями (`MimeKit`) |
| `Microsoft.Extensions.Caching.Memory` | `8.0.1` | In-Memory кэширование (L1) дневных срезов ЦБ РФ и готовых аналитических отчётов |
| `Microsoft.Extensions.Http` | `8.0.0` | Управление пулом соединений `HttpClient` (`AddHttpClient<CbrClient>`) |
| `Microsoft.Extensions.Options.DataAnnotations` | `8.0.0` | Декларативная валидация секций `config.json` через атрибуты `DataAnnotations` |
| `Microsoft.Extensions.Configuration.Binder` | `8.0.0` | Привязка конфигурации с использованием C# Source Generator |
| `Microsoft.Extensions.DependencyInjection` | `8.0.0` | Регистрация сервисов и декораторов в DI-контейнере |
| `XmlSchemaClassGenerator` (`xscgen`) | `3.0.1405` | Генерация C#-классов DTO (`ValCurs`, `ValCursValute`) из XSD-схемы ЦБ РФ (`schema.xsd`) |

### Проект `UI`
| Пакет | Версия | Назначение |
|---|---|---|
| [`Photino.Blazor`](https://github.com/tryphotino/photino.Blazor) | `4.*` | Нативное легковесное десктопное окно поверх системного WebView с поддержкой Blazor |
| [`Serilog.Extensions.Logging`](https://github.com/serilog/serilog-extensions-logging) | `8.0.0` | Интеграция `Serilog` со стандартным `ILogger<T>` во всех сервисах `Core` и `UI` |
| [`Serilog.Sinks.File`](https://github.com/serilog/serilog-sinks-file) | `6.0.0` | Запись логов приложения в файл с ежедневной ротацией (`logs/app-.log`) |

---

## 5. Инструкция по настройке конфигурации и запуску

### Предварительные требования
- Установленный **[.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)** (или новее с поддержкой таргета `net8.0`).
- ОС Windows 10 / 11 (с установленным системным компонентом Microsoft Edge WebView2 Runtime — предустановлен в актуальных версиях Windows).

### Шаг 1. Настройка конфигурационного файла `UI/config.json`

Файл с реальными учётными данными `UI/config.json` исключён из Git (`.gitignore`), чтобы секреты не попадали в репозиторий. В папке `UI/` находится файл-шаблон [`UI/config.example.json`](./UI/config.example.json).

1. Скопируйте шаблон `UI/config.example.json` в `UI/config.json`:
   ```powershell
   Copy-Item UI/config.example.json UI/config.json
   ```
2. Откройте `UI/config.json` и укажите ваши настройки SMTP:
   ```json
   {
     "CbrClient": {
       "BaseUrl": "https://www.cbr.ru/",
       "Timeout": "00:00:10"
     },
     "Cache": {
       "SlidingExpiration": "06:00:00",
       "AbsoluteExpiration": "1.00:00:00",
       "FilePath": "cache/last-report.json"
     },
     "Smtp": {
       "Host": "smtp.yandex.ru",
       "Port": 465,
       "Username": "your-login@yandex.ru",
       "Password": "your-app-password",
       "SenderEmail": "your-login@yandex.ru",
       "Timeout": "00:00:15"
     }
   }
   ```

#### Описание параметров конфигурации:
- **`CbrClient`**:
  - `BaseUrl` — базовый URL сервиса ЦБ РФ (`https://www.cbr.ru/`).
  - `Timeout` — таймаут HTTP-запроса (от `00:00:01` до `00:02:00`).
- **`Cache`**:
  - `SlidingExpiration` — скользящее время жизни записей в `IMemoryCache` (от `00:01:00` до `1.00:00:00`).
  - `AbsoluteExpiration` — абсолютное время жизни записей в `IMemoryCache` (от `00:01:00` до `7.00:00:00`).
  - `FilePath` — относительный (от директории запуска) или абсолютный путь к файлу офлайн-кэша последнего отчёта.
- **`Smtp`**:
  - `Host` — адрес SMTP-сервера (например, `smtp.yandex.ru`, `smtp.mail.ru`, `smtp.gmail.com`).
  - `Port` — порт SMTP-сервера (`465` для SSL/TLS или `587` для STARTTLS; режим шифрования определяется автоматически через `SecureSocketOptions.Auto`).
  - `Username` — логин почтового ящика (обычно совпадает с полным email-адресом).
  - `Password` — **пароль приложения (App Password)** почтового сервиса (для Яндекс Почты создаётся в разделе *Управление аккаунтом $\rightarrow$ Безопасность $\rightarrow$ Пароли приложений*).
  - `SenderEmail` — адрес отправителя в заголовке `From` (должен совпадать с учётной записью `Username`).
  - `Timeout` — таймаут операций SMTP (от `00:00:01` до `00:02:00`).

### Шаг 2. Сборка и запуск проекта

Из корневой директории репозитория выполните:

```powershell
# Сборка всего решения
dotnet build

# Запуск десктопного приложения
dotnet run --project UI
```

Также проект можно открыть и запустить в **JetBrains Rider** или **Visual Studio 2022+** (файл решения `RussianCentralBankExchangeRates.slnx`, стартовый проект — `UI`).

### Использование приложения
1. При запуске приложение автоматически загружает актуальные курсы ЦБ РФ на текущую дату.
2. Чтобы посмотреть курсы на другую дату (начиная с `02.07.1992`), выберите дату в поле **«Дата курса»** и нажмите **«Загрузить данные»**.
3. В таблице доступно мгновенное **поисковое фильтрование** по коду/названию валюты и **сортировка** по любому столбцу (клик по заголовку колонки).
4. Кнопка **«Сохранить PDF»** открывает системный диалог сохранения и формирует PDF-отчёт локально на диск (работает в том числе без интернета).
5. Для отправки отчёта на почту введите адрес в поле **«Email получателя»** и нажмите **«Отправить отчёт»** — получателю придёт письмо с краткой сводкой Топ-3 в тексте письма и вложенным файлом `cbr-rates-YYYY-MM-DD.pdf`.
6. Логи работы приложения записываются в папку `UI/bin/Debug/net8.0/logs/`, а файловый кэш последнего отчёта — в `UI/bin/Debug/net8.0/cache/last-report.json`.