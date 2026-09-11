# My Digital Library

Личен каталог за книги (хартиени / ebook / аудио) — какво притежавам, какво искам,
какво чета в момента. Виж пълния план за имплементация в [docs/PLAN.md](docs/PLAN.md).

> **Статус:** Етап 6 — Импорт по линк/ISBN. Домейн (Етап 1), EF Core/Postgres
> персистентност (Етап 2), REST API (Етап 3), ASP.NET Core Identity с cookie
> auth + global query filters (Етап 4), Angular shell/рутиране/i18n/login/
> библиотека (Етап 5) и импорт по линк/ISBN през Open Library/Google Books с
> преглед-и-потвърждение екран (Етап 6) са готови. Книжарници, CSV импорт и
> баркод скенер идват в по-късен етап.

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

`npm start` (`ng serve`) обслужва Angular dev server-а на `http://localhost:4200`
и прокси-ва `/api`/`/health` към `http://localhost:8081` (виж `proxy.conf.json`)
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

API-то (виж [docs/PLAN.md](docs/PLAN.md) т. 4/4.1 за пълния контракт):

```text
POST        /api/v1/auth/login, /api/v1/auth/logout
GET         /api/v1/auth/me, /api/v1/auth/antiforgery
GET/POST    /api/v1/works, /api/v1/works/{id}, /api/v1/works/{id}/editions
GET/PUT     /api/v1/editions/{id}
GET/POST    /api/v1/library-items, /api/v1/library-items/{id}
PATCH       /api/v1/library-items/{id}/location, /status
GET/POST    /api/v1/wishlist, /api/v1/wishlist/{id}/fulfill
POST        /api/v1/import/lookup   # { url } или { isbn } → кандидат за преглед, нищо не се записва
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

Angular dev server-ът (виж Frontend по-горе) вече работи срещу това API.
Production build зад nginx в самия `docker compose` стек идва в Етап 10.

## CI

GitHub Actions (`.github/workflows/ci.yml`) пуска `dotnet build` + `dotnet test`
и `ng build` + `ng test` при всеки push / pull request към `main`.
