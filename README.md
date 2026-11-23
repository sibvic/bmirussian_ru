# bmirussian_ru
Новая версия сайта BMIRussian

## Разработка

Для доступа к приватным пакетам из GitHub Packages (sibvic) установите переменные окружения:
- `GITHUB_USERNAME` — ваш GitHub логин
- `GITHUB_TOKEN` — Personal Access Token с правом `read:packages`

## Импорт видео по URL (yt-dlp)

Если Kafka (`DownloaderKafka`) не настроена, импорт видео в админке получает метаданные
через локальный `yt-dlp`. На сервере его нужно установить:

```sh
sudo sh scripts/install_yt_dlp.sh
```

Если `yt-dlp` установлен не в `PATH`, укажите полный путь в настройке `VideoImport:YtDlpPath`.

## Kafka

Сервер, логин и пароль Kafka задаются один раз в секции `Kafka`
(`BootstrapServers`, `Username`, `Password`) — её значения используются
секциями `KafkaMessageSender`, `DownloaderKafka` и `ShopKafka`, в которых
остаются только топики. При необходимости внутри этих секций можно
переопределить подключение для конкретного консьюмера/продюсера.

## Магазин 3D-моделей (STL-генерация)

Страница `/Shop` позволяет залогиненным пользователям генерировать STL-модели
(пока только `key_tag` — метка на ключи с гравировкой произвольного текста).

Архитектура:

- Веб-приложение отправляет задание в Kafka топик `stl-generate`
  (настройки `ShopKafka`) и ждёт результата в `stl-generate-result`.
- Отдельный воркер `StlWorker/` (консольное приложение) забирает задания,
  рисует текст в PNG (шрифт Danger 96pt, `Assets/Fonts/Danger.ttf`),
  прогоняет `Assets/3d/key_tag.scad` через OpenSCAD, загружает STL в MinIO
  (`shop/{requestId}.stl`) и публикует результат.
- Скачивание идёт через веб-приложение (стримит объект из MinIO).

Требования на машине воркера:

```sh
sudo sh scripts/install_openscad.sh
```

Если `openscad` не в `PATH`, укажите полный путь в `Generator:OpenScadPath`
(на Windows — `C:/Program Files/OpenSCAD/openscad.com`).

Деплой воркера на сервере — по аналогии с основным сайтом: `dotnet publish`,
выкладка в `/var/bmirussian_stl_worker`, systemd unit `scripts/bmirussian_stl_worker.service`.
При деплое через docker-compose добавьте сервис из `StlWorker/Dockerfile`
(см. секцию `stl_worker` в корневом docker-compose.yml).
