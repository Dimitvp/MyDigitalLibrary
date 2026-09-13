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

Ръчен backup/restore (докато стекът работи):

```powershell
docker compose exec db-backup sh -c 'pg_dump --format=custom --file="/backup/mydigitallibrary_$(date -u +%Y%m%dT%H%M%SZ).dump" "$POSTGRES_DB"'
docker compose exec db pg_restore --username "$POSTGRES_USER" --dbname "$POSTGRES_DB" --no-owner --clean --if-exists /backup/mydigitallibrary_<timestamp>.dump
```
