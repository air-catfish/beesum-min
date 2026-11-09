" Minimal Vim integration for beesum-min.
" Requires beesum-min in $PATH. No plugins, no Vim9, no async magic.

if exists('g:loaded_beesum_min')
  finish
endif
let g:loaded_beesum_min = 1

let g:beesum_min_command = get(g:, 'beesum_min_command', 'beesum-min')
let g:beesum_min_arguments = get(g:, 'beesum_min_arguments', '')

function! s:Invoke(arguments, input) abort
  let l:command = join(filter([g:beesum_min_command, g:beesum_min_arguments, a:arguments], '!empty(v:val)'), ' ')
  let l:output = empty(a:input) ? system(l:command) : system(l:command, a:input)

  if v:shell_error
    echohl ErrorMsg
    echomsg substitute(l:output, '\n', ' ', 'g')
    echohl None
    return
  endif

  new
  setlocal buftype=nofile bufhidden=wipe noswapfile
  call setline(1, split(l:output, "\n"))
endfunction

command! -nargs=+ BeesumUrl call s:Invoke(shellescape(<q-args>), '')
command! -range BeesumSelection call s:Invoke('--stdin --input text', join(getline(<line1>, <line2>), "\n"))
command! BeesumBuffer call s:Invoke('--stdin --input text', join(getline(1, '$'), "\n"))
