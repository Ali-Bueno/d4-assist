# Claude Code setup

Per-machine Claude Code configuration that keeps mod work cheap. Not mod code: copy it into the user
config once per machine. Rationale and evidence: [token efficiency](../../reference/token-efficiency/README.md).

## `agents/Explore.md`

Overrides the built-in Explore subagent (which otherwise runs on the main model, e.g. Opus) with a
read-only Sonnet agent at low effort that returns `path:line` findings.

Install (applies to every repo on the machine):

```powershell
New-Item -ItemType Directory -Force "$HOME\.claude\agents" | Out-Null
Copy-Item "templates\claude-code\agents\Explore.md" "$HOME\.claude\agents\Explore.md"
```

A project `.claude/agents/Explore.md` would take precedence over the user copy. Edit the master here and
re-copy; start a new session for the change to load.
