# Subtitle languages

`beesum-min` asks yt-dlp for subtitles in the languages you name:

```powershell
beesum-min "..." --sub-langs en
beesum-min "..." --sub-langs ru,en
beesum-min "..." --sub-langs ru --proxy socks5://127.0.0.1:1080
```

The default is `en`. Manual subtitles are preferred by yt-dlp; auto-generated
captions are accepted when there is nothing better.

## Selection

When yt-dlp writes several `.vtt` files, the first language in your list wins:

1. `ru,en` → `abc.ru.vtt`, fallback to `abc.en.vtt`;
2. `en,ru` → `abc.en.vtt`, fallback to `abc.ru.vtt`;
3. `en.*` → a file whose tag is `en` or starts with `en-` (for example `en-US`).

If none of the requested languages is present, the first downloaded file is used.
That fallback is deliberate: a transcript in the wrong language is still better
than a failed run, and the JSON/Language field tells you what you got.

## Why not `en.*` by default

YouTube exposes dozens of auto-translated variants (`en-ar`, `en-de`, ...).
`en.*` matches all of them, turns one subtitle request into a burst, and gets rate
limited with HTTP 429. Concrete codes (`en`, `ru`, `de`) are cheaper and more
predictable.

## Translation

This is subtitle selection, not translation. `--sub-langs ru` downloads Russian
subtitles if the video has them; it does not translate an English transcript.
Prompt-based translation is a separate feature.

## Library

```csharp
services.AddBeesum(options =>
{
    options.SubtitleLanguages.Clear();
    options.SubtitleLanguages.Add("ru");
    options.SubtitleLanguages.Add("en");
});
```
