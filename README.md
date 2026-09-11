# My Digital Library

Личен каталог за книги (хартиени / ebook / аудио) — какво притежавам, какво искам,
какво чета в момента. Виж пълния план за имплементация в [docs/PLAN.md](docs/PLAN.md).

> **Статус:** Етап 0 — репо и скелет. Домейн логика, персистентност, API и
> Docker Compose ще бъдат добавени в следващите етапи.

## Стек

- Backend: ASP.NET Core Web API, .NET 10
- Frontend: Angular 22 (standalone components, signals)
- База: PostgreSQL 17 + EF Core (от Етап 2)
- Тестове: xUnit

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

Docker Compose (`db`, `api`, `web`) ще бъде добавен в Етап 2 / Етап 10 на плана.

## CI

GitHub Actions (`.github/workflows/ci.yml`) пуска `dotnet build` + `dotnet test`
и `ng build` + `ng test` при всеки push / pull request към `main`.
