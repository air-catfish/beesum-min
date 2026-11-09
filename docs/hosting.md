# Hosting and mirrors

## Canonical source

- GitHub: https://github.com/air-catfish/beesum-min
- Codeberg mirror: https://codeberg.org/air-catfish/beesum-min

## Documentation site

This site is built with MkDocs Material and published from the `pages` branch on
Codeberg Pages:

```text
https://air-catfish.codeberg.page/beesum-min/
```

Build locally:

```powershell
python -m pip install -r requirements-docs.txt
python -m mkdocs build --clean
```

Publish the generated `artifacts/docs-site` directory to the `pages` branch.

## Codeberg Pages

1. Repository must be public.
2. Create a `pages` branch containing the built site.
3. Settings → Webhooks → Add webhook → **git-pages**.
4. Target: `https://air-catfish.codeberg.page/beesum-min/`
5. Branch filter: `pages`.

## Migration

The site is static. If Codeberg Pages ever goes away, copy the generated
directory to any static host (Netlify, Cloudflare Pages, nginx) and change
`site_url` in `mkdocs.yml`.
