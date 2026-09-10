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
