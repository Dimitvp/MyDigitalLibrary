# db-backup/

Локална папка за backup на PostgreSQL базата — **не** се следи от git (виж
`.gitignore`), с изключение на този файл.

- Всеки backup се пише в **свой собствен файл**,
  `mydigitallibrary_<UTC timestamp>.dump` (напр.
  `mydigitallibrary_20260913T083920Z.dump`) — нищо никога не се
  презаписва, всяка моментна снимка на базата си остава достъпна отделно.
- Пише се автоматично от `db-backup` service-а в `docker-compose.yml`
  (custom-format `pg_dump`) — до ~5 мин. след промяна в която и да е
  таблица с реални данни (books/editions/authors/genres/library items/
  reading sessions/wishlist/shelves/notes/quotes/reviews/ratings/loans/
  reading goals/series/bookstore listings/import jobs — пълният списък е в
  `docker/db-backup/backup.sh`), или поне веднъж седмично, което от двете
  настъпи първо. Промяна засечена по време на "прозореца" от 5 мин. след
  предходен backup не се пропуска — изчаква се прозорецът да мине и тогава
  се прави backup, вместо тихо да се забрави (както в по-старата версия на
  скрипта).
- При старт на `db` service-а с **празен** `pgdata` volume (нов checkout,
  изтрит volume, нов хардуер) — `docker/db-init/10-restore-if-exists.sh`
  автоматично възстановява базата от **най-новия** `.dump` файл в тази
  папка (сортирано по име — timestamp форматът се сортира хронологично),
  ако намери такъв; ако папката е празна, стартира с празна база.
- Възстановяване работи само при **първо** стартиране на празен volume
  (postgres `docker-entrypoint-initdb.d` конвенция) — не презаписва
  съществуващи данни.
- Изчистване на стари backup-и: **първо** се проверява броят файлове, чак
  **после** възрастта им — така папката никога не може да бъде изтрита
  почти напълно наведнъж. Ако файловете са 10 или по-малко, нищо не се
  трие, колкото и стари да са. Ако са повече от 10, най-новите 10 се пазят
  без значение от възрастта им; само сред по-старите (извън тези 10) се
  трият файловете на повече от ~6 месеца. Винаги остават поне 10 backup-а
  на диска.

## Off-site копие от production (biblioteka.svara.bg)

От 2026-09-17 базата на production сървъра (Hetzner) е каноничният
източник — вижте `.claude/history/2026-09-17-1700-local-to-production-data-migration.md`.
Production си има собствен `db-backup` sidecar (същия механизъм), но
пази backup-ите на **същия диск** като живата база — Hetzner-ският weekly
VM snapshot покрива и това, но не е истинско off-site копие (все още е
"вътре" в Hetzner).

`pull-from-production.ps1` тегли най-новия production `.dump` в тази
папка (пропуска изтеглянето, ако вече го има локално по име). Регистриран
е като Windows Scheduled Task (`MyDigitalLibrary-PullProductionBackup`,
ежедневно, само докато сте logged in — за да има достъп до SSH ключа):

```powershell
Get-ScheduledTask -TaskName "MyDigitalLibrary-PullProductionBackup"
Start-ScheduledTask -TaskName "MyDigitalLibrary-PullProductionBackup"  # ръчно изпълнение
```

Понеже файловете кацат в тази същата папка и `10-restore-if-exists.sh`
винаги възстановява от **най-новия** `.dump` (сортирано по timestamp),
изтеглено production копие автоматично става това, от което би се
възстановила локалната база, ако `pgdata` volume-ът някога тръгне
празен — без нужда от отделна restore логика.

Ако пренасяш проекта на нова машина, task-ът трябва да се регистрира
наново там (`Register-ScheduledTask`, вижте историята на сесията за
точната команда) — самият `.ps1` е в git, но Task Scheduler конфигурацията
е локална за тази машина.

Ръчен backup/restore (докато стекът работи):

```powershell
docker compose exec db-backup sh -c 'pg_dump --format=custom --file="/backup/mydigitallibrary_$(date -u +%Y%m%dT%H%M%SZ).dump" "$POSTGRES_DB"'
docker compose exec db pg_restore --username "$POSTGRES_USER" --dbname "$POSTGRES_DB" --no-owner --clean --if-exists /backup/mydigitallibrary_<timestamp>.dump
```
