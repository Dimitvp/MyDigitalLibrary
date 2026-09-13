# db-backup/

Локална папка за backup на PostgreSQL базата — **не** се следи от git (виж
`.gitignore`), с изключение на този файл.

- `mydigitallibrary.dump` се пише автоматично от `db-backup` service-а в
  `docker-compose.yml` (custom-format `pg_dump`) — веднъж седмично или до
  ~5 мин. след промяна в `works`/`editions` (добавяне/редакция/трил на
  книга/издание), кой​то от двата случая настъпи първи.
- При старт на `db` service-а с **празен** `pgdata` volume (нов checkout,
  изтрит volume, нов хардуер) — `docker/db-init/10-restore-if-exists.sh`
  автоматично възстановява базата от този файл, ако го намери; ако файлът
  липсва, стартира с празна база.
- Възстановяване работи само при **първо** стартиране на празен volume
  (postgres `docker-entrypoint-initdb.d` конвенция) — не презаписва
  съществуващи данни.

Ръчен backup/restore (докато стекът работи):

```powershell
docker compose exec db-backup sh -c 'pg_dump --format=custom --file=/backup/mydigitallibrary.dump "$POSTGRES_DB"'
docker compose exec db pg_restore --username "$POSTGRES_USER" --dbname "$POSTGRES_DB" --no-owner --clean --if-exists /backup/mydigitallibrary.dump
```
