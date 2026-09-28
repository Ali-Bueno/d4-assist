# Modding Projects — Accessibility Template Workspace

Reusable workspace for building **game accessibility mods** (screen-reader output via PRISM + audio
cues). It bundles a project playbook and a growing reverse-engineering reference library so each new
mod can be structured section by section instead of from scratch.

## Contents

| Path | Purpose |
|------|---------|
| [`CLAUDE.md`](CLAUDE.md) | **The playbook.** How to approach an accessibility mod: investigate the engine, choose the framework, integrate PRISM, structure code, accessibilize menus, hook patterns, conventions. |
| [`PRINCIPLES.md`](PRINCIPLES.md) | **The ground rules.** General engineering & accessibility principles that apply under the playbook: code discipline, scope, testing/build, no magic numbers, framework rules, RE tools, publishing. Drop it into any project (or your AI assistant's config). |
| [`reference/`](reference/) | **Reference library.** Engine-agnostic reverse-engineering studies of how real games implement the systems we reproduce (currently: audio navigation). |
| [`reference/_engine-study-template.md`](reference/_engine-study-template.md) | Blank template to document a new engine/game when asked to study one. |

## Starting a new mod

1. Read [`CLAUDE.md`](CLAUDE.md) → **Investigar el Engine** and pick the framework for *that* game.
2. Pull the relevant subsystem references from [`reference/`](reference/) (e.g. for navigation, start
   at [`reference/audio-navigation/README.md`](reference/audio-navigation/README.md)).
3. Build in the recommended order: adapter + speech → beacon → radar → scan → polish.

## Asked to study an engine?

Copy [`reference/_engine-study-template.md`](reference/_engine-study-template.md) into
`reference/<topic>/<game-slug>/`, identify the engine first, then fill it in with confidence notes.
See [`reference/README.md`](reference/README.md).
