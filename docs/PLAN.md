# My Digital Library — План за имплементация

> **Този документ е входът за кодиращия агент (VS Code / Claude Code).**
> Чети го целия преди да напишеш първия ред код. Работи **етап по етап** —
> не прескачай напред и не започвай Етап N+1, докато Етап N не е завършен,
> тестван и комитнат.

---

## 0. Правила за работа на агента

1. **Един етап = един разговор = един commit range.** В края на всеки етап:
   `dotnet build` + `dotnet test` минават, `docker compose up` вдига всичко, commit.
2. **Не измисляй API-та, NuGet пакети или config ключове.** Ако не си сигурен в
   сигнатура или в поведение на библиотека — провери в официалната документация
   или кажи, че не си сигурен. Никога не представяй предположение като факт.
3. **Малък, компилиращ пример > дълго обяснение.**
4. **Всеки етап има изричен "извън обхвата" списък.** Спазвай го. Не добавяй
   функционалност "за всеки случай" (YAGNI).
5. **Език:** целият код, имената на типове, таблици, API-та, commit съобщения и
   документацията са на **английски**. Потребителският интерфейс поддържа
   **български и английски** (виж Етап 5).
6. **Conventional commits:** `feat(domain): add ReadingSession aggregate`,
   `fix(api): return 409 on duplicate ISBN`, `chore(docker): pin postgres 17`.
7. **Тестове заедно с кода, не след него.** Домейн логика без тест не е готова.
8. Ако намериш противоречие между този план и реалността (напр. API-то се е
   променило) — **спри, опиши проблема, предложи вариант**, не импровизирай мълчаливо.

---

## 1. Технологичен стек

| Слой | Технология | Бележка |
|---|---|---|
| Backend | ASP.NET Core Web API, **.NET 10 (LTS)** | .NET 10 е текущият LTS (издаден ноем. 2025). Провери с `dotnet --list-sdks` и ако имаш по-нов LTS — използвай него. |
| Frontend | **Angular 22** (standalone components, signals, typed reactive forms) | Angular 22.x е текущата стабилна линия (септ. 2026). Провери с `ng version` след scaffold. |
| База | **PostgreSQL 17**, EF Core, code-first миграции | Npgsql provider: `Npgsql.EntityFrameworkCore.PostgreSQL` |
| Auth | ASP.NET Core Identity + **cookie auth** | Не JWT в localStorage. Причина в Етап 4. |
| Хостинг | Docker Compose: `db`, `api`, `web` (nginx), по избор `pgadmin` | Локално първо; деплой по-късно без пренаписване. |
| Background work | `BackgroundService` + `PeriodicTimer` | Без Hangfire/Quartz в началото — KISS. |
| Тестове | xUnit, FluentAssertions, NSubstitute, Testcontainers (PostgreSQL) | |

> ⚠️ **Проверявай версиите.** Числата по-горе са към септември 2026. Първото нещо,
> което правиш, е `dotnet --list-sdks` и `npx @angular/cli@latest version`.

---

## 2. Структура на solution-а (Clean Architecture)

```
D:\01. Work\MyDigitalLibrary\
├─ .github/workflows/ci.yml
├─ docker-compose.yml
├─ docker-compose.override.yml        # dev-only (портове, hot reload)
├─ .env.example
├─ .gitignore
├─ README.md
├─ docs/
│  ├─ PLAN.md                         # този файл
│  └─ adr/                            # Architecture Decision Records
├─ src/
│  ├─ MyDigitalLibrary.Domain/        # ЧИСТ. Няма EF, няма HTTP, няма DI атрибути.
│  ├─ MyDigitalLibrary.Application/   # use cases, интерфейси (портове), DTO-та
│  ├─ MyDigitalLibrary.Infrastructure/# EF Core, Identity, HTTP клиенти, storage
│  ├─ MyDigitalLibrary.Api/           # Minimal API / контролери, композиция
│  └─ web/                            # Angular workspace
└─ tests/
   ├─ MyDigitalLibrary.Domain.Tests/
   ├─ MyDigitalLibrary.Application.Tests/
   └─ MyDigitalLibrary.Api.IntegrationTests/
```

**Правило за зависимости (проверявай го):**
`Domain` ← `Application` ← `Infrastructure` ← `Api`.
`Domain` няма **нито един** project reference и няма NuGet пакет освен BCL.
Ако се изкушиш да добавиш `Microsoft.EntityFrameworkCore` в Domain — спри.

---

## 3. Домейн модел

### 3.1 Централното разделение

Това е най-важното решение в проекта. Не го компрометирай.

- **`Work`** — самата книга като произведение. „Дюна" на Франк Хърбърт.
  Заглавие, оригинално заглавие, автор(и), серия + позиция, описание,
  година на първо издание, жанрове.
- **`Edition`** — конкретно издание на `Work`. ISBN, издател, език, преводач,
  година на **това** издание, брой страници, тип корица (твърда/мека),
  корица (изображение), за аудио: разказвач + продължителност.
- **`LibraryItem`** (наричан още Copy) — **моят** екземпляр. Едно `Edition` в
  конкретен `Format`, с притежателски статус, местоположение, цена на придобиване.

> Една и съща „Дюна" може законно да присъства **три пъти** в библиотеката —
> хартиена, ebook и аудио. Това не е дубликат.

### 3.2 Enum-и

```csharp
public enum BookFormat { Physical = 1, Ebook = 2, Audiobook = 3 }

public enum CoverType { Unknown = 0, Hardcover = 1, Paperback = 2 }   // само за Physical

public enum OwnershipStatus { Owned = 1, Borrowed = 2, LentOut = 3, Sold = 4, GivenAway = 5 }

public enum ReadingStatus { Reading = 1, Finished = 2, Abandoned = 3, OnHold = 4 }

public enum MarketAvailability { Unknown = 0, InStock = 1, OutOfStock = 2, Discontinued = 3 }
```

> Забележи: `Wanted` **не е** в `OwnershipStatus`. Виж 3.4.

### 3.3 Value objects

```csharp
public sealed record Isbn                 // валидиран ISBN-10/13 с checksum
{
    public string Value { get; }          // винаги нормализиран към ISBN-13, без тирета
    public static Result<Isbn> TryCreate(string raw);
}

public sealed record Money(decimal Amount, string CurrencyCode);   // ISO 4217

public sealed record AudioDuration(TimeSpan Value);

public sealed record SeriesPosition(decimal Value);  // decimal заради „книга 2.5"

public sealed record PhysicalLocation(string? Room, string? Shelf, string? Box);
```

### 3.4 „Искам я" vs „Имам я" — без nullable soup

**Решение:** желаните книги са **отделен агрегат**, не статус на `LibraryItem`.

```csharp
// Искам я — няма дата на придобиване, няма цена на придобиване, няма локация.
public sealed class WishlistEntry : Entity
{
    public Guid UserId { get; private set; }
    public Guid WorkId { get; private set; }
    public Guid? PreferredEditionId { get; private set; }  // може да искам „кое да е издание"
    public BookFormat DesiredFormat { get; private set; }
    public int Priority { get; private set; }              // 1..5
    public Money? MaxPrice { get; private set; }
    public string? Note { get; private set; }
    public DateOnly AddedOn { get; private set; }

    // Купих я → затваряме желанието и създаваме LibraryItem.
    public LibraryItem Fulfill(Guid editionId, Acquisition acquisition);
}

// Имам я (или съм я имал) — винаги има Acquisition.
public sealed class LibraryItem : Entity
{
    public Guid UserId { get; private set; }
    public Guid EditionId { get; private set; }
    public BookFormat Format { get; private set; }
    public OwnershipStatus Status { get; private set; }
    public Acquisition Acquisition { get; private set; }   // НЕ е nullable
    public PhysicalLocation? Location { get; private set; } // само за Physical
    public string? PersonalNote { get; private set; }
}

public sealed record Acquisition(
    DateOnly AcquiredOn,
    AcquisitionMethod Method,       // Bought, Gift, Borrowed, Inherited, Downloaded
    Money? Price,                   // подарък няма цена — това nullable е честно
    string? Source);                // „Хеликон Витоша", „от Иван", „Humble Bundle"
```

**Обосновка:** „искам" и „имам" имат различни задължителни полета и различен
жизнен цикъл. Сливането им в един ентитет прави половината колони nullable и
инвариантите непроверими. Преходът е явен метод `Fulfill()`, не промяна на enum.

### 3.5 Четене — сесии и прогрес

```csharp
public sealed class ReadingSession : Entity
{
    public Guid UserId { get; private set; }
    public Guid LibraryItemId { get; private set; }   // сесията е към КОНКРЕТЕН екземпляр
    public DateOnly StartedOn { get; private set; }
    public ReadingStatus Status { get; private set; }
    public DateOnly? EndedOn { get; private set; }    // Finished или Abandoned

    private readonly List<ProgressEntry> _progress = new();
    public IReadOnlyList<ProgressEntry> Progress => _progress.AsReadOnly();

    public void RecordProgress(ProgressPoint point, DateTimeOffset at);  // валидира формата
    public void Finish(DateOnly on);
    public void Abandon(DateOnly on, string? reason);
}
```

Препрочитане → **нова** `ReadingSession` за същия `LibraryItem`. Историята се пази.

**Прогресът НЕ е един `int`:**

```csharp
public abstract record ProgressPoint
{
    public abstract BookFormat[] ValidFor { get; }
}

public sealed record PageProgress(int Page) : ProgressPoint
{
    public override BookFormat[] ValidFor => [BookFormat.Physical, BookFormat.Ebook];
}

public sealed record PercentProgress(decimal Percent) : ProgressPoint   // 0..100
{
    public override BookFormat[] ValidFor => [BookFormat.Ebook];
}

public sealed record TimestampProgress(TimeSpan Position) : ProgressPoint
{
    public override BookFormat[] ValidFor => [BookFormat.Audiobook];
}
```

`ReadingSession.RecordProgress` хвърля доменно изключение, ако видът прогрес не
отговаря на формата на екземпляра. **Това е инвариант, не валидация в UI.**

За статистики („колко % съм прочел") се въвежда явна проекция
`ProgressPoint.ToFraction(EditionExtent extent)`, където
`EditionExtent` е `PageCount` или `Duration` от `Edition`. Ако липсва extent —
върни `null`, не гадай.

### 3.6 Рейтинг и ревю — явно решение

- **`WorkRating`** — оценката е за **произведението**, не за изданието.
  Уникален индекс `(UserId, WorkId)`. Едно произведение, една оценка.
  *Обосновка:* оценяваш историята, не хартията.
- **`Review`** — също за **`Work`**, per user.
  *Обосновка:* ревюто е за съдържанието; ако го вържеш за Edition, препрочитане
  в друг формат ти дава две несвързани ревюта за една и съща книга.
- Коментарите, специфични за изданието (лош превод, слаб разказвач, лошо
  качество на печата) отиват в **`LibraryItem.PersonalNote`** и в
  `Edition.TranslatorNote`. **Няма nullable `EditionId` в `Review`.**

### 3.7 Останалите ентитети

```
Author            Id, FullName, SortName, BirthYear?, DeathYear?, Bio?, ExternalIds
WorkAuthor        WorkId, AuthorId, Role (Author|CoAuthor|Editor|Illustrator)
Series            Id, Name, Description?
Genre             Id, Name, ParentGenreId?          // споделен каталог
Tag               Id, UserId, Name                  // ПОТРЕБИТЕЛСКИ — на LibraryItem
Shelf             Id, UserId, Name, IsSystem        // „Прочетени", „Текущи" са системни
ShelfItem         ShelfId, LibraryItemId, AddedOn, SortOrder
Note              Id, UserId, LibraryItemId, Body, LocationInBook?, CreatedAt
Quote             Id, UserId, WorkId, Text, PageOrPosition?, CreatedAt
Loan              Id, UserId, LibraryItemId, BorrowerName, LentOn, DueOn?, ReturnedOn?
ReadingGoal       Id, UserId, Year, TargetBooks?, TargetPages?
Bookstore         Id, Name, BaseUrl, AdapterKey
BookstoreListing  Id, EditionId, BookstoreId, Url, Availability, Price?, LastCheckedAt, ConsecutiveFailures
ImportJob         Id, UserId, Kind, Status, SourceFileName?, Stats, StartedAt, FinishedAt?
```

**Разделение „споделен каталог" vs „мои данни":**

| Споделени (не носят UserId) | Потребителски (носят UserId) |
|---|---|
| Work, Edition, Author, Series, Genre, Bookstore, BookstoreListing | LibraryItem, WishlistEntry, ReadingSession, WorkRating, Review, Tag, Shelf, Note, Quote, Loan, ReadingGoal, ImportJob |

Това прави multi-user режима безплатен по-късно.

### 3.8 Агрегати

- `Work` (корен) ← `WorkAuthor`, връзка към `Series`
- `Edition` (корен) — реферира `Work` по Id
- `LibraryItem` (корен) — реферира `Edition` по Id
- `ReadingSession` (корен) ← `ProgressEntry`
- `Shelf` (корен) ← `ShelfItem`
- `WishlistEntry` (корен)

**Между агрегати се реферира само по `Guid`, не по навигационно свойство.**
(EF Core ще има навигации за заявки, но доменните методи не ги ползват за писане.)

---

## 4. API контракти

**Конвенции:**
- Базов път: `/api/v1/...` (версия в URL — най-простото, което не те заключва).
- `ProblemDetails` (RFC 9457) за **всички** грешки, с `extensions.errorCode`
  (стабилен низ като `isbn.invalid`, `edition.duplicate`) — фронтендът превежда кода.
  **Бекендът не връща преведени текстове** (виж Етап 5 / i18n решението).
- Пагинация: `?page=1&pageSize=50` → отговор `{ items, page, pageSize, totalCount }`.
- Сортиране: `?sort=title,-year`.
- `201 Created` + `Location` при създаване. `204` при успешен DELETE/PUT без тяло.
- `409 Conflict` при нарушен уникален индекс, не `500`.
- OpenAPI документ се генерира автоматично (в .NET 10: вградената
  `Microsoft.AspNetCore.OpenApi` поддръжка — провери точния API при scaffold).

**Основни ресурси:**

```
GET    /api/v1/library-items?format=&status=&shelfId=&q=&page=&pageSize=
POST   /api/v1/library-items
GET    /api/v1/library-items/{id}
PUT    /api/v1/library-items/{id}
DELETE /api/v1/library-items/{id}
PATCH  /api/v1/library-items/{id}/location
PATCH  /api/v1/library-items/{id}/status

GET    /api/v1/works/{id}
GET    /api/v1/works/{id}/editions
PUT    /api/v1/works/{id}/rating          # upsert, 1..5 или 1..10 — избери и документирай
PUT    /api/v1/works/{id}/review

GET    /api/v1/wishlist
POST   /api/v1/wishlist
POST   /api/v1/wishlist/{id}/fulfill      # → създава LibraryItem, връща 201 + Location

POST   /api/v1/reading-sessions
POST   /api/v1/reading-sessions/{id}/progress
POST   /api/v1/reading-sessions/{id}/finish
POST   /api/v1/reading-sessions/{id}/abandon

POST   /api/v1/import/lookup              # { url } или { isbn } → кандидат за преглед
POST   /api/v1/import/csv                 # multipart → ImportJob
GET    /api/v1/import/jobs/{id}

GET    /api/v1/editions/{id}/listings     # оферти по книжарници
POST   /api/v1/editions/{id}/listings     # ръчно добавяне на линк към книжарница

GET    /api/v1/export?format=json|csv     # целият архив — без vendor lock-in
GET    /api/v1/statistics?year=2026
GET    /api/v1/search?q=...               # full-text (Етап 9)
```

---

## 5. Импорт по линк / ISBN — сърцето на „да не пиша ръчно"

### 5.1 Поток

```
URL или ISBN
   ↓
IBookLinkResolver (chain of responsibility)
   ├─ site-specific resolver-и (по-късно)
   └─ GenericIsbnPageResolver  ← това е v1
   ↓ ISBN
IBookMetadataProvider (композитен)
   ├─ OpenLibraryProvider
   └─ GoogleBooksProvider
   ↓ BookMetadataCandidate  (собствен на Application слоя — БЕЗ provider DTO-та)
   ↓
MetadataMergePolicy  (ръчните ми промени винаги печелят)
   ↓
Екран за преглед и потвърждение → запис
```

### 5.2 `GenericIsbnPageResolver` — извличане на ISBN от произволна страница

Опитва по ред и спира на първия валиден ISBN (с проверена контролна сума):

1. **JSON-LD** — `<script type="application/ld+json">` с `"@type": "Book"` → `isbn`
2. **Meta тагове** — `<meta property="books:isbn">`, `<meta itemprop="isbn">`
3. **Microdata** — елемент с `itemprop="isbn"`
4. **Regex върху видимия текст** — `ISBN(?:-1[03])?:?\s*([\d\-–\s]{10,20}[\dXx])`
5. Ако намери няколко — връща всички като кандидати и пита потребителя.

Библиотека за парсване на HTML: **AngleSharp** (активно поддържана, MIT).
`HtmlAgilityPack` също върши работа. Не използвай regex за парсване на HTML —
само за извличане на ISBN от вече извлечен текст.

> **Важно:** валидирай checksum-а и на ISBN-10, и на ISBN-13, иначе ще хващаш
> случайни числа. Нормализирай към ISBN-13 за съхранение, но пази оригинала.

### 5.3 Доставчици на метаданни

**Open Library** (без ключ, безплатно):
- `GET https://openlibrary.org/api/books?bibkeys=ISBN:{isbn}&format=json&jscmd=data`
- `GET https://openlibrary.org/isbn/{isbn}.json`
- Корица: `https://covers.openlibrary.org/b/isbn/{isbn}-L.jpg`
- Изисква описателен `User-Agent` header с контакт.

**Google Books** (работи и без ключ при ниски обеми; ключ по избор):
- `GET https://www.googleapis.com/books/v1/volumes?q=isbn:{isbn}`
- Ключ се подава като `&key={apiKey}` — държи се в `.env`, никога в git.

> ⚠️ **Провери и двата endpoint-а срещу актуалната документация преди да пишеш
> кода.** Формите на URL-ите по-горе са по мои сведения коректни, но не ги
> приемай на доверие — направи по едно ръчно извикване с `curl` и виж отговора.
> Български заглавия често липсват и в двата източника — това е очаквано,
> ръчното допълване остава нужно.

### 5.4 Абстракцията

```csharp
// Application слой. Провайдърските DTO-та НЕ излизат оттук.
public interface IBookMetadataProvider
{
    string ProviderKey { get; }
    Task<BookMetadataCandidate?> LookupByIsbnAsync(Isbn isbn, CancellationToken ct);
}

public sealed record BookMetadataCandidate(
    string ProviderKey,
    string? Title,
    string? OriginalTitle,
    IReadOnlyList<string> AuthorNames,
    string? Publisher,
    int? PublicationYear,
    string? Language,
    int? PageCount,
    string? Description,
    Uri? CoverUrl,
    string? SeriesName,
    decimal? SeriesPosition,
    IReadOnlyList<string> Genres);
```

Композитният провайдър пита всички регистрирани източници, обединява ги по
приоритет (Open Library → Google Books, конфигурируем ред), и връща **един**
кандидат + списък с алтернативи за полетата, където източниците се разминават.

Регистрация: `services.AddHttpClient<OpenLibraryProvider>()` с
`Polly`/`Microsoft.Extensions.Http.Resilience` за retry и timeout.
**Провери кой от двата пакета е актуалният препоръчван за .NET 10** преди да избереш.

### 5.5 „Ръчните ми промени винаги печелят"

```csharp
// Всеки enrichable ентитет пази кои полета са пипани ръчно.
public sealed class ManualFieldOverrides   // owned type, съхранява се като jsonb
{
    private readonly HashSet<string> _fields = new();
    public bool IsOverridden(string field);
    public void MarkOverridden(string field);
}
```

При re-enrichment: за всяко поле — ако е в `ManualFieldOverrides`, **пропусни го**.
Покрий това с юнит тест: „enrich → редактирам заглавие ръчно → enrich пак →
заглавието ми е останало".

### 5.6 Корици

`ICoverStorage` абстракция; `LocalFileCoverStorage` пише в Docker volume,
имена по SHA-256 на съдържанието. **Не съхранявай изображения в базата.**
Сваляне на корицата е background job, не блокира записа на книгата.
Максимален размер, whitelist на content-type, ресайз до 2 варианта (thumb / full).

---

## 6. Наличност по книжарници (автоматична проверка по график)

> ⚠️ **Прочети това преди да кодиш.** Ozone.bg, Helikon.bg, Ciela.com и
> Orange Center **нямат публични API**. Единственият път е scraping на HTML,
> което: (а) се чупи при всяка смяна на дизайна, (б) може да е в противоречие с
> Условията за ползване на сайта. Провери `robots.txt` и ToS на всеки сайт
> преди да добавиш адаптер. Това е лична употреба и малък обем — дръж го
> учтиво. Аз не съм юрист; ако имаш съмнения, недей.

### 6.1 Дизайн

```csharp
public interface IBookstoreAdapter
{
    string AdapterKey { get; }                  // "ozone", "helikon", ...
    Task<OfferSnapshot> FetchAsync(Uri listingUrl, CancellationToken ct);
}

public sealed record OfferSnapshot(
    MarketAvailability Availability,
    Money? Price,
    DateTimeOffset CheckedAt);
```

**Селекторите са конфигурация, не код.** Смяна на дизайн = промяна в
`appsettings.json` / ред в базата, не нов deploy:

```json
{
  "Bookstores": {
    "ozone": {
      "PriceSelector": ".product-price .price",
      "AvailabilitySelector": ".availability",
      "InStockMarkers": [ "в наличност", "налична" ],
      "OutOfStockMarkers": [ "изчерпана", "няма наличност" ],
      "MinDelayBetweenRequests": "00:00:03"
    }
  }
}
```

> Селекторите горе са **примерни** — агентът трябва да ги открие сам, като
> погледне реалния HTML на всяка книжарница, и да ги запише в конфигурацията.
> Не ги приемай за верни.

### 6.2 Background service

```csharp
public sealed class AvailabilityRefreshService : BackgroundService
{
    // PeriodicTimer, веднъж дневно (конфигурируемо).
    // За всеки listing: rate limit per host, User-Agent с контакт,
    // exponential backoff при грешка.
    // След N последователни неуспеха → Availability = Unknown + лог warning.
    // НИКОГА не записвай OutOfStock, ако scrape-ът се е провалил.
}
```

Задължително:
- Отделен DI scope на всяка итерация (`IServiceScopeFactory`) — иначе captive dependency.
- `CancellationToken` се уважава навсякъде.
- История на цените: `PriceHistoryEntry (ListingId, Price, ObservedAt)` — така
  „изчерпана ли е" и „поевтиня ли" се отговарят без нов scrape.
- Флаг за глобално изключване: `Bookstores:Enabled = false`.

### 6.3 Ръчният вариант винаги работи

Дори когато всички адаптери са изключени, потребителят може ръчно да:
- зададе `Availability` на `Discontinued` („изчерпана")
- добави линк към книжарница със свободен текст

**Приложението трябва да е напълно използваемо без интернет.**

---

## 7. База данни и EF Core

- Схема: `public`. Именуване: `snake_case` таблици и колони
  (EF Core: конфигурирай явно или чрез `EFCore.NamingConventions` — провери дали
  пакетът поддържа текущата версия на EF Core преди да го добавиш; ако не —
  именувай ръчно в конфигурациите).
- **Една `IEntityTypeConfiguration<T>` на ентитет**, в `Infrastructure/Persistence/Configurations/`.
  Без `[Attribute]` mapping в домейна.
- `DateOnly` / `TimeSpan` се поддържат от Npgsql — провери точното мапване.
- Уникални индекси: `Edition.Isbn13` (филтриран, `WHERE isbn13 IS NOT NULL`),
  `(UserId, WorkId)` за `WorkRating`, `(UserId, Name)` за `Tag` и `Shelf`.
- Индекси за често филтрираните: `LibraryItem(UserId, Status, Format)`,
  `BookstoreListing(LastCheckedAt)`.
- **`AsNoTracking()` по подразбиране за всички read заявки.** Проекция към DTO с
  `Select`, никога `Include` на целия граф за списъчен екран.
- Никакъв lazy loading. Изключи го изрично.
- Миграциите се комитват. Всяка миграция се преглежда преди commit —
  генерираният SQL трябва да е разбираем.
- `ProgressPoint` мапване: owned type с дискриминатор
  `progress_kind` + `page_value` / `percent_value` / `position_ticks`
  (nullable само на ниво база, домейнът остава чист).
- Soft delete: **не в v1.** Ако решиш да я има по-късно — глобален query filter.

---

## 8. Автентикация и multi-user готовност

**Решение: ASP.NET Core Identity + cookie authentication.**
*Обосновка:* Angular и API-то седят зад един reverse proxy на един origin.
При този сетъп cookie с `HttpOnly` + `SameSite=Strict` е по-безопасно от JWT в
`localStorage` (който е достъпен за всеки XSS). Няма нужда от refresh token логика.

Задължително:
- Anti-forgery токени за state-changing заявки.
- `ICurrentUser` (scoped) в Application слоя: `Guid UserId { get; }`.
  Имплементацията в `Api` чете от `HttpContext.User`. **Domain не знае за HTTP.**
- Global query filter в `DbContext` за всяко user-owned ентити:
  `builder.HasQueryFilter(e => e.UserId == _currentUser.UserId);`
- **Юнит/интеграционен тест, който доказва, че потребител А не вижда данни на Б.**
  Това е най-важният тест за сигурност в проекта.
- Първоначален seed: един потребител от `.env` (`SEED_ADMIN_EMAIL`, `SEED_ADMIN_PASSWORD`).
  Само в Development. Паролата **никога** не влиза в git.
- Регистрация на нови потребители: изключена по подразбиране
  (`Auth:AllowRegistration = false`).

---

## 9. Frontend (Angular)

### 9.1 Структура

```
src/web/src/app/
├─ core/            # интерцептори, guard-ове, ICurrentUser, error handler
├─ shared/          # dumb компоненти, pipes, директиви
├─ features/
│  ├─ library/      # списък, детайл, добавяне/редакция
│  ├─ wishlist/
│  ├─ reading/      # сесии, прогрес
│  ├─ import/       # „пусни линк" екрана
│  ├─ shelves/
│  └─ statistics/
└─ app.routes.ts    # lazy loading на всеки feature
```

- **Standalone компоненти** навсякъде, без NgModule.
- **Signals** за локално състояние; `httpResource` / `toSignal` за сървърни данни
  (провери какво е актуалното в Angular 22 преди да избереш).
- **Typed reactive forms** (`FormGroup<...>`) — без `any`.
- `ChangeDetectionStrategy.OnPush` на всеки компонент. Без изключения.
- Smart/dumb разделение: feature компонентите правят HTTP, shared компонентите
  получават всичко през `input()` и излъчват през `output()`.
- Никакви ръчни `.subscribe()` без `takeUntilDestroyed()` — или изобщо не
  subscribe-вай, ползвай `async` pipe / signals.

### 9.2 Интернационализация (bg + en)

**Решение: runtime превод с Transloco** (`@jsverse/transloco`).
*Обосновка:* официалният `@angular/localize` прави отделен build за всеки език —
смяната на езика значи навигация към друг URL и презареждане. За лично
приложение с превключвател в хедъра runtime подходът е по-удобен.
*Компромис:* Transloco е библиотека на трета страна, не е част от Angular.
Ако предпочиташ само официални инструменти — `@angular/localize` е валидният
избор и планът не се чупи от това.

- Файлове: `src/assets/i18n/bg.json`, `src/assets/i18n/en.json`.
- **Нито един низ в шаблон не е hardcode-нат.** Ключове по feature: `library.add.title`.
- Език по подразбиране: `bg`. Избраният език се пази в `localStorage`.
- Дати, числа и валута — през Angular `DatePipe`/`CurrencyPipe` с активния locale;
  регистрирай `bg` locale данните.
- **Грешките от API-то идват като код**, не като текст. Фронтендът мапва
  `errorCode` → превод. Непознат код → общо съобщение + кодът в детайлите.
- Езикът на **съдържанието** (издание на български vs на английски) е съвсем
  отделно нещо от езика на интерфейса. Не ги смесвай.

---

## 10. Docker

`docker-compose.yml`:

```yaml
services:
  db:        # postgres:17-alpine, volume pgdata, healthcheck pg_isready
  api:       # build ./src, depends_on db (condition: service_healthy)
  web:       # nginx, сервира Angular build + proxy на /api → api:8080
volumes:
  pgdata:
  covers:    # монтира се в api контейнера за корици
```

- Всички тайни през `.env` (в `.gitignore`); `.env.example` се комитва с празни стойности.
- Multi-stage Dockerfile-и (SDK → runtime за .NET; node → nginx за Angular).
- Health endpoint-и: `/health/live`, `/health/ready` (`AspNetCore.HealthChecks` или вградените).
- Миграциите се пускат при старт на `api` **само в Development**;
  за production — отделна стъпка/скрипт. Никога `EnsureCreated()`.

---

## 11. Тестова стратегия

| Ниво | Какво | Инструменти |
|---|---|---|
| Domain | инварианти, преходи на състояния, `ProgressPoint` правилата, ISBN checksum | xUnit + FluentAssertions, **без мокове** |
| Application | use cases с мокнати портове | NSubstitute |
| Infrastructure | EF Core мапване и заявки срещу реален Postgres | Testcontainers |
| API | end-to-end през HTTP, вкл. auth изолацията | `WebApplicationFactory` + Testcontainers |
| Angular | компоненти и services | стандартният runner на Angular CLI за версията, която scaffold-ваш — провери с `ng test` |

**Минимални тестове, които трябва да съществуват преди Етап 6:**
1. Не мога да запиша `PercentProgress` за аудиокнига.
2. Препрочитане създава втора сесия, а не презаписва първата.
3. `WishlistEntry.Fulfill()` създава `LibraryItem` с `Acquisition` и затваря желанието.
4. Невалиден ISBN се отхвърля с `isbn.invalid`, не се записва.
5. Потребител А не вижда `LibraryItem` на потребител Б.
6. Ръчно редактирано поле оцелява повторен enrichment.

---

## 12. Етапи

### Етап 0 — Репо и скелет
```powershell
cd "D:\01. Work"
mkdir MyDigitalLibrary
cd MyDigitalLibrary
git init -b main
dotnet new gitignore
dotnet new sln -n MyDigitalLibrary
# ... проекти по структурата от т.2
gh auth status          # ако не си логнат: gh auth login
gh repo create MyDigitalLibrary --public --source=. --remote=origin --push
```
- `.gitignore` да покрива и `node_modules/`, `dist/`, `.env`, `*.user`.
- `README.md` с как се вдига локално.
- GitHub Actions: build + test на push.
- **Извън обхвата:** всякаква функционалност.

### Етап 1 — Домейн
Всички ентитети, value objects и инварианти от т.3, с юнит тестове.
Нищо персистентно, нищо HTTP.
**Извън обхвата:** EF Core, DTO-та, API.

### Етап 2 — Персистентност
`DbContext`, конфигурации, първа миграция, `docker compose up db api`.
Seed на минимални данни (2 work-а, 3 edition-а, 2 library item-а).
**Извън обхвата:** auth, фронтенд.

### Етап 3 — CRUD API
Ресурсите за `library-items`, `works`, `editions`, `wishlist`.
ProblemDetails, пагинация, валидация, OpenAPI.
**Извън обхвата:** импорт, четене, книжарници.

### Етап 4 — Auth
Identity, cookie auth, `ICurrentUser`, global query filters, тестът за изолация.
**Извън обхвата:** регистрация през UI, външни login провайдъри, роли.

### Етап 5 — Angular skeleton
Shell, рутиране, i18n (bg/en), login екран, списък + детайл + форма за книга.
**Извън обхвата:** статистики, графики, тъмна тема.

### Етап 6 — Импорт по линк / ISBN
`GenericIsbnPageResolver`, Open Library + Google Books, екран за преглед и
потвърждение, `ManualFieldOverrides`, сваляне на корици.
**Извън обхвата:** книжарници, CSV импорт, баркод скенер.

### Етап 7 — Четене
`ReadingSession`, прогрес по формат, рейтинги, ревюта, рафтове, бележки, цитати.
**Извън обхвата:** цели за четене, статистики.

### Етап 8 — Книжарници и наличност
`Bookstore`, `BookstoreListing`, адаптери с конфигурируеми селектори,
`AvailabilityRefreshService`, история на цените.
**Извън обхвата:** известия по имейл, сравнение на цени между магазини.

### Етап 9 — Останалото
CSV импорт от Goodreads/Calibre, експорт (JSON + CSV), full-text search
(PostgreSQL `tsvector` + GIN; за български най-вероятно `simple` конфигурация +
`unaccent` — Postgres няма вграден български речник, провери това преди да го
проектираш), статистики, цели за четене, заемане на книги, откриване на дубликати.

---

## 13. Неща, които изрично **не** правим сега

- Микросървиси. Един API проект е достатъчен.
- CQRS с отделна read база. Проекции с `Select` стигат.
- Event sourcing.
- MediatR. Извикай use case класа директно през DI.
- GraphQL.
- Kubernetes.
- Мобилно приложение.
- Социални функции (приятели, публични рафтове).
- Баркод скенер (добра идея, но след Етап 8).

Ако в някой момент планът започне да изисква нещо от този списък —
**спри и питай**, вероятно проектът се over-engineer-ва.

---

## 14. Отворени въпроси за собственика на проекта

1. Рейтинг скала: 1–5 звезди (като Goodreads) или 1–10? Реши преди Етап 3.
2. Аудиокниги — следим ли от кой доставчик са (Audible / Storytel / файл)?
3. Хартиените книги: следим ли състояние (ново / добро / износено)?
4. Нужна ли е история на ревютата (versioned), или последното презаписва?
5. Кои конкретни книжарници са приоритет за Етап 8?
