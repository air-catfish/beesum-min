;;; beesum.el --- minimal beesum-min integration -*- lexical-binding: t; -*-

;; Load this file, then:
;;   M-x beesum-min-url RET <url>
;;   M-x beesum-min-region   (with an active region)

(defgroup beesum-min nil
  "Summarize YouTube transcripts with beesum-min."
  :group 'tools)

(defcustom beesum-min-command "beesum-min"
  "Command used to invoke beesum-min."
  :type 'string
  :group 'beesum-min)

(defun beesum-min--show (buffer)
  "Show BUFFER in special mode."
  (with-current-buffer buffer
    (goto-char (point-min))
    (special-mode))
  (display-buffer buffer))

(defun beesum-min-url (url)
  "Summarize URL and show the result."
  (interactive "sYouTube URL: ")
  (let ((buffer (get-buffer-create "*beesum-min*")))
    (with-current-buffer buffer (erase-buffer))
    (shell-command (concat beesum-min-command " " (shell-quote-argument url)) buffer)
    (beesum-min--show buffer)))

(defun beesum-min-region (start end)
  "Summarize the region as plain text and show the result."
  (interactive "r")
  (let ((buffer (get-buffer-create "*beesum-min*")))
    (with-current-buffer buffer (erase-buffer))
    (shell-command-on-region start end
                             (concat beesum-min-command " --stdin --input text")
                             buffer)
    (beesum-min--show buffer)))

(provide 'beesum-min)
;;; beesum.el ends here
