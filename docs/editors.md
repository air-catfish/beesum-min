# Editors and pipes

`beesum-min` reads a transcript from stdin, so you do not need a plugin to use it
from an editor. Progress goes to stderr; stdout is the summary (or JSON).

## Shell pipes

```powershell
# Already downloaded subtitles
Get-Content .\captions.vtt | beesum-min --stdin --input vtt

# Any plain text: a transcript, an article, a meeting note
Get-Content .\transcript.txt | beesum-min --stdin --input text --format markdown

# JSON for scripts and editor plugins
Get-Content .\transcript.txt | beesum-min --stdin --json
```

`--input` defaults to `text`, so `--stdin` alone is enough for plain text.

## Clips and saves

Clip by transcript time and keep both files:

```powershell
beesum-min "https://www.youtube.com/watch?v=..." `
  --from 00:12:34 --to 00:18:00 --timestamps `
  --save-vtt clip.vtt --save-transcript clip.md `
  -o notes.md --no-save-prompt
```

* `--from` / `--to` accept `ss`, `mm:ss`, or `hh:mm:ss` and slice the transcript,
  not the video. The raw VTT stays complete.
* `--save-vtt` keeps the downloaded/local VTT; `--save-transcript` writes the
  clipped text or summary; `-o` writes the notes/summary file.
* Without the save flags, an interactive terminal asks **once per file**:
  `Save transcript? [y/N]` and `Save subtitles (VTT)? [y/N]`, then the path.
  `--no-save-prompt` disables the questions. Piped, JSON, batch, and stdin runs
  never prompt, so scripts cannot hang.

Paths are explicit; nothing is written into a hidden library automatically. Create
a folder and pass a path when you want one:

```powershell
--save-transcript notes\caching.md --save-vtt notes\caching.vtt
```

From Vim/Neovim, insert a clip below the cursor without replacing the buffer:

```vim
:r !beesum-min "https://www.youtube.com/watch?v=..." --from 00:12:34 --to 00:18:00 --timestamps --no-save-prompt
```
## Vim / Neovim

No plugin is required; the built-in filter is enough:

```vim
" Replace the current buffer with a summary (undo with u)
:%!beesum-min --stdin --input text

" Replace the selected lines
:'<,'>!beesum-min --stdin --input text
```

For a scratch buffer instead of replacing text, load
[`contrib/vim/beesum.vim`](https://codeberg.org/air-catfish/beesum-min/src/branch/main/contrib/vim/beesum.vim) (copy it to
`~/.vim/plugin/`, or use a plugin manager) and:

```vim
:BeesumUrl https://www.youtube.com/watch?v=...
:'<,'>BeesumSelection
:BeesumBuffer
```

If the binary is not `beesum-min`, set `g:beesum_min_command`. Extra flags go to
`g:beesum_min_arguments`.

Neovim:

```lua
vim.opt.runtimepath:append("/path/to/beesum-min/contrib/vim")
```

## Emacs

Load [`contrib/emacs/beesum.el`](https://codeberg.org/air-catfish/beesum-min/src/branch/main/contrib/emacs/beesum.el):

```elisp
(load-file "/path/to/beesum-min/contrib/emacs/beesum.el")
(global-set-key (kbd "C-c b u") #'beesum-min-url)
(global-set-key (kbd "C-c b r") #'beesum-min-region)
```

`M-x beesum-min-url` prompts for a URL; `M-x beesum-min-region` summarizes the
active region. Results open in `*beesum-min*` in `special-mode`.

## Notes

* `--json` is the stable interface for plugins; parse `content` and ignore
  progress on stderr.
* On Windows, a global tool lives in `%USERPROFILE%\.dotnet\tools`; make sure that
  directory is in `PATH`, or set `g:beesum_min_command` to the full path.
* Editors usually add a trailing newline; it ends up in the transcript. Harmless.
