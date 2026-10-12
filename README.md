# Курсы валют ЦБ РФ — Аналитика, PDF-отчёты и рассылка по SMTP

Десктопное приложение на платформе **.NET 8 (`Photino.Blazor`)** для загрузки официальных курсов валют Центрального банка РФ, расчёта аналитики динамики относительно предыдущего доступного дня котировок, генерации **PDF-отчёта (`QuestPDF`)** и его отправки по **электронной почте (`MailKit` / SMTP)** с поддержкой двухуровневого кэширования, работы в офлайн-режиме (**Fail-Safe**) и структурированного файлового логирования (**`Serilog`**).

---

## Содержание

1. [Матрица выполнения требований ТЗ](#1-матрица-выполнения-требований-тз)
2. [Быстрый старт и настройка конфигурации](#2-быстрый-старт-и-настройка-конфигурации)
3. [Полный сценарий работы: от выбора даты до получения письма с PDF](#3-полный-сценарий-работы-от-выбора-даты-до-получения-письма-с-pdf)
4. [Архитектура решения и публичный API Core](#4-архитектура-решения-и-публичный-api-core)
5. [Качество кода Core, обработка ошибок и подводные камни](#5-качество-кода-core-обработка-ошибок-и-подводные-камни)
6. [Выполненные дополнительные задания (технический разбор)](#6-выполненные-дополнительные-задания-технический-разбор)
7. [Использованные библиотеки и обоснование выбора](#7-использованные-библиотеки-и-обоснование-выбора)

---

## 1. Матрица выполнения требований ТЗ

| Раздел ТЗ | Требование | Статус и реализация в проекте |
|---|---|---|
| **2. Архитектура** | Чёткое разделение на `Core` и `UI`; `UI` не содержит бизнес-логики и вызывает только публичный API `Core` | Выполнено. Все внутренние сервисы, HTTP-клиент, декораторы кэша, промежуточные модели, DTO и `Options` скрыты модификатором `internal`. В `UI` вынесено только отображение и вызовы `IExchangeRateAnalyticsService`, `IPdfReportService`, `IEmailService`. |
| **3.1. Загрузка данных** | Загрузка с `XML_daily.asp?date_req=dd/MM/yyyy` (`windows-1251`) на выбранную дату и предыдущий доступный день по атрибуту `ValCurs.Date` | Выполнено в `CbrClient` и `ExchangeRateAnalyticsService`. Предыдущий срез запрашивается строго от фактической даты котировки: `currentReport.Date.AddDays(-1)`. |
| **3.2. Обработка данных** | Приведение курса к 1 единице валюты с учётом номинала и запятой, расчёт изменения в %, Топ-3 роста и Топ-3 падения | Выполнено в доменных моделях `CurrencyRate`, `CurrencyRateDynamics` и `DailyExchangeRatesReport` (через кастомный `LeftJoin` по валютам). |
| **3.3. Интерфейс** | Выбор даты (≤ сегодня), таблица результатов, поле email, кнопки «Загрузить данные» и «Отправить отчёт», индикация этапов | Выполнено в `UI/App.razor`. Дополнительно реализованы поиск по коду/названию, сортировка по всем 6 колонкам, кнопка локального сохранения PDF и пошаговый индикатор прогресса (`1. Загрузка` → `2. Формирование PDF` → `3. Отправка`). |
| **3.4. PDF-отчёт** | Заголовок, дата отчёта, блок «Итоги дня» (Топ-3 роста, Топ-3 падения, среднее изменение), таблица всех валют | Выполнено в `PdfReportService` на базе `QuestPDF` с цветовой индикацией динамики и постраничной нумерацией. |
| **3.5. Отправка письма** | Тема `«Курсы валют ЦБ РФ на {дата}»`, текстовая сводка Топ-3 в теле письма, PDF во вложении | Выполнено в `SmtpEmailService` на базе `MailKit` / `MimeKit`. |
| **4. Обработка ошибок** | Перехват ошибок сети ЦБ РФ, пустого/битого XML, невалидного email, ошибок SMTP, отсутствия/порчи `config.json` без падения приложения | Выполнено через паттерн Result (`ErrorOr<T>`), гранулярный перехват сетевых/протокольных исключений и безопасную инициализацию конфигурации при старте. |
| **5. Конфигурация** | Параметры в `config.json` (вне Git), наличие шаблона `config.example.json` в репозитории | Выполнено (`UI/config.example.json` в репозитории, `config.json` в `.gitignore`). Добавлена декларативная валидация `DataAnnotations` + Source Generator. |
| **7.1. Доп. задание №1** | Кэширование последних загруженных данных с возможностью сформировать отчёт без подключения к сети | **Выполнено.** Двухуровневый кэш (**L1 `IMemoryCache`** + **L2 `cache/last-report.json`**) с атомарной записью, прогревом памяти, режимом **Fail-Safe** и офлайн-сохранением PDF. |
| **7.2. Доп. задание №2** | Логирование операций в файл | **Выполнено.** Структурированное логирование через **`Serilog`** (`logs/app-YYYYMMDD.log`) с ежедневной ротацией и фильтрацией системного шума. |
| **7.3. Доп. задание №3** | График динамики курса (USD/EUR) за последние 7 дней в PDF | **Выполнено.** Пошаговая докачка исторических срезов ЦБ РФ за 7-дневное окно, протяжка курсов через выходные (`Carry-Forward`) в `CurrencyRateTrend` и векторная отрисовка SVG-графика через **`ScottPlot`** внутри **`QuestPDF`**. |

---

## 2. Быстрый старт и настройка конфигурации

### Предварительные требования
- Установленный **[.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)** (или новее с поддержкой таргета `net8.0`).
- ОС **Windows 10 / 11** (с системным компонентом Microsoft Edge WebView2 Runtime, предустановленным в современных версиях Windows).

### Шаг 1. Создание рабочего `UI/config.json` из шаблона

Реальный конфигурационный файл `UI/config.json` добавлен в [`.gitignore`](./.gitignore), чтобы пароли SMTP не попадали в систему контроля версий. В репозитории находится готовый шаблон [`UI/config.example.json`](./UI/config.example.json).

Скопируйте шаблон в рабочий файл конфигурации:

```powershell
Copy-Item UI/config.example.json UI/config.json
```

Откройте `UI/config.json` и укажите параметры вашей учётной записи SMTP:

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

#### Справочник параметров конфигурации и правил валидации (`DataAnnotations`)

| Секция | Параметр | Тип / Формат | Значение по умолчанию | Ограничения валидации и описание |
|---|---|---|---|---|
| `CbrClient` | `BaseUrl` | `string` (URL) | `"https://www.cbr.ru/"` | `[Required, Url]`. Базовый адрес XML-сервиса Банка России. |
| `CbrClient` | `Timeout` | `TimeSpan` | `"00:00:10"` | `[Range("00:00:01", "00:02:00")]`. Таймаут одного HTTP-запроса к ЦБ РФ. |
| `Cache` | `SlidingExpiration` | `TimeSpan` | `"06:00:00"` | `[Range("00:01:00", "1.00:00:00")]`. Скользящее время жизни записей в `IMemoryCache` (от 1 мин до 1 суток). |
| `Cache` | `AbsoluteExpiration` | `TimeSpan` | `"1.00:00:00"` | `[Range("00:01:00", "7.00:00:00")]`. Абсолютное время жизни записей в `IMemoryCache` (от 1 мин до 7 суток). |
| `Cache` | `FilePath` | `string` | `"cache/last-report.json"` | `[Required]`. Относительный (от `AppContext.BaseDirectory`) или абсолютный путь к файлу L2-кэша. |
| `Smtp` | `Host` | `string` | `"smtp.yandex.ru"` | `[Required]`. Адрес SMTP-сервера (`smtp.yandex.ru`, `smtp.mail.ru`, `smtp.gmail.com` и др.). |
| `Smtp` | `Port` | `int` | `465` (или `587`) | `[Range(1, 65535)]`. Порт SMTP. Шифрование (`SSL/TLS` или `STARTTLS`) выбирается автоматически через `SecureSocketOptions.Auto`. |
| `Smtp` | `Username` | `string` | — | `[Required]`. Логин почтового ящика (полный email-адрес). |
| `Smtp` | `Password` | `string` | — | `[Required]`. **Пароль приложения (App Password)** почтового провайдера. |
| `Smtp` | `SenderEmail` | `string` (Email) | — | `[Required, EmailAddress]`. Адрес отправителя в заголовке `From` (должен совпадать с аккаунтом `Username`). |
| `Smtp` | `Timeout` | `TimeSpan` | `"00:00:15"` | `[Range("00:00:01", "00:02:00")]`. Таймаут подключения и отправки письма по SMTP. |

> [!TIP]
> **Как настроить SMTP для Яндекс Почты / Mail.ru / Gmail:**
> Почтовые сервисы требуют использовать **пароль приложения**, а не основной пароль от аккаунта:
> - **Яндекс (`smtp.yandex.ru`, порт `465` или `587`):** *Управление аккаунтом (Яндекс ID) → Безопасность → Пароли приложений → Создать пароль приложения (Почта)*.
> - **Mail.ru (`smtp.mail.ru`, порт `465`):** *Настройки → Безопасность → Пароли для внешних приложений*.
> - **Gmail (`smtp.gmail.com`, порт `587` или `465`):** *Аккаунт Google → Безопасность → Двухэтапная аутентификация → Пароли приложений*.

### Шаг 2. Сборка и запуск приложения

Выполните из корневой папки репозитория:

```powershell
# Сборка решения
dotnet build

# Запуск десктопного приложения
dotnet run --project UI
```

Также решение [`RussianCentralBankExchangeRates.slnx`](./RussianCentralBankExchangeRates.slnx) можно открыть и запустить напрямую из **JetBrains Rider** или **Visual Studio 2022+** (стартовый проект — `UI`).

---

## 3. Полный сценарий работы: от выбора даты до получения письма с PDF

Ниже показана полная сквозная цепочка выполнения основного пользовательского сценария через слои приложения:

```mermaid
sequenceDiagram
    actor User as Пользователь
    participant UI as UI (App.razor)
    participant CacheSvc as CachedExchangeRateAnalyticsService
    participant FileCache as LastReportFileCache (L2 JSON)
    participant Analytics as ExchangeRateAnalyticsService
    participant CbrCache as CachedCbrClient (L1 Memory)
    participant CbrHttp as CbrClient (HTTP XML_daily.asp)
    participant Domain as DailyExchangeRatesReport (Domain)
    participant Pdf as PdfReportService (QuestPDF + ScottPlot)
    participant Smtp as SmtpEmailService (MailKit)

    User->>UI: 1. Выбирает дату и нажимает «Загрузить данные»
    UI->>CacheSvc: GetDailyAnalyticsAsync(selectedDate)
    alt Есть в L1 (IMemoryCache) или L2 (last-report.json на ту же дату)
        CacheSvc-->>UI: ErrorOr<DailyExchangeRatesReport> (мгновенно из кэша)
    else Запрос новых данных
        CacheSvc->>Analytics: GetDailyAnalyticsAsync(selectedDate)
        Analytics->>CbrCache: GetExchangeRateReportAsync(selectedDate)
        CbrCache->>CbrHttp: GET /scripts/XML_daily.asp?date_req=dd/MM/yyyy
        CbrHttp-->>CbrCache: ValCurs (windows-1251) -> ExchangeRateReport (currentReport)
        Note over CbrCache: Кэширует весь интервал [currentReport.Date .. selectedDate]
        loop Шаг назад по report.Date.AddDays(-1) до покрытия 7 дней (минимум 2 среза)
            Analytics->>CbrCache: GetExchangeRateReportAsync(nextTargetDate)
            CbrCache->>CbrHttp: GET /scripts/XML_daily.asp?date_req=dd/MM/yyyy (если нет в L1)
            CbrHttp-->>CbrCache: ValCurs -> ExchangeRateReport
        end
        Analytics->>Domain: DailyExchangeRatesReport.Create(selectedDate, historicalReports)
        Domain-->>Analytics: Расчёт UnitRate, LeftJoin динамики, Топ-3, среднего % и 7-дневных трендов USD/EUR
        Analytics-->>CacheSvc: DailyExchangeRatesReport
        CacheSvc->>FileCache: SaveIfChangedAsync (атомарная запись в last-report.json)
        CacheSvc-->>UI: ErrorOr<DailyExchangeRatesReport>
    end
    UI-->>User: Отображает сводку дня, Топ-3 и интерактивную таблицу курсов

    User->>UI: 2. Вводит Email и нажимает «Отправить отчёт»
    UI->>Domain: EmailAddress.Create(recipientEmail)
    UI->>Pdf: GenerateDailyReportAsync(report)
    Pdf-->>UI: ErrorOr<byte[]> (PDF-документ A4 с SVG-графиком за 7 дней)
    UI->>Smtp: SendDailyReportAsync(report, emailAddress, pdfBytes)
    Smtp-->>UI: Доставка письма с темой, сводкой Топ-3 и вложением cbr-rates-yyyy-MM-dd.pdf
    UI-->>User: Уведомление об успешной отправке
```

### Пошаговое описание этапов сценария

1. **Автозагрузка при старте и выбор даты:**
   - При открытии окна приложение автоматически запрашивает курсы на сегодняшнюю дату (`DateTime.Today`).
   - Поле выбора даты ограничено диапазоном от `02.07.1992` (первая историческая котировка в базе ЦБ РФ) до текущей даты (нельзя выбрать дату в будущем).
   - Кнопки **«Сохранить PDF»** и **«Отправить отчёт»** блокируются при смене даты в календаре до тех пор, пока пользователь не нажмёт **«Загрузить данные»**, что исключает отправку старого отчёта при изменённом поле даты.

2. **Загрузка котировок, определение предыдущего торгового дня и 7-дневного окна:**
   - `CbrClient` запрашивает `https://www.cbr.ru/scripts/XML_daily.asp?date_req=dd/MM/yyyy`, декодирует поток из `windows-1251`, десериализует XML в `ValCurs` и преобразует в доменный срез `ExchangeRateReport` через антикоррупционный маппер `CbrMapperExtensions.ToDomain` (с применением паттерна **Tolerant Reader** для изоляции повреждённых архивных записей).
   - `ExchangeRateAnalyticsService` считывает фактическую дату котировки из `currentReport.Date` (атрибут `Date` корневого элемента `<ValCurs>`) и в едином цикле `FetchHistoricalReportsAsync` шагает назад по `reports[^1].Date.AddDays(-1)`, пока не соберёт минимум 2 торговых среза (для расчёта дневной динамики) и не покроет 7-дневное календарное окно `[RequestedDate - 6 дней .. RequestedDate]` (`WindowStartDate`) для графика `USD`/`EUR`.

3. **Доменный расчёт аналитики (`DailyExchangeRatesReport.Create`):**
   - Для каждой валюты курс приводится к **1 единице валюты** (`UnitRate`): используется высокоточное поле `<VunitRate>` из XML ЦБ РФ, а при его отсутствии в архивных выгрузках выполняется фолбэк `Value / Nominal`.
   - Текущий (`HistoricalReports[0]`) и предыдущий (`HistoricalReports[1]`) срезы сопоставляются через кастомный `LeftJoin` по сущности `Currency`.
   - В `CurrencyRateDynamics.Create` вычисляются абсолютное изменение (`CurrentUnitRate - PreviousUnitRate`) и процентное изменение с округлением до 4 знаков после запятой:

     $$\text{PercentChange} = \text{round}\left(\frac{\text{CurrentUnitRate} - \text{PreviousUnitRate}}{\text{PreviousUnitRate}} \times 100, 4\right)$$

   - Формируются списки **Топ-3 выросших валют** (`PercentChange > 0`, по убыванию), **Топ-3 упавших валют** (`PercentChange < 0`, по возрастанию), **среднее изменение по всем валютам** (`AveragePercentChange`) и **7-дневные тренды `WeeklyTrends`** (`CurrencyRateTrend`) для `Currency.Usd` и `Currency.Eur`.

4. **Отображение в UI и интерактивная работа с таблицей:**
   - Во время выполнения любой операции отображается панель прогресса со спиннером и активным шагом (`1. Загрузка` → `2. Формирование PDF` → `3. Отправка`).
   - На экране выводятся 3 аналитические карточки («Среднее изменение», «Топ-3 роста», «Топ-3 падения») и полная таблица курсов.
   - Таблица поддерживает **мгновенный поиск** по символьному коду (`USD`, `EUR`, `CNY`) или названию валюты и **сортировку в оба направления** по любому из 6 столбцов.

5. **Формирование PDF-отчёта (`PdfReportService`):**
   - Генерируется документ формата A4 (шрифт `Arial`, отступы `1.5 см`) со следующей структурой:
     - **Шапка:** заголовок «Отчёт по курсам валют ЦБ РФ» и строка дат (с явным указанием запрошенной даты, даты действующего курса ЦБ РФ и даты предыдущего среза для сравнения).
     - **Блок «Итоги дня»:** среднее изменение по всем валютам и две колонки с Топ-3 роста (зелёный акцент) и Топ-3 падения (красный акцент) с указанием изменения в `%` и в рублях.
     - **Векторный график динамики за 7 дней:** блок «Динамика курса за последние 7 дней (руб. за 1 ед.)», построенный с помощью **`ScottPlot`** в векторном формате SVG и встроенный через `QuestPDF` (`row.RelativeItem().Svg(...)`), с линиями трендов `USD` и `EUR`, маркерами точек, подписями значений курса и автоматическими аналитическими примечаниями (`FormattingExtensions.FormatWeeklyChartNote`) при неполном покрытии архивного или офлайн-окна.
     - **Таблица всех валют:** 6 колонок (`Код`, `Валюта`, `Курс (руб.)`, `Пред. (руб.)`, `Изм. (руб.)`, `Изм. (%)`) с цветовой индикацией знака и сквозной нумерацией страниц в колонтитуле (`Страница X из Y`).
   - Отчёт можно как сразу сохранить локально кнопкой **«Сохранить PDF»**, так и отправить по почте.

6. **Отправка письма по SMTP (`SmtpEmailService`):**
   - Адрес получателя и отправителя валидируются через доменный Value Object `EmailAddress.Create`.
   - Собирается `MimeMessage` с темой `Курсы валют ЦБ РФ на {CurrentDate:dd.MM.yyyy}`, текстовым телом со сводкой Топ-3 роста и Топ-3 падения и прикреплённым PDF-файлом `cbr-rates-{CurrentDate:yyyy-MM-dd}.pdf`.

### Рекомендуемые даты для быстрой проверки особых случаев
- **Любое воскресенье или праздничный день (например, `05.01.2025`):** демонстрация корректного определения действующего курса ЦБ РФ (от `29.12.2024`), предыдущего торгового дня (от `28.12.2024`) и протяжки 7-дневного графика вплоть до запрошенной даты `05.01.2025` с выводом информационного баннера о нерабочем дне.
- **`06.06.1996` (реальная историческая аномалия в XML ЦБ РФ):** в официальной базе ЦБ РФ за эту дату запись `<Valute ID="R01720A">` («Украинских карбованцев») не содержит тегов `<NumCode>` и `<CharCode>`. Демонстрирует работу паттерна **Tolerant Reader**: битая запись изолированно пропускается с записью `WRN` в лог-файл, а отчёт успешно формируется по остальным 23 валидным валютам.
- **`04.01.1999` или даты до `01.01.1999`:** демонстрация построения 7-дневного графика на границе введения единой европейской валюты `EUR` (котируется Банком России с `01.01.1999`) с автоматическим пояснением в PDF-отчёте.
- **`19.01.2023`:** день, когда ЦБ РФ добавил в перечень сразу 9 новых валют (`AED`, `THB`, `VND`, `RSD`, `NZD`, `EGP`, `IDR`, `QAR`, `GEL`). Для новых валют в колонках предыдущего курса и изменения корректно отображается прочерк `-` без сбоев в расчёте Топ-3.
- **Отключение интернета после загрузки:** демонстрация работы селективного **Fail-Safe** режима, сборки отчёта с частичным 7-дневным графиком по доступным в кэше срезам и сохранения PDF-отчёта полностью офлайн.

---

## 4. Архитектура решения и публичный API `Core`

Решение спроектировано по принципам **Clean Architecture** и **Rich Domain Model** с жёстким разделением между библиотекой бизнес-логики (`Core`) и десктопным представлением (`UI`).

### Дерево структуры проектов

```text
russian-central-bank-exchange-rates/
├── .gitignore                                      # Исключает UI/config.json, bin/, obj/, logs/, cache/
├── Makefile                                        # Команда кодогенерации XML DTO из XSD-схемы (xscgen)
├── README.md                                       # Документация проекта
├── RussianCentralBankExchangeRates.slnx            # Файл решения (.NET 8)
├── Core/                                           # Библиотека бизнес-логики и инфраструктуры
│   ├── Core.csproj
│   ├── DependencyInjection.cs                      # Единственная точка входа для регистрации сервисов (AddCoreServices)
│   ├── Domain/
│   │   ├── Errors/
│   │   │   └── CbrErrors.cs                        # internal: каталог транзиентных сетевых ошибок ЦБ РФ (Error.Custom)
│   │   ├── Helpers/
│   │   │   ├── FormattingExtensions.cs             # public/internal: методы форматирования доменных моделей
│   │   │   └── LinqExtensions.cs                   # internal: обобщённый оператор LeftJoin
│   │   └── Models/
│   │       ├── Currency.cs                         # public: Value Object валюты (NumCode, CharCode, Name)
│   │       ├── CurrencyRate.cs                     # internal: Value Object курса за 1 ед. валюты
│   │       ├── ExchangeRateReport.cs               # internal: Value Object дневного среза котировок ЦБ РФ
│   │       ├── CurrencyRateDynamics.cs             # public: Value Object расчёта изменений по валюте
│   │       ├── CurrencyRatePoint.cs                # internal: Value Object точки курса на графике (Date, UnitRate)
│   │       ├── CurrencyRateTrend.cs                # internal: Value Object 7-дневного тренда валюты (Currency, Points)
│   │       ├── DailyExchangeRatesReport.cs         # public: Агрегат итогового аналитического отчёта
│   │       └── EmailAddress.cs                     # public: Value Object email-адреса с глубокой валидацией
│   ├── Services/
│   │   ├── ICbrClient.cs                           # internal: интерфейс клиента к API ЦБ РФ
│   │   ├── IExchangeRateAnalyticsService.cs        # public: интерфейс получения аналитического отчёта
│   │   ├── IPdfReportService.cs                    # public: интерфейс генерации PDF-документа
│   │   ├── IEmailService.cs                        # public: интерфейс отправки отчёта по электронной почте
│   │   └── ExchangeRateAnalyticsService.cs         # internal: сервис загрузки срезов и сборки отчёта
│   └── Infrastructure/
│       ├── CbrClient.cs                            # internal: HTTP-клиент к XML_daily.asp (windows-1251)
│       ├── CachedCbrClient.cs                      # internal: L1-декоратор кэширования срезов ЦБ РФ
│       ├── CachedExchangeRateAnalyticsService.cs   # internal: L1/L2-декоратор кэширования отчётов + Fail-Safe
│       ├── LastReportFileCache.cs                  # internal: потокобезопасное хранилище last-report.json
│       ├── PdfReportService.cs                     # internal: вёрстка PDF-отчёта (QuestPDF + ScottPlot SVG)
│       ├── SmtpEmailService.cs                     # internal: отправка письма с PDF через MailKit
│       ├── CbrClientOptions.cs                     # internal: настройки HTTP-клиента ЦБ РФ
│       ├── CacheOptions.cs                         # internal: настройки времени жизни и пути кэша
│       ├── SmtpOptions.cs                          # internal: настройки подключения к SMTP-серверу
│       ├── schema.xsd                              # XSD-схема ответа ЦБ РФ
│       ├── Dto/                                    # internal/EditorBrowsable(Never): XML и JSON Cache DTO
│       └── Mappers/
│           ├── CbrMapperExtensions.cs              # internal: маппинг XML DTO -> Domain (Tolerant Reader)
│           └── CacheMapperExtensions.cs            # internal: маппинг Domain <-> JSON Cache DTO
└── UI/                                             # Десктопное приложение Photino.Blazor
    ├── UI.csproj
    ├── Program.cs                                  # Инициализация окна Photino, Serilog и чтения config.json
    ├── App.razor                                   # UI-компонент отображения отчёта и управления операциями
    ├── _Imports.razor
    ├── config.example.json                         # Шаблон конфигурации
    └── wwwroot/
        ├── index.html
        └── css/app.css
```

### Инкапсуляция и граница публичного API `Core`

В соответствии с п. 2 и п. 9 ТЗ (*«Чёткое разделение Core/UI, понятный публичный API Core»*), проект `Core` закрывает все детали реализации модификатором `internal`, а слой `UI` вообще не импортирует пространства имён `Core.Infrastructure.*`. Снаружи библиотеки доступны **только** следующие типы:

| Категория | Публичный API `Core` (`public`) | Скрытая реализация внутри `Core` (`internal`) |
|---|---|---|
| **Регистрация в DI** | `DependencyInjection.AddCoreServices(IServiceCollection, IConfiguration)` | Настройка `QuestPDF`, привязка и валидация `CbrClientOptions`, `SmtpOptions`, `CacheOptions`, регистрация `HttpClient`, `XmlSerializer` и цепочки декораторов. |
| **Сервисные контракты** | `IExchangeRateAnalyticsService`<br>`IPdfReportService`<br>`IEmailService` | `ICbrClient`, `CbrClient`, `CachedCbrClient`, `ExchangeRateAnalyticsService`, `CachedExchangeRateAnalyticsService`, `LastReportFileCache`, `PdfReportService`, `SmtpEmailService`. |
| **Доменные модели и ошибки** | `DailyExchangeRatesReport`<br>`CurrencyRateDynamics`<br>`Currency`<br>`EmailAddress`<br>*(все с `private` конструкторами — `UI` может только читать свойства отчёта и вызывать `EmailAddress.Create`)* | `CbrErrors` (`TransientNetworkErrorType`, `IsTransientNetworkError`), `ExchangeRateReport`, `CurrencyRate`, `CurrencyRateTrend`, `CurrencyRatePoint`, а также все фабричные методы `DailyExchangeRatesReport.Create`, `CurrencyRateDynamics.Create`, `CurrencyRateTrend.Create`, `CurrencyRatePoint.Create`, `Currency.Create`, `ValidateRequestedDate`. |
| **Форматирование и DTO** | `FormattingExtensions` *(в `Core.Domain.Helpers`: публичны только методы, используемые в `UI`, а специфичные методы вёрстки PDF/Email закрыты как `internal`)* | `CbrMapperExtensions`, `CacheMapperExtensions`, `LinqExtensions`, `LastDailyExchangeRatesReportCacheDto`, `ExchangeRateReportCacheDto`, `CurrencyRateCacheDto`, `LastDailyExchangeRatesReportCacheJsonContext`. |

Такой дизайн гарантирует на уровне компилятора C#, что слой `UI` физически не может обойти валидацию, напрямую обратиться к `HttpClient` или `IMemoryCache`, вручную сконструировать фальшивый `DailyExchangeRatesReport` или изменить бизнес-правила расчёта.

---

## 5. Качество кода `Core`, обработка ошибок и подводные камни

### 5.1. Обработка ошибок без необработанных исключений (п. 4 ТЗ)

В проекте комбинируются два подхода:
1. **Внутри бизнес-логики и доменной модели** исключения не используются для управления потоком выполнения — все фабрики и сервисные методы возвращают дискриминированное объединение **`ErrorOr<T>`**.
2. **На границах инфраструктуры** (`HttpClient`, `XmlSerializer`, `MailKit`, `QuestPDF`, `File IO`, `IOptions`) все специфичные исключения перехватываются, логируются через `ILogger<T>` с полным контекстом и преобразуются в типизированный `Error.Failure` / `Error.Validation` / `Error.NotFound` или выделенный тип транзиентных сетевых сбоев `CbrErrors` (`Error.Custom`) с понятным сообщением для пользователя:

| Ситуация из п. 4 ТЗ | Где перехватывается | Какие ошибки и исключения обрабатываются | Поведение приложения |
|---|---|---|---|
| **Отсутствие сети или недоступность сайта ЦБ РФ** | `CbrClient`<br>`CbrErrors`<br>`ExchangeRateAnalyticsService`<br>`CachedExchangeRateAnalyticsService` | `HttpRequestException`, `IOException`, `TaskCanceledException` (таймаут `CbrClientOptions.Timeout` vs отмена пользователем), не-2xx HTTP-статусы → маркируются кастомным типом `CbrErrors` (`IsTransientNetworkError()`) | Если в кэше уже есть текущий и предыдущий срезы (`reports.Count >= 2`), отчёт на выбранную дату собирается с частичным 7-дневным графиком. Иначе при наличии кэша на диске включается **Fail-Safe** режим (возвращается последний сохранённый отчёт с предупреждающим баннером). |
| **Некорректный или пустой ответ сервиса ЦБ РФ** | `CbrClient`<br>`CbrMapperExtensions`<br>`ExchangeRateReport.Create` | Пустой поток, `InvalidOperationException` внутри `XmlSerializer`, некорректный формат даты, пустой список `<Valute>`, повреждённые отдельные записи `<Valute>` (без `<NumCode>`/`<CharCode>`), нулевой номинал, непарсящийся курс | Отдельные битые записи `<Valute>` изолированно пропускаются с записью `LogWarning` (**Tolerant Reader**). Если же нарушена целостность всего документа или в срезе нет ни одной валидной валюты — ошибка не маскируется офлайн-кэшем, а честно выводится пользователю. |
| **Некорректный адрес получателя** | `EmailAddress.Create` | Пустая строка (`"Не указан email-адрес получателя."`), несоответствие `EmailAddressAttribute`, ошибка `MailboxAddress.TryParse`, попытка подстановки отображаемого имени (`Display Name Injection`) | Валидация полностью делегирована в `Core` (`EmailAddress.Create`): при пустом или некорректном адресе в `SendReportAsync` мгновенно возвращается `Error.Validation` и выводится сообщение об ошибке **до** запуска генерации PDF и обращения к SMTP. |
| **Ошибка отправки по SMTP** | `SmtpEmailService` | `AuthenticationException`, `SaslException` (неверный логин/пароль приложения), `SslHandshakeException` (ошибка TLS), `SmtpCommandException` (отказ сервера принять адрес), `TimeoutException`, `SocketException`, `SmtpProtocolException` | Для каждого типа сбоя возвращается адресное сообщение (например, рекомендация проверить пароль приложения при ошибке авторизации или порт при ошибке TLS). |
| **Отсутствующий или некорректный `config.json`** | `UI/Program.cs`<br>`CbrClient`<br>`SmtpEmailService`<br>`CachedExchangeRateAnalyticsService` | `FileNotFoundException`, `InvalidDataException`, `FormatException` при старте; `OptionsValidationException` при обращении к `options.Value` | Приложение **не падает при запуске** даже при удалённом или синтаксически битом `config.json`: создаётся `StartupConfigState`, показывается системное диалоговое окно и баннер с подсказкой скопировать `config.example.json`. |

### 5.2. Подводные камни и как они были решены

#### 1. Поиск предыдущего дня по `ValCurs.Date`, а не по календарю
- **Проблема:** ЦБ РФ не публикует котировки по выходным и официальным праздникам. Если в воскресенье (`11.10.2026`) запросить курс, ЦБ РФ вернёт срез со штампом субботы `<ValCurs Date="10.10.2026">`. Если искать предыдущий день по календарю (`requestedDate.AddDays(-1)` = `10.10.2026`), то и за текущий, и за предыдущий день вернётся один и тот же субботний срез, а динамика по всем валютам станет `0,0000%` (или сработает ошибка валидации дат). Аналогично в новогодние праздники (`01.01`–`08.01`) курс не меняется целую неделю.
- **Решение:** В [`ExchangeRateAnalyticsService.cs`](./Core/Services/ExchangeRateAnalyticsService.cs) целевая дата каждого предшествующего среза всегда отсчитывается от **фактической даты котировки**, полученной из атрибута `Date` корневого элемента `<ValCurs>`:
  ```csharp
  var nextTargetDate = reports[^1].Date.AddDays(-1);
  ```
  Это гарантирует нахождение реальных предшествующих торговых дней при любой длительности выходных или праздников без холостых запросов внутри одного нерабочего интервала.

#### 2. Интервальное кэширование нерабочих дней (`CacheReportInterval`)
- **Проблема:** Когда на запрос за понедельник (`19.01`) ЦБ РФ возвращает курс, установленный в субботу (`17.01`), обычное кэширование по ключу запрошенной даты сохранило бы ответ только под ключом `19.01`. При переключении пользователя на `18.01` или `17.01` произошёл бы лишний сетевой запрос за тем же самым XML.
- **Решение:** Метод [`CachedCbrClient.CacheReportInterval`](./Core/Infrastructure/CachedCbrClient.cs) при обнаружении `report.Date < requestedDate` кэширует полученный `ExchangeRateReport` сразу для всех календарных дней диапазона `[report.Date .. requestedDate]`.

#### 3. Появление новых валют в выгрузке ЦБ РФ (кейс `19.01.2023`) и фолбэк `VunitRate`
- **Проблема:** 19 января 2023 года Банк России расширил перечень котируемых валют сразу на 9 позиций (дирхам ОАЭ, таиландский бат, вьетнамский донг и др.), которых не было в XML за 18 января 2023 года. Кроме того, в ранних исторических выгрузках ЦБ РФ (начиная с `02.07.1992`) мог отсутствовать элемент `<VunitRate>`.
- **Решение:**
  - Сопоставление валют в [`DailyExchangeRatesReport.Create`](./Core/Domain/Models/DailyExchangeRatesReport.cs) реализовано через кастомный оператор `LeftJoin` ([`LinqExtensions.cs`](./Core/Domain/Helpers/LinqExtensions.cs)). Если в предыдущем дне валюта отсутствовала (`previous is null`), `CurrencyRateDynamics.Create` присваивает `PreviousUnitRate = null`, `AbsoluteChange = null` и `PercentChange = null`. Такие валюты отображаются в таблице с прочерком `-` и не искажают расчёт Топ-3 и среднего процента изменения.
  - В [`CbrMapperExtensions.cs`](./Core/Infrastructure/Mappers/CbrMapperExtensions.cs) и [`CurrencyRate.cs`](./Core/Domain/Models/CurrencyRate.cs) реализован двойной разбор курса: приоритетно берётся `<VunitRate>`, а если он отсутствует — курс вычисляется как `Value / Nominal` с использованием культуры `ru-RU` (десятичный разделитель — запятая).

#### 4. Кодировка `windows-1251`, XSD-схема и управление памятью `XmlSerializer`
- **Проблема:** В .NET Core / .NET 8 кодовая страница `windows-1251` не включена в стандартный набор кодировок, а динамическое создание `XmlSerializer` с дополнительными параметрами на каждый вызов не кэширует сгенерированные сборки в памяти.
- **Решение:** В статическом конструкторе `CbrClient` регистрируется `CodePagesEncodingProvider.Instance`, классы DTO (`ValCurs`, `ValCursValute`) строго типизированы по схеме [`schema.xsd`](./Core/Infrastructure/schema.xsd) через `XmlSchemaClassGenerator` и скрыты из IntelliSense атрибутом `[EditorBrowsable(EditorBrowsableState.Never)]`, а экземпляр `XmlSerializer(typeof(ValCurs))` зарегистрирован в DI-контейнере как `Singleton`.

#### 5. Нюанс C# Source Generator `<EnableConfigurationBindingGenerator>` и `TimeSpan` в `[Range]`
- **Проблема:**
  1. При включённом генераторе привязки конфигурации `<EnableConfigurationBindingGenerator>true</EnableConfigurationBindingGenerator>` компилятор генерирует метод `BindCore(IConfiguration, ref T instance, ...)`. Если свойства в классах `Options` имеют аксессор `{ get; init; }`, сгенерированный код не может перезаписать свойства уже созданного объекта `instance` и генерирует пустое тело метода, оставляя `BaseUrl` и настройки `Smtp` пустыми.
  2. В атрибуте `[Range(typeof(TimeSpan), "00:01:00", "24:00:00")]` запись `"24:00:00"` вызывает `FormatException` внутри `TimeSpan.Parse`, так как сегмент часов принимает значения только от `0` до `23`.
- **Решение:** Во всех классах настроек (`CbrClientOptions`, `CacheOptions`, `SmtpOptions`) используются свойства `{ get; set; }`, а суточные интервалы в `[Range]` записаны в каноничном формате `d.hh:mm:ss` (`"1.00:00:00"` и `"7.00:00:00"`).

#### 6. Структурное равенство (`SequenceEqual`) в `record` с коллекциями и защита Email
- **Проблема:** Стандартная реализация `Equals` в C# `record` сравнивает свойства типа `IReadOnlyList<T>` по ссылке (`ReferenceEquals`), а не по элементам. Кроме того, стандартный `MailboxAddress.TryParse` допускает строки вида `"Hacker <victim@mail.ru>"`.
- **Решение:** В `ExchangeRateReport`, `CurrencyRateTrend` и `DailyExchangeRatesReport` переопределены `Equals` и `GetHashCode` с поэлементным сравнением через `SequenceEqual`. В `EmailAddress.Create` помимо `EmailAddressAttribute` и `MailboxAddress.TryParse` проверяется строгое совпадение `mailbox.Address == trimmed`, что полностью блокирует Display-Name и Header Injection.

#### 7. Дефекты исторических XML в базе ЦБ РФ за 1990-е годы (кейс `06.06.1996`, `R01720A`) и паттерн Tolerant Reader
- **Проблема:** В реальных архивных выгрузках `XML_daily.asp` Банка России за 1990-е годы встречаются записи, нарушающие собственную XSD-схему ЦБ РФ. Например, за `06.06.1996` элемент `<Valute ID="R01720A">` (*«Украинских карбованцев»*) содержит номинал и курс, но в нём полностью отсутствуют теги `<NumCode>` и `<CharCode>` (при десериализации `NumCode = 0`, `CharCode = null`). При наивной валидации по принципу *All-or-Nothing* одна битая запись из 24 валют приводила к отказу парсинга всего дневного среза (а также любого 7-дневного окна, задевающего эту дату).
- **Решение:** В маппере антикоррупционного слоя ([`CbrMapperExtensions.ToDomain`](./Core/Infrastructure/Mappers/CbrMapperExtensions.cs)) реализован паттерн **Tolerant Reader** (Мартин Фаулер): каждая запись `<Valute>` проходит индивидуальную доменную валидацию (`Currency.Create` → `CurrencyRate.Create`). Если конкретная запись в XML ЦБ РФ повреждена, она изолированно отбрасывается с записью `LogWarning` (с указанием ID валюты, даты среза и причины), а отчёт успешно формируется из остальных валидных котировок (23 из 24 за `06.06.1996`). Если же в ответе ЦБ РФ не осталось ни одной валидной валюты (например, пустой `<ValCurs>` за `01.06.1996`), срабатывает доменный инвариант `ExchangeRateReport.Create` (*«Список курсов валют не может быть пустым»*).

#### 8. Ложное срабатывание офлайн-режима (Fail-Safe) при ошибках данных/конфигурации и доменная таксономия `CbrErrors`
- **Проблема:** Если возвращать из `CbrClient` стандартный `Error.Failure` для любых сбоев (и сетевых, и ошибок `config.json`, и ошибок десериализации XML), а в `CachedExchangeRateAnalyticsService.GetFailSafeOrError` перехватывать **любую** ошибку (`IsError == true`), то при ошибке валидации данных или опечатке в `BaseUrl` в `config.json` кэш-декоратор молча подменяет ошибку старым отчётом из `last-report.json`. В результате UI видит несовпадение запрошенной и полученной даты и выводит ложный баннер *«Автономный режим (нет связи с сервером ЦБ РФ)»* при полностью работающем интернете.
- **Решение:** В [`Core/Domain/Errors/CbrErrors.cs`](./Core/Domain/Errors/CbrErrors.cs) выделена специализированная категория транзиентных сетевых сбоев через `Error.Custom(type: TransientNetworkErrorType, ...)` (`Cbr.ServerHttpError`, `Cbr.Timeout`, `Cbr.NetworkFailure`) и предикат `.IsTransientNetworkError()`. Механизм `GetFailSafeOrError` и частичная сборка офлайн-графика активируются **исключительно при реальных сетевых/транспортных отказах**, тогда как ошибки валидации, формата XML или конфигурации честно пробрасываются на экран пользователя.

#### 9. Graceful Degradation 7-дневного графика в офлайне (`reports.Count >= 2`) и защита кэша от перезаписи неполным окном (`IsHistoryComplete`)
- **Проблема:** Если пользователь загрузил отчёт на `12.10.2026` (в кэш попали срезы за `[06.10 .. 12.10]`), затем пропал интернет, и пользователь выбирает `10.10.2026`, то в кэше **уже есть** оба среза, необходимых для таблицы курсов и дневной динамики (`CurrentReport` за `10.10` и `PreviousReport` за `09.10`), но не хватает старых срезов за `04.10–05.10` для полного 7-дневного графика. Без разделения критичности срезов сетевая ошибка на 6-м дне ретроспективы отбросила бы весь отчёт за `10.10`. В то же время, если сохранить такой «урезанный» офлайн-отчёт в `IMemoryCache` и `last-report.json`, он перезапишет на диске полноценный 7-дневный кэш и не позволит докачать недостающие дни графика после восстановления связи.
- **Решение:**
  1. В [`ExchangeRateAnalyticsService.FetchHistoricalReportsAsync`](./Core/Services/ExchangeRateAnalyticsService.cs) при `reports.Count >= 2` транзиентная сетевая ошибка (`IsTransientNetworkError()`) при докачке глубоких архивных дней для графика не прерывает формирование отчёта ошибкой, а завершает цикл с `LogWarning` и строит отчёт на выбранную дату по доступным в кэше срезам.
  2. В агрегате [`DailyExchangeRatesReport`](./Core/Domain/Models/DailyExchangeRatesReport.cs) вычисляется свойство полноты исторического окна `IsHistoryComplete`.
  3. В [`CachedExchangeRateAnalyticsService.FetchAndCacheAsync`](./Core/Infrastructure/CachedExchangeRateAnalyticsService.cs) запись в `IMemoryCache` и `last-report.json` выполняется **строго при `freshReport.IsHistoryComplete == true`**. Благодаря этому частичный офлайн-отчёт отображается пользователю (с предупреждением в UI и примечанием под графиком в PDF), но не загрязняет L1/L2-кэш, а при появлении интернета автоматически достраивается до полных 7 дней.

#### 10. Фиксация 7-дневного окна графика относительно `RequestedDate` на длинных праздниках и учёт появления `EUR` в 1999 году
- **Проблема:**
  1. В новогодние каникулы (например, при выборе `05.01.2025`) действует курс ЦБ РФ, установленный `29.12.2024`. Если отсчитывать 7-дневное окно графика от даты среза ЦБ (`CurrentDate`), график покажет декабрьскую неделю `[23.12.2024 .. 29.12.2024]`, в которой вообще нет запрошенной пользователем даты `05.01.2025`.
  2. Валюта `EUR` начала котироваться Банком России только с `01.01.1999`. При запросе отчёта за 1992–1998 гг. или за первую неделю января 1999 г. (например, `04.01.1999`, когда окно захватывает конец декабря 1998 г.) кривая `EUR` отсутствует полностью или частично.
- **Решение:**
  1. Окно графика привязано к запрошенной календарной дате `[RequestedDate - 6 дней .. RequestedDate]` (`WindowStartDate` в `DailyExchangeRatesReport`), а `CurrencyRateTrend.Create` протягивает действующий курс (`Carry-Forward`) вплоть до `RequestedDate`.
  2. Метод [`FormattingExtensions.FormatWeeklyChartNote`](./Core/Domain/Helpers/FormattingExtensions.cs) анализирует полноту рядов `USD` и `EUR` относительно `ExpectedTrendDaysCount` и `IsHistoryComplete` и автоматически выводит под заголовком графика в PDF точное аналитическое пояснение (например, почему в 1996 г. отображён только `USD`, почему в начале января 1999 г. `EUR` показан за часть дней окна или почему в автономном режиме график построен по неполному числу дней из кэша).

---

## 6. Выполненные дополнительные задания (технический разбор)

### 6.1. Кэширование последних загруженных данных и формирование отчёта без подключения к сети

Система кэширования построена на **паттерне Decorator** поверх сервисов `ICbrClient` и `IExchangeRateAnalyticsService` и сочетает быстрый кэш в оперативной памяти (**L1 `IMemoryCache`**), персистентный файловый кэш на диске (**L2 `cache/last-report.json`**), частичную офлайн-сборку из кэшированных срезов и селективный режим отказоустойчивости (**Fail-Safe**).

```mermaid
flowchart TD
    Req["Запрос GetDailyAnalyticsAsync(date)"] --> Validate["Валидация даты (02.07.1992 .. Сегодня)"]
    Validate -->|Ошибка| ErrVal["Возврат Error.Validation"]
    Validate -->|ОК| L1{"Есть в L1 (IMemoryCache)?"}
    L1 -->|Hit| SyncL2["SaveIfChangedAsync в L2 (last-report.json)"] --> RetL1["Возврат отчёта из L1"]
    L1 -->|Miss| LoadL2["Чтение L2 (last-report.json) + Прогрев L1 всех интервалов HistoricalReports"]
    LoadL2 --> CheckL2{"L2.RequestedDate == date?"}
    CheckL2 -->|Да| SetL1["Запись в L1"] --> RetL2["Возврат отчёта из L2 (без сети)"]
    CheckL2 -->|Нет| Net["Запрос через ExchangeRateAnalyticsService -> CachedCbrClient -> CbrClient"]
    Net -->|Успех (IsHistoryComplete == true)| SaveAll["Запись в L1 + Атомарная запись в L2 (.tmp -> Move)"] --> RetFresh["Возврат полного отчёта"]
    Net -->|Успех (частичный офлайн-график, IsHistoryComplete == false)| RetPartial["Возврат отчёта без перезаписи L1/L2"]
    Net -->|Ошибка| CheckErr{"Есть L2 И ошибка IsTransientNetworkError()?"}
    CheckErr -->|Да| RetFailSafe["LogWarning + Возврат последнего отчёта из L2 (Fail-Safe)"]
    CheckErr -->|Нет| RetErr["Возврат реальной ошибки ErrorOr на UI"]
```

#### Два сценария офлайн-работы (отображение на UI + генерация PDF):

1. **Сценарий А — Офлайн в рамках текущей сессии и внутри закэшированного 7-дневного окна (из `IMemoryCache`, уровень L1):**
   - Во время работы приложения все загруженные аналитические отчёты кэшируются в памяти под ключами `cbr:daily-analytics:{yyyy-MM-dd}`, а сырые срезы котировок ЦБ РФ — под ключами `cbr:exchange-rates:{yyyy-MM-dd}` (причём метод `CachedCbrClient.CacheReportInterval` сразу заполняет весь диапазон нерабочих/праздничных дней `[report.Date .. requestedDate]`).
   - Если интернет пропадает во время работы приложения:
     - При повторном выборе любой ранее просмотренной даты `CachedExchangeRateAnalyticsService.TryGetFromMemoryCache` мгновенно возвращает `DailyExchangeRatesReport` из `IMemoryCache` (и синхронизирует `last-report.json` через `SaveIfChangedAsync`).
     - Если пользователь выбирает соседнюю дату внутри уже загруженного 7-дневного окна (для которой в L1-кэше есть `CurrentReport` и `PreviousReport`), `ExchangeRateAnalyticsService` берёт эти срезы из `CachedCbrClient` и формирует точный отчёт на выбранную дату даже тогда, когда более глубокие архивные срезы для 7-дневного графика недоступны без сети (`reports.Count >= 2` + `IsTransientNetworkError()`). При этом такой частичный отчёт (`IsHistoryComplete == false`) не перезаписывает `last-report.json`.
     - На **UI** полноценно отображаются карточки итогов дня (топ-3 роста, топ-3 падения, среднее изменение) и интерактивная таблица валют с фильтрацией и сортировкой, а нажатие кнопки **«Сохранить PDF»** (`SavePdfReportAsync`) генерирует PDF-файл полностью офлайн.

2. **Сценарий Б — Офлайн после перезапуска приложения на произвольную дату вне кэша (из файла `cache/last-report.json`, уровень L2 + Fail-Safe):**
   - После полного закрытия и повторного запуска приложения оперативный кэш `IMemoryCache` пуст.
   - При первом же запросе `CachedExchangeRateAnalyticsService` обращается к `LastReportFileCache.GetAsync()`, десериализует `cache/last-report.json` и методом `TryRestoreFromDiskCache` **прогревает `IMemoryCache`** (восстанавливая в памяти все календарные интервалы из `HistoricalReports`).
   - Если запрошенная дата совпадает с датой в `last-report.json` (`diskReport.RequestedDate == date`), отчёт сразу возвращается из файла и кэшируется в `IMemoryCache` без обращения к сети.
   - Если пользователь запрашивает новую дату вне кэшированного окна при отсутствии интернета, сетевой вызов возвращает транзиентную сетевую ошибку из каталога `CbrErrors` (`IsTransientNetworkError() == true`), после чего срабатывает механизм **`GetFailSafeOrError`**: сервис перехватывает сетевой сбой, логирует предупреждение и возвращает восстановленный из `last-report.json` отчёт (`diskReport`).
   - На **UI** отрисовывается полный сохранённый отчёт вместе с информационным баннером (*«Автономный режим (нет связи с сервером ЦБ РФ): не удалось загрузить данные на выбранную дату {loadedForDate}. Отображён последний сохранённый в кэше отчёт...»*), а кнопка **«Сохранить PDF»** (`SavePdfReportAsync`) генерирует полноценный PDF-документ из восстановленного файлового кэша и сохраняет его через нативный диалог ОС.

#### Технические детали реализации:
1. **Декоратор `CachedCbrClient` (L1 для XML-срезов ЦБ РФ):**
   - Кэширует объекты `ExchangeRateReport` в `IMemoryCache` под ключами `cbr:exchange-rates:{yyyy-MM-dd}` с комбинированной политикой вытеснения (`SlidingExpiration` + `AbsoluteExpirationRelativeToNow` из `CacheOptions`).
   - Заполняет сразу весь диапазон нерабочих дат `[report.Date .. requestedDate]` методом `CacheReportInterval`.
2. **Декоратор `CachedExchangeRateAnalyticsService` и `LastReportFileCache` (L1 + L2 для аналитического отчёта):**
   - **AOT-дружественная сериализация без рефлексии:** для записи и чтения `last-report.json` используется генератор исходного кода `System.Text.Json` (`[JsonSerializable(typeof(LastDailyExchangeRatesReportCacheDto))]` в [`LastDailyExchangeRatesReportCacheJsonContext.cs`](./Core/Infrastructure/Dto/LastDailyExchangeRatesReportCacheJsonContext.cs)).
   - **Нормализованное хранение и повторная валидация инвариантов:** в JSON-файл сохраняются только `RequestedDate` и упорядоченный список исходных срезов котировок `HistoricalReports` (`[0]` — актуальный срез `CurrentReport`, `[1]` — предыдущий торговый срез `PreviousReport`, `[2..N]` — срезы для покрытия 7-дневного окна графика), без дублирования вычисляемых коллекций (`AllRates`, `TopGrown`, `TopFallen`, `WeeklyTrends`). При загрузке с диска маппер [`CacheMapperExtensions.ToDomain`](./Core/Infrastructure/Mappers/CacheMapperExtensions.cs) заново проводит данные через всю цепочку доменных фабрик (`Currency.Create` → `CurrencyRate.Create` → `ExchangeRateReport.Create` → `DailyExchangeRatesReport.Create`), автоматически восстанавливая и таблицу динамики, и 7-дневные тренды для графика. Если файл кэша был повреждён или изменён вручную с нарушением инвариантов, он отбраковывается с записью `Warning` в лог.
   - **Защита кэша от деградации (`IsHistoryComplete`):** в L1 и L2 сохраняются только отчёты с полным 7-дневным покрытием (`if (freshReport.IsHistoryComplete)`), что предотвращает затирание полного кэша частичными офлайн-отчётами.
   - **Атомарная потокобезопасная запись (`SaveIfChangedAsync`):** конкурентный доступ синхронизируется через `SemaphoreSlim(1, 1)`. Чтобы исключить повреждение файла при внезапном завершении процесса или отключении питания, запись сначала производится во временный файл `last-report.json.tmp`, после чего выполняется атомарная замена `File.Move(tempPath, cacheFilePath, overwrite: true)` с гарантированной очисткой `.tmp` в блоке `finally`.

---

### 6.2. Структурированное логирование операций в файл (`Serilog`)

1. **Архитектура и конфигурация (`UI/Program.cs`):**
   - Все сервисы в `Core` используют стандартный интерфейс `Microsoft.Extensions.Logging.ILogger<T>`, не создавая жёсткой привязки к конкретной библиотеке логирования.
   - В точке входа [`UI/Program.cs`](./UI/Program.cs) подключён провайдер **`Serilog`** (`Serilog.Extensions.Logging` + `Serilog.Sinks.File`):
     - Файлы логов пишутся по пути `logs/app-YYYYMMDD.log` относительно базовой директории приложения с ежедневной ротацией (`RollingInterval.Day`).
     - Базовый уровень логирования — `Information`, при этом системные логи от `Microsoft.*` и `System.*` приглушены до `Warning` (`MinimumLevel.Override`), чтобы в лог-файле оставались только значимые события предметной области.
     - Шаблон вывода включает `SourceContext` (полное имя класса), что обеспечивает прозрачную трассировку вызовов между декораторами и сервисами:
       ```text
       {Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}
       ```
   - Зарегистрирован глобальный обработчик `AppDomain.CurrentDomain.UnhandledException`, который при любой критической ошибке среды выполнения записывает `Log.Fatal` и принудительно сбрасывает файловый буфер через `Log.CloseAndFlush()`.

2. **Сквозное покрытие всех этапов работы:**
   - **HTTP и парсинг (`CbrClient`, `CachedCbrClient`, `CbrMapperExtensions`):** старт запроса на дату, попадание в L1-кэш, успешное получение среза (с фактической датой ЦБ РФ и числом валют), предупреждения `LogWarning` при пропуске повреждённых архивных записей `<Valute>` (**Tolerant Reader**), ошибки HTTP/сети/таймаута и ошибки десериализации XML.
   - **Аналитика и файловый кэш (`ExchangeRateAnalyticsService`, `CachedExchangeRateAnalyticsService`, `LastReportFileCache`):** расчёт аналитики, частичная сборка графика в офлайне, чтение и сохранение `last-report.json`, восстановление из L2-кэша, отбраковка невалидного кэш-файла и активация Fail-Safe режима.
   - **PDF и SMTP (`PdfReportService`, `SmtpEmailService`):** начало и успешное завершение генерации PDF с указанием размера документа в байтах, подключение к SMTP-серверу (`Host:Port`), успешная отправка письма получателю и детализация ошибок SMTP/TLS/аутентификации.

---

### 6.3. График динамики курса (`USD` / `EUR`) за последние 7 дней в PDF-отчёте

1. **Эффективный сбор исторических срезов без лишних HTTP-запросов (`FetchHistoricalReportsAsync`):**
   - Вместо 7 наивных календарных запросов на каждый день недели [`ExchangeRateAnalyticsService`](./Core/Services/ExchangeRateAnalyticsService.cs) шагает назад строго от даты фактически действовавшего среза: `reports[^1].Date.AddDays(-1)`.
   - Цикл `while (reports.Count < 2 || reports[^1].Date > windowStartDate)` одновременно гарантирует наличие минимум 2 торговых срезов для таблицы динамики и полное покрытие 7-дневного календарного окна `[RequestedDate - 6 дней .. RequestedDate]` (`WindowStartDate`, за неделю обычно выполняется 5–6 запросов вместо 7, а в новогодние каникулы — всего 2 запроса).
2. **Протяжка курса на нерабочие дни (`Carry-Forward` / `LOCF` в `CurrencyRateTrend.Create`) и умные примечания в PDF:**
   - В финансовой отчётности в выходные и праздничные дни продолжает действовать последний официально установленный курс ЦБ РФ (`r.Date <= date`).
   - Метод [`CurrencyRateTrend.Create`](./Core/Domain/Models/CurrencyRateTrend.cs) строит непрерывный календарный ряд `CurrencyRatePoint` от `startDate` (`WindowStartDate`) до `endDate` (`RequestedDate`). Если соседние календарные дни попадают на один и тот же нерабочий срез (`ReferenceEquals(effectiveReport, lastEffectiveReport)`), повторный поиск валюты по списку `Rates` не выполняется.
   - Если для архивной даты (например, в 1992–1998 гг. до введения евро) валюта `EUR` отсутствует в выгрузке ЦБ РФ (`ErrorType.NotFound`) или котировалась только часть недели (начало января 1999 г.), либо отчёт собран в офлайне из неполного кэша (`!IsHistoryComplete`), `DailyExchangeRatesReport` строит график по доступным точкам, а [`FormattingExtensions.FormatWeeklyChartNote`](./Core/Domain/Helpers/FormattingExtensions.cs) выводит под заголовком графика в PDF курсивное пояснение.
3. **Векторная отрисовка SVG в PDF (`ScottPlot` + `QuestPDF`):**
   - В [`PdfReportService.ComposeWeeklyTrendsChart`](./Core/Infrastructure/PdfReportService.cs) библиотека **`ScottPlot`** формирует чистый векторный SVG (`plot.GetSvgXml`), который встраивается в документ `QuestPDF` через `row.RelativeItem().Svg(svgXml)`. Благодаря векторному формату линии тренда, маркеры и подписи значений курса сохраняют идеальную чёткость при любом масштабировании и печати.

---

## 7. Использованные библиотеки и обоснование выбора

### Проект `Core`

| Библиотека / Инструмент | Версия | Назначение в проекте | Обоснование выбора |
|---|---|---|---|
| **[`ErrorOr`](https://github.com/amantinband/error-or)** | `2.1.1` | Result-паттерн (`ErrorOr<T>`) для возврата ошибок из доменных фабрик и сервисов | Позволяет явно описывать ошибки валидации, сети и инфраструктуры в сигнатурах методов без дорогих `throw/catch` в бизнес-логике. |
| **[`QuestPDF`](https://www.questpdf.com/)** | `2026.9.1` | Генерация табличного PDF-отчёта (`PdfReportService`) | Современный декларативный C# Fluent API для вёрстки PDF без необходимости вручную считать координаты текста и таблиц; высокая скорость генерации и встроенная поддержка кириллических системных шрифтов и векторных SVG. |
| **[`ScottPlot`](https://scottplot.net/)** | `5.1.59` | Генерация векторного SVG-графика динамики курсов за 7 дней (`PdfReportService`) | Быстрая библиотека научной и финансовой графики для .NET с поддержкой headless-генерации чистого SVG (`GetSvgXml`) без зависимости от системного UI-контекста. |
| **[`MailKit`](https://github.com/jstedfast/MailKit)** | `4.18.1` | Отправка писем с PDF-вложением по протоколу SMTP (`SmtpEmailService`) | Индустриальный стандарт для .NET (рекомендован Microsoft взамен устаревшего `System.Net.Mail.SmtpClient`); поддерживает полноценный асинхронный I/O, современные механизмы аутентификации и `SecureSocketOptions.Auto`. |
| **`Microsoft.Extensions.Caching.Memory`** | `8.0.1` | Внутрипроцессное L1-кэширование (`IMemoryCache`) | Стандартный высокопроизводительный потокобезопасный кэш .NET с поддержкой комбинированных политик `SlidingExpiration` и `AbsoluteExpiration`. |
| **`Microsoft.Extensions.Http`** | `8.0.0` | Фабрика `IHttpClientFactory` и типизированный клиент `AddHttpClient<CbrClient>` | Предотвращает исчерпание сокетов (`Socket Exhaustion`) и управляет временем жизни `HttpMessageHandler`. |
| **`Microsoft.Extensions.Options.DataAnnotations`** | `8.0.0` | Валидация классов настроек (`CbrClientOptions`, `CacheOptions`, `SmtpOptions`) | Обеспечивает декларативную проверку конфигурации через атрибуты `[Required]`, `[Url]`, `[EmailAddress]`, `[Range]`. |
| **`Microsoft.Extensions.Configuration.Binder`** | `8.0.0` | Привязка секций `IConfiguration` к строго типизированным классам `Options` | В сочетании с `<EnableConfigurationBindingGenerator>true</EnableConfigurationBindingGenerator>` генерирует код привязки на этапе компиляции без использования рефлексии. |
| **`Microsoft.Extensions.DependencyInjection`** | `8.0.0` | DI-контейнер приложения | Позволяет чисто изолировать регистрацию зависимостей и декораторов внутри `Core.DependencyInjection.AddCoreServices`. |
| **`XmlSchemaClassGenerator` (`xscgen`)** | `3.0.1405` | Генерация C# DTO (`ValCurs`, `ValCursValute`) по схеме `schema.xsd` | Автоматизирует создание строго типизированных классов разбора XML-ответа ЦБ РФ (команда `make dto-gen` в [`Makefile`](./Makefile)). |

### Проект `UI`

| Библиотека | Версия | Назначение в проекте | Обоснование выбора |
|---|---|---|---|
| **[`Photino.Blazor`](https://github.com/tryphotino/photino.Blazor)** | `4.*` | Десктопная оболочка для Blazor поверх системного WebView | В отличие от Electron, использует нативный системный движок ОС (WebView2 на Windows), обеспечивая минимальный размер сборки, мгновенный старт окна и доступ к нативным диалогам ОС (`ShowSaveFileAsync`, `ShowMessage`). |
| **[`Serilog.Extensions.Logging`](https://github.com/serilog/serilog-extensions-logging)** | `8.0.0` | Интеграция `Serilog` с `Microsoft.Extensions.Logging` | Позволяет подключить мощный пайплайн ротации логов `Serilog` в `UI`, сохранив в `Core` чистую зависимость только от стандартного `ILogger<T>`. |
| **[`Serilog.Sinks.File`](https://github.com/serilog/serilog-sinks-file)** | `6.0.0` | Запись структурированных логов в файл | Надёжный файловый приёмник с поддержкой ежедневной ротации (`RollingInterval.Day`), форматирования шаблонов и буферизованного сброса на диск. |