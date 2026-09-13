# My Digital Library

Личен каталог за книги (хартиени / ebook / аудио) — какво притежавам, какво искам,
какво чета в момента. Виж пълния план за имплементация в [docs/PLAN.md](docs/PLAN.md).

> **Статус:** Етап 9 — Останалото. Домейн (Етап 1), EF Core/Postgres
> персистентност (Етап 2), REST API (Етап 3), ASP.NET Core Identity с cookie
> auth + global query filters (Етап 4), Angular shell/рутиране/i18n/login/
> библиотека (Етап 5), импорт по линк/ISBN през Open Library/Google Books
> (Етап 6), четене — читателски сесии/прогрес, рейтинги, ревюта, рафтове,
> бележки, цитати (Етап 7), наличност по книжарници — Helikon и Ciela,
> с ежедневно фоново обновяване (Етап 8) и Етап 9 — заемане на книги,
> цели за четене, експорт (JSON + CSV), статистики, откриване на дубликати,
> CSV импорт от Goodreads/Calibre и full-text search (PostgreSQL `tsvector`
> + GIN, `simple` конфигурация) — са готови. Планът (секции 0-12) е изпълнен
> изцяло; баркод скенер и nginx production hardening остават извън обхвата
> (виж бележката по-долу). След Етап 9 (виж
> [.claude/history](.claude/history/2026-09-12-1026-library-import-catalog-reading-wishlist-covers.md)):
> Angular екраните вече покриват и четене (старт/финал с дати), wishlist
> (списък + добавяне), категории по жанр/език и качване/смяна на корица на
> изданието — не само библиотеката/импорта от Етап 5-6.
> (виж `docs/PLAN.md` т. 13; nginx production build е скициран, но не
> е формален план-етап — виж бележката преди "## CI" по-долу).

## Стек

- Backend: ASP.NET Core Web API, .NET 10
- Frontend: Angular 22 (standalone components, signals)
- База: PostgreSQL 17 + EF Core
- Тестове: xUnit + FluentAssertions

## Структура на solution-а

```
src/
├─ MyDigitalLibrary.Domain/         # чист домейн, без зависимости
├─ MyDigitalLibrary.Application/    # use cases, портове, DTO-та
├─ MyDigitalLibrary.Infrastructure/ # EF Core, Identity, HTTP клиенти
├─ MyDigitalLibrary.Api/            # ASP.NET Core Web API
└─ web/                             # Angular workspace
tests/
├─ MyDigitalLibrary.Domain.Tests/
├─ MyDigitalLibrary.Application.Tests/
└─ MyDigitalLibrary.Api.IntegrationTests/
```

## Локално стартиране

### Изисквания

- [.NET SDK 10](https://dotnet.microsoft.com/download) (`dotnet --list-sdks`)
- [Node.js 24+](https://nodejs.org/) и npm (`node --version`)
- Angular CLI: `npx @angular/cli@latest version`

### Backend

```powershell
dotnet build
dotnet test
dotnet run --project src/MyDigitalLibrary.Api
```

`MyDigitalLibrary.Api.IntegrationTests` spins up a real Postgres container via
Testcontainers (plan section 1) — Docker must be running for `dotnet test` to
pass.

### Frontend

```powershell
cd src/web
npm install
npm start
```

`npm start` (`ng serve`) обслужва Angular dev server-а на `http://localhost:4201`
(портът е фиксиран в `angular.json` — `4200` е зает от друг локален Docker проект)
и прокси-ва `/api`/`/health`/`/covers` към `http://localhost:8081` (виж `proxy.conf.json`)
— затова API-то (`docker compose up -d` или `dotnet run`) трябва да работи
паралелно, за да работят вход/списък/детайли/добавяне в браузъра. Вход с
seed admin-а (`SEED_ADMIN_EMAIL`/`SEED_ADMIN_PASSWORD` от `.env`).

### Docker

```powershell
cp .env.example .env   # или ръчно създай .env със същите ключове
docker compose up --build -d
```

Вдига `db` (Postgres 17) и `api`; API-то прилага EF Core миграциите и seed-ва
минимални данни автоматично в Development. `docker-compose.override.yml`
публикува портове само локално (по подразбиране `5532` за Postgres и `8081`
за API — сменени от стандартните 5432/8080, ако вече имаш друг проект на тях).

- `GET http://localhost:8081/health/live`
- `GET http://localhost:8081/health/ready`
- `GET http://localhost:8081/openapi/v1.json` — OpenAPI документ (Development)

Вдига се и `db-backup` service (Postgres-alpine sidecar, виж
[db-backup/README.md](db-backup/README.md)) — пази `db-backup/mydigitallibrary.dump`
(host bind mount, **не** се качва в git) свеж чрез `pg_dump`: до ~5 мин.
след добавяне/редакция/трил на книга/издание (Postgres тригер + `LISTEN`/
`NOTIFY` на `works`/`editions`, дебаунснат) или поне веднъж седмично, ако
няма промени. При старт на `db` с празен `pgdata` volume (загубен/изтрит
volume, нов хардуер) `docker/db-init/10-restore-if-exists.sh` автоматично
възстановява от този файл, ако го намери — иначе старт с празна база.

API-то (виж [docs/PLAN.md](docs/PLAN.md) т. 4/4.1 за пълния контракт):

```text
POST        /api/v1/auth/login, /api/v1/auth/logout
GET         /api/v1/auth/me, /api/v1/auth/antiforgery
GET/POST    /api/v1/works, /api/v1/works/{id}, /api/v1/works/{id}/editions
PUT         /api/v1/works/{id}   # title/description/genreNames — resolve-or-create по име, като авторите
PUT         /api/v1/works/{id}/rating, /api/v1/works/{id}/review   # upsert; виж GET /works/{id} за myRating/myReview
GET         /api/v1/genres
GET/PUT     /api/v1/editions/{id}
POST        /api/v1/editions/{id}/cover   # multipart/form-data, поле "file" — качва/сменя корицата ръчно
GET/POST    /api/v1/library-items, /api/v1/library-items/{id}
PATCH       /api/v1/library-items/{id}/location, /status
GET/POST    /api/v1/wishlist, /api/v1/wishlist/{id}/fulfill
GET/POST    /api/v1/reading-sessions, /api/v1/reading-sessions/{id}
POST        /api/v1/reading-sessions/{id}/progress, /finish, /abandon
GET/POST    /api/v1/shelves, /api/v1/shelves/{id}, /api/v1/shelves/{id}/items
GET/POST    /api/v1/library-items/{id}/notes, PUT/DELETE /api/v1/notes/{id}
GET/POST    /api/v1/works/{id}/quotes, PUT/DELETE /api/v1/quotes/{id}
POST        /api/v1/import/lookup   # { url } или { isbn } → кандидат за преглед, нищо не се записва
GET/POST    /api/v1/editions/{id}/listings
PATCH       /api/v1/editions/{id}/listings/{listingId}/discontinued
POST        /api/v1/library-items/{id}/loans
POST        /api/v1/loans/{id}/return
GET/PUT     /api/v1/reading-goals/{year}
GET         /api/v1/export?format=json|csv
GET         /api/v1/statistics?year=
GET         /api/v1/duplicates
POST        /api/v1/import/csv   # multipart/form-data, поле "file" — авто-детекция Goodreads/Calibre
GET         /api/v1/import/jobs/{id}
GET         /api/v1/search?q=...   # PostgreSQL full-text, "simple" конфигурация; заглавие/оригинално заглавие/описание + автори
```

Всички ресурсни endpoints изискват вход (cookie auth); мутиращите заявки
(POST/PUT/PATCH/DELETE) изискват и anti-forgery token в header `X-XSRF-TOKEN`,
получен от `GET /api/v1/auth/antiforgery`. Регистрация през UI изключена
(`Auth:AllowRegistration = false`) — единственият потребител е seed admin-ът
от `SEED_ADMIN_EMAIL`/`SEED_ADMIN_PASSWORD` (Development-only, виж `.env.example`).

Импортът пита Open Library (без ключ) и Google Books (ключ по избор,
`GoogleBooks:ApiKey` — анонимните заявки делят обща квота, която може да е
изчерпана; при провал на доставчик просто липсва от резултата, не гърми
заявката). Свалените корици отиват в `Covers:RootDirectory` (Docker: volume,
виж по-долу) и се сервират през `GET /covers/{файл}` — свалянето е background
job, не блокира записа на книгата.

Наличността по книжарници (`Bookstores` в `appsettings.json`) проверява
Helikon и Ciela веднъж дневно (`Bookstores:RefreshInterval`) — и двете имат
permissive `robots.txt` и публикуват schema.org `Book`/`Offer` JSON-LD.
Ozone.bg и Orange Center **нямат** адаптери — Ozone блокира бот трафик на
мрежово ниво, Orange Center забранява продуктовите страници в `robots.txt`
(виж `docs/PLAN.md` т. 6.1). `Bookstores:Enabled=false` спира всичко;
ръчно добавен линк към книжарница работи винаги, дори без съвпадащ адаптер.

Angular dev server-ът (виж Frontend по-горе) вече работи срещу това API.
Production build зад nginx в самия `docker compose` стек идва в Етап 10.

## CI

GitHub Actions (`.github/workflows/ci.yml`) пуска `dotnet build` + `dotnet test`
и `ng build` + `ng test` при всеки push / pull request към `main`.
