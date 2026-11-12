using Beesum.Core.Localization;

namespace Beesum.Min.Cli;

/// <summary>Localized help text.</summary>
public static class HelpText
{
    public static string Text => AppLanguage.Current == "ru" ? Russian : English;

    internal const string English = """
        Usage: beesum-min <url> [options]
               beesum-min --local-vtt <path> [options]

        Options:
          --style <historian|programmer|neutral>   Summary style (default: neutral)
          --format <markdown|plaintext|mindmap>    Output format (default: plaintext)
          --provider <id>                          transcript-only | ollama | openai | anthropic | gemini
          --local-vtt <path>                       Use a local .vtt instead of downloading
          -o, --output <path>                      Write the result to a file
          --json                                   Emit machine-readable JSON
          --instructions "<text>"                  Extra instructions for the summary
          --prompt-file <path>                     Full system prompt from a file
          --stdin                                  Read the transcript from stdin
          --input <text|vtt>                       Format of stdin (default: text)
          --batch <path>                           File with one URL per line
          --jsonl                                  Batch output as JSON Lines
          --yt-dlp <path>                          Override the yt-dlp executable path
          --proxy <url>                            Proxy for yt-dlp
          --sub-langs <list>                       Subtitle languages, e.g. en or ru,en (default: en)
          --timestamps                             Add [hh:mm:ss] cue times to the output and prompt
          --from <time>                            Clip start: ss, mm:ss, or hh:mm:ss
          --to <time>                              Clip end: ss, mm:ss, or hh:mm:ss
          --save-vtt <path>                        Save the full downloaded/local VTT
          --save-transcript <path>                 Save the clipped transcript or summary
          --no-save-prompt                         Do not ask where to save files

          --cookies-file <path>                    Netscape cookies.txt file
          --js-runtime <name[:path]>               JS runtime for YouTube challenges
          --yt-dlp-args "<args>"                   Extra args for every yt-dlp call
          --ollama-url <url>                       Ollama base URL (default: http://localhost:11434)
          --ollama-model <name>                    Ollama model (default: llama3.2)
          --api-url <url>                          Base URL for the selected API provider
          --api-model <name>                       Model for the selected API provider
          --api-key-env <VAR>                      Env var with the key (default: OPENAI_API_KEY,
                                                   ANTHROPIC_API_KEY, GEMINI_API_KEY)
          --max-tokens <n>                         Max output tokens (default: 1024)
          --no-chunk                               Do not split long transcripts
          --chunk-tokens <n>                       Approx input tokens per chunk (default: 6000)
          --version                                Print version
          -h, --help                               Show this help

        Doctor:
          beesum-min doctor [--json] [--check-updates]
          --check-updates                          Check the CLI and yt-dlp for newer versions
          --cookies-from-browser <name>            Cookies from a browser profile
        Environment:
          BEESUM_LANG=en|ru                        UI language (default: system, English fallback)
          BEESUM_OLLAMA_NUM_CTX=<n>                Ollama context window in tokens (default: 8192)
          API keys are read from environment variables, never from arguments.
          yt-dlp is looked up at ./tools/yt-dlp[.exe] next to the executable;
          override with --yt-dlp.

        API examples:
          beesum-min URL --provider openai
          beesum-min URL --provider openai --api-url https://openrouter.ai/api --api-model anthropic/claude-sonnet-4-5
          beesum-min URL --provider anthropic
          beesum-min URL --provider gemini --api-model gemini-2.5-flash

        Examples:
          beesum-min https://www.youtube.com/watch?v=abc
          beesum-min https://www.youtube.com/watch?v=abc --provider ollama --style programmer
          beesum-min https://www.youtube.com/watch?v=abc --proxy socks5://127.0.0.1:1080
          beesum-min https://www.youtube.com/watch?v=abc --cookies-from-browser firefox
          beesum-min --local-vtt ./captions.vtt --format markdown -o summary.md
          beesum-min --local-vtt ./captions.vtt --json
        """;

