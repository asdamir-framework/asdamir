---
name: asdamir-session-memory
description: Use at the END of every working session in this repo (project rule), and at the START to read prior decisions. Writes a dated, bilingual (English + Türkçe) summary to .claude/memory/. Trigger on session wrap-up, "save memory", "log this session", or before finishing meaningful work.
---

# Asdamir session memory (bilingual EN + TR)

Project rule (`CLAUDE.md` → "Session memory"): **at the end of every session — even routine ones — add a
dated, bilingual summary** to `.claude/memory/`. And **at the start of new work, read the relevant prior
entries** first.

## Write
Create `.claude/memory/YYYY-MM-DD-<slug>.md` with **parallel English and Türkçe** sections:

```markdown
# YYYY-MM-DD — <short title>

## English
### What was done
- …
### Key decisions
- … (decision + the WHY)
### Watch out
- … (gotchas, things not obvious from the code)

## Türkçe
### Ne yapıldı
- …
### Önemli kararlar
- … (karar + NEDEN)
### Dikkat
- …
```

- **Record the non-obvious**: decisions + their rationale, gotchas, "why it's done this way" — NOT what the
  code/git history already shows.
- **Link related entries** with `[[other-entry-name]]` (the filename without `.md`).
- One concern per entry; update an existing entry rather than duplicating it.
- Significant decisions (e.g. CI turned off, a publishing channel choice) deserve their own entry so future
  sessions inherit the context.

## Read (start of work)
`ls .claude/memory/` and skim entries relevant to your task before changing anything — they reflect what
was true when written, so **verify a named file/flag/command still exists** before acting on it.

## Note: two memory systems
- **`.claude/memory/`** — this project log (the CLAUDE.md rule above). This is what the skill writes.
- The personal **auto-memory** (`MEMORY.md` index under `~/.claude/projects/…/memory/`) is separate,
  cross-session, and one-fact-per-file — out of scope for this skill.

## DON'T
- **Don't skip the memory entry** at session end — the rule says every session, routine included.
- **Don't write English-only or Türkçe-only** — both, in parallel.
- **Don't dump code/diffs** — capture decisions, rationale, and watch-outs.
