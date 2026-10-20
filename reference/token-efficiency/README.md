# Token efficiency

> How we keep Claude Code usage low on mod work, and which code-navigation tools we evaluated and
> rejected. The working rules live in [CLAUDE.md §12](../../CLAUDE.md#token-efficiency); this doc keeps
> the evidence so the question isn't re-researched. Evaluated 2026-10-12 on Claude Code 2.1.296;
> limits, subagent and settings findings added 2026-10-15.

## How Max plan limits are counted

- A rolling **5-hour** limit and a **weekly** limit, shared between claude.ai and Claude Code; the weekly
  one is what the maintainer hits on Max 5x. **Fable has its own separate weekly cap** (and may need usage
  credits). Hitting the general session or weekly limit blocks every model.
- Subagent tokens count against the same limits. The relative weight of an Opus vs a Sonnet token and of
  a cache read against the plan limit are **not published** — only that a one-line question in a long
  session "still draws usage for the whole conversation".

## What actually drives usage

- **Context size, every turn.** Each message re-sends the whole conversation. Cached reads are cheaper
  but still count against the plan limit, so a session left to grow costs more on every turn.
- **The 1M window.** Opus 4.7+ (incl. 5.5), Sonnet 5+, Haiku 5.5 and Fable run with a native 1M-token
  window and **don't auto-compact until ~967K** by default, so an uncapped long session can re-send close
  to a million tokens per message. See *Settings* below.
- **Tool output.** Build logs, decompiler output, broad grep results and whole-file reads land in
  context and are then re-sent with every later message.
- **Always-loaded instructions.** `CLAUDE.md` files ride on every request **and every subagent**;
  Anthropic recommends keeping them under ~200 lines and moving detail into docs or skills that load on
  demand.
- **Thinking.** Billed as output and not disableable on current models; the effort level (`/effort`) is
  the only dial. Effort is a per-user choice, not a playbook rule.
- **Subagents.** They keep bulk reading out of the main context but **do not reduce total usage**: each
  starts with a cold cache (5-minute TTL; the main thread gets 1 hour), loads the full `CLAUDE.md`
  hierarchy (except the built-in Explore and Plan), and runs on the main model unless given one. Anthropic
  measured multi-agent systems at ~15× the tokens of a chat (single agents ~4×). They pay off only for
  bulk output that would otherwise stay in the main context, or for work that can run on a cheaper model.
- **Cache busting.** Switching model mid-session (including `opusplan` plan/execute toggles), fast mode,
  adding/removing plugins or MCP servers, compaction, and resuming after more than an hour idle each cost
  one full-price turn.

## Practices we use

1. **Short sessions.** Resume from `STATUS.md` and `/clear` between unrelated tasks; the ledger makes a
   fresh session cheap.
2. **Code map in `STATUS.md`.** A short "where things are" table (entry point, speech sink, hooks, key
   game classes in the decompilation) replaces exploration at session start —
   [status template](../_mod-status-template.md).
3. **Plain, narrow search.** `Grep` with `files_with_matches` / `head_limit` / `glob`, `Read` with line
   ranges. Broad sweeps (decompiled dumps, unfamiliar subsystems) go to an Explore subagent that returns
   `path:line + conclusion`.
4. **Cheap Explore.** [`templates/claude-code/agents/Explore.md`](../../templates/claude-code/agents/Explore.md)
   overrides the built-in Explore with a Sonnet, low-effort, read-only agent; install it once per machine
   in `~/.claude/agents/`. Without it, pass `model: 'sonnet'` when delegating searches.
5. **Filter bulk output** before it reaches context (build errors only, filtered logs) —
   [PRINCIPLES §4/§8](../../PRINCIPLES.md).
6. **Measure before tuning.** `/usage` attributes usage to subagents, skills, plugins and MCP servers and
   flags long context or cache misses (`d`/`w` switch between 24 hours and 7 days); `/context` shows
   what fills the window.
7. **Lean always-loaded instructions** (2026-10-15). The playbook `CLAUDE.md` went from 356 to 174
   lines (routing and hard rules only; §1–§12 numbering kept because other docs cite it). The user-level
   `~/.claude/CLAUDE.md` now reads `PRINCIPLES.md` once before the first change instead of at every
   session start, says to always pass `model` to subagents, and suggests `/clear` at task boundaries.
   Mod repos keep their own `CLAUDE.md` copies; they only shrink when synced ([`MODS.md`](../../MODS.md)).
8. **Cheapest worker that gets it right** (user rule, 2026-10-15). Investigation goes to subagents so
   the director's context stays small; Haiku 5.5 or newer for simple tasks (build/test errors only, log
   filtering, file and symbol lists, engine-identification sweeps), Sonnet for work that needs reasoning,
   Opus only for the hard separable parts. Lives in the user-level `~/.claude/CLAUDE.md` and
   [CLAUDE.md §12](../../CLAUDE.md#12-working-model-expensive-model-as-director).

## Settings worth knowing

Main model and effort are per-user choices (the maintainer keeps Opus as the main model, 2026-10-15).
Names verified in the Claude Code docs on 2026-10-15:

- **Cap the context:** `"autoCompactWindow": 200000` in `~/.claude/settings.json` (or
  `CLAUDE_CODE_AUTO_COMPACT_WINDOW=200000`, which overrides the setting; `/autocompact` sets it per model).
  `CLAUDE_CODE_DISABLE_1M_CONTEXT=1` treats native-1M models as 200K. The biggest single lever for long
  RE sessions; set to 200000 on the maintainer's machine on 2026-10-15.
- **Effort:** Opus 5.5 and Sonnet 5.5 default to `medium`; per-model values go in `modelSettings`
  (`"claude-opus-5-5": { "effortLevel": "high" }`); a top-level `effortLevel` does not apply to Opus 5.5.
  `/effort` changes it without breaking the cache; the word `ultrathink` raises it for one turn.
- **Subagent model:** resolved as per-call `model` → agent frontmatter `model` → `CLAUDE_CODE_SUBAGENT_MODEL`
  → main model. Setting `CLAUDE_CODE_SUBAGENT_MODEL=sonnet` makes Sonnet the default while still allowing
  an explicit Opus worker; adding `CLAUDE_CODE_SUBAGENT_MODEL_FORCE=1` forbids any override. It does not
  change the built-in Explore/Plan agents. `omitClaudeMd: true` in an agent file skips the `CLAUDE.md`
  load for agents that get everything from the prompt.
- **`opusplan`:** Opus in plan mode, Sonnet when executing; each toggle is a model switch with a cold cache.
- **Background usage:** agent teams (~7×), `/loop`, scheduled tasks and workflows all spend the same
  limits; `skipWorkflowUsageWarning` hides the workflow cost warning.

## Code-navigation tools evaluated

No rigorous independent benchmark exists for any of these; most published token figures are vendor
claims or single-user anecdotes.

- **graphify** (`graphifyy`: code knowledge graph + `CLAUDE.md` section + PreToolUse hook) —
  **rejected**, and uninstalled from the maintainer's machine on 2026-10-15 (`uv tool uninstall
  graphifyy`, the `~/.claude/skills/graphify` skill, and its `~/.claude/CLAUDE.md` section; its skill
  description claimed "any question about a codebase", so it could trigger on its own). A query returns ~2K tokens, often more than the grep it replaces (graphify issue #985).
  A controlled A/B on a large .NET monorepo (user report, Aug 2026; graphify MCP vs plain grep/glob)
  measured 1.6–1.9× more cost and weaker answers. Its hook injects "MANDATORY" nudges into every agent
  and reportedly breaks on recent Claude Code versions. Our repos are either small (graphify's own detector
  says they don't need a graph) or large decompiled dumps — the size range where the A/B lost.
- **Official LSP plugins** (`csharp-lsp`, `clangd-lsp`, `lua-lsp` in `claude-plugins-official`) —
  **deferred.** Go-to-definition / find-references could beat grep-then-read, but the savings are
  anecdotal; Windows has open "unsafe location (current directory)" spawn bugs for csharp-ls and clangd
  (Jul 2026: #75237, #75612, #73961, #78188); and the servers need a project (`.csproj`,
  `compile_commands.json`), so they don't help on loose decompiled dumps. Revisit only for mods that
  build, once those bugs are fixed, piloting on one mod first.
- **Serena MCP** (LSP-based symbol retrieval) — **rejected for now.** Conflicting evidence (one
  measurement found +73% tokens) and it inherits the LSP project/Windows issues.
- **Vector / semantic search MCPs** (claude-context, code-index-mcp, …) — **rejected.** Savings claims
  are vendor benchmarks; they need an embedding service and index upkeep. Anthropic dropped a vector-DB
  approach in early Claude Code because agentic search worked better.

## Sources

- Anthropic docs (fetched 2026-10-12, model config and subagents re-checked 2026-10-15):
  [costs](https://code.claude.com/docs/en/costs),
  [prompt caching](https://code.claude.com/docs/en/prompt-caching),
  [model config](https://code.claude.com/docs/en/model-config),
  [subagents](https://code.claude.com/docs/en/sub-agents).
- Plan limits: [Using Claude Code with your Pro or Max plan](https://support.claude.com/en/articles/11145838-using-claude-code-with-your-pro-or-max-plan)
  (fetched 2026-10-15).
- Multi-agent token cost: [How we built our multi-agent research system](https://www.anthropic.com/engineering/multi-agent-research-system)
  (Anthropic, Jun 2025).
- graphify: [issue #985](https://github.com/Graphify-Labs/graphify/issues/985) (May–Sep 2026),
  [independent review](https://www.roborhythms.com/graphify-review/) (2026).
- LSP on Windows: [Claude Code issue #75237](https://claudeissues.com/issue/75237-bug-lsp-plugin-csharp-ls-spawn-fails-with-unsafe-location-current-directory-desp)
  (Jul 2026).
- Serena: [token measurement write-up](https://zenn.dev/iraken/articles/891b137c9c6628).
- Agentic search vs RAG at Anthropic:
  [Boris Cherny interview summary](https://officechai.com/ai/claude-researcher-explains-how-agentic-search-performed-better-than-rag-for-code-generation/) (2025).