    internal const string Russian = """
        Использование: beesum-min <url> [опции]
                       beesum-min --local-vtt <путь> [опции]

        Опции:
          --style <historian|programmer|neutral>   Стиль пересказа (по умолчанию: neutral)
          --format <markdown|plaintext|mindmap>    Формат вывода (по умолчанию: plaintext)
          --provider <id>                          transcript-only | ollama | openai | anthropic | gemini
          --local-vtt <путь>                       Использовать локальный .vtt вместо скачивания
          -o, --output <путь>                      Записать результат в файл
          --json                                   Вывод в JSON
          --instructions "<текст>"                 Доп. инструкции к пересказу
          --prompt-file <путь>                     Полный system prompt из файла
          --stdin                                  Читать транскрипт из stdin
          --input <text|vtt>                       Формат stdin (по умолчанию: text)
          --batch <путь>                           Файл с URL, по одному на строку
          --jsonl                                  Batch-вывод в формате JSON Lines
          --yt-dlp <путь>                          Указать путь к yt-dlp
          --proxy <url>                            Прокси для yt-dlp
          --sub-langs <list>                       Языки субтитров, например en или ru,en (по умолчанию: en)
          --timestamps                             Добавить [hh:mm:ss] к выводу и промпту
          --from <time>                            Начало фрагмента: ss, mm:ss или hh:mm:ss
          --to <time>                              Конец фрагмента: ss, mm:ss или hh:mm:ss
          --save-vtt <path>                        Сохранить полный VTT
          --save-transcript <path>                 Сохранить фрагмент транскрипта или конспект
          --no-save-prompt                         Не спрашивать, куда сохранять файлы

          --cookies-file <путь>                    Использовать cookies.txt (Netscape)
          --js-runtime <имя[:путь]>                JS-рантайм для YouTube (например, deno)
          --yt-dlp-args "<аргументы>"              Доп. аргументы для всех вызовов yt-dlp
          --ollama-url <url>                       Базовый URL Ollama (по умолчанию: http://localhost:11434)
          --ollama-model <имя>                     Имя модели Ollama (по умолчанию: llama3.2)
          --api-url <url>                          Базовый URL выбранного API-провайдера
          --api-model <имя>                        Модель выбранного API-провайдера
          --api-key-env <VAR>                      Переменная окружения с ключом (по умолчанию:
                                                   OPENAI_API_KEY, ANTHROPIC_API_KEY, GEMINI_API_KEY)
          --max-tokens <n>                         Максимум выходных токенов (по умолчанию: 1024)
          --no-chunk                               Не разбивать длинный транскрипт
          --chunk-tokens <n>                       Примерно токенов на чанк (по умолчанию: 6000)
          --version                                Показать версию
          -h, --help                               Показать эту справку

        Диагностика:
          beesum-min doctor [--json] [--check-updates]
          --check-updates                          Проверить новые версии CLI и yt-dlp
          --cookies-from-browser <браузер>         Взять cookies из профиля браузера
        Окружение:
          BEESUM_LANG=en|ru                        Язык интерфейса (по умолчанию: системный)
          BEESUM_OLLAMA_NUM_CTX=<n>                Контекст Ollama в токенах (по умолчанию: 8192)
          API-ключи читаются только из переменных окружения, не из аргументов.
          yt-dlp ищется в ./tools/yt-dlp[.exe] рядом с приложением;
          путь можно переопределить через --yt-dlp.

        Примеры API:
          beesum-min URL --provider openai
          beesum-min URL --provider openai --api-url https://openrouter.ai/api --api-model anthropic/claude-sonnet-4-5
          beesum-min URL --provider anthropic
          beesum-min URL --provider gemini --api-model gemini-2.5-flash

        Примеры:
          beesum-min https://www.youtube.com/watch?v=abc
          beesum-min https://www.youtube.com/watch?v=abc --provider ollama --style programmer
          beesum-min https://www.youtube.com/watch?v=abc --proxy socks5://127.0.0.1:1080
          beesum-min https://www.youtube.com/watch?v=abc --cookies-from-browser firefox
          beesum-min --local-vtt ./captions.vtt --format markdown -o summary.md
          beesum-min --local-vtt ./captions.vtt --json
        """;
}
