# My Digital Library

Личен каталог за книги (хартиени / ebook / аудио) — какво притежавам, какво искам,
какво чета в момента. Виж пълния план за имплементация в [docs/PLAN.md](docs/PLAN.md).

> **Статус:** Етап 3 — CRUD API. Домейн (Етап 1), EF Core/Postgres персистентност
> (Етап 2) и REST API за `works`/`editions`/`library-items`/`wishlist` (Етап 3)
> са готови. Auth и фронтенд идват в следващите етапи.

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

### Frontend

```powershell
cd src/web
npm install
npm start
```

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
GET/POST    /api/v1/works, /api/v1/works/{id}, /api/v1/works/{id}/editions
GET/PUT     /api/v1/editions/{id}
GET/POST    /api/v1/library-items, /api/v1/library-items/{id}
PATCH       /api/v1/library-items/{id}/location, /status
GET/POST    /api/v1/wishlist, /api/v1/wishlist/{id}/fulfill
```

Няма `auth` още (Етап 4) — всички заявки се третират като един фиксиран
dev потребител.

`web` (Angular, зад nginx) идва в Етап 10.

## CI

GitHub Actions (`.github/workflows/ci.yml`) пуска `dotnet build` + `dotnet test`
и `ng build` + `ng test` при всеки push / pull request към `main`.
