# Documentation Style Guide

Rules for writing and organising docs in this repository.

## Rendering

All documentation is Markdown, rendered in-app by **Markdig** via the `/docs` page. There is no MkDocs, DocFX, or external site — the in-app reader is the single source of truth.

## File Naming

- Use `UPPERCASE-WITH-DASHES.md` for filenames (e.g. `INSTALL-FEDORA.md`, `SECURITY.md`).
- Prefix with `NN-` when ordering matters (e.g. `01-GETTING-STARTED.md`, `02-CONFIGURATION.md`).
- `README.md` always sorts first in the sidebar — use it as the landing page for each docs folder.

## Structure

- Every file starts with an **H1 title** (`# Title`). Only one H1 per file.
- Use H2 (`##`) for major sections, H3 (`###`) for subsections.
- Keep pages focused on a single topic. Prefer many small files over one large file.

## Tone

- Concise and practical — write for someone who wants to get things done.
- Use imperative mood for instructions ("Run the script", not "You should run the script").
- Avoid filler phrases like "In order to" or "It should be noted that".

## Code Blocks

Use fenced code blocks with a language hint:

````markdown
```bash
sudo systemctl restart vpn-dashboard
```
````

Common language hints: `bash`, `csharp`, `json`, `xml`, `ini`, `sql`.

Inline code uses single backticks for file paths, commands, and identifiers: `appsettings.json`, `dotnet run`, `AppDbContext`.

## Links

- Link to other docs with relative paths: `[Security](SECURITY.md)`.
- For subdirectory docs, use folder-relative links: `[Getting Started](website/01-GETTING-STARTED.md)`.

## Tables

Use Markdown tables for structured data. Align columns for readability in source:

```markdown
| Setting       | Default | Description          |
|---------------|---------|----------------------|
| `ListenPort`  | `5000`  | HTTP listen port     |
```

## Images

Place images in a sibling `images/` folder and reference with relative paths:

```markdown
![Dashboard overview](images/dashboard-overview.png)
```
