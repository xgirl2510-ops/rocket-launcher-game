# CCPM Repository Analysis

## What is CCPM?

**CCPM** = "Claude Code Project Management" (inferred) — an open-source Agent Skill for structured project management in AI-assisted development. It prevents context loss, enables parallel work streams, and enforces spec-driven coding by treating GitHub Issues as the source of truth.

## Core Features

1. **PRD-to-Code Pipeline**: Brainstorming → Requirements → Architecture → Task Decomposition → Execution
2. **GitHub Integration**: Auto-creates epics + sub-issues with dependency tracking
3. **Parallel Execution**: Intelligently decomposes monolithic features into independent work streams
4. **Local State Persistence**: Stores project files in `.claude/` directories (markdown-based)
5. **Framework-Agnostic**: Works with Claude Code, Factory, Codex, Cursor, Amp (Agent Skills standard)

## Automatic GitHub Issue Creation?

**YES** — During the "Sync" phase, it:
- Creates epic issues on GitHub
- Generates sub-issues for each task
- Auto-renames local files to match GitHub issue numbers
- Sets up worktrees for parallel work

## Claude Code Integration

Installed as a skill: `ln -s /path/to/ccpm .claude/skills/ccpm`

Activates automatically when project management intent detected. Works alongside existing codebase without special syntax.

## Maintenance Status

**ACTIVELY MAINTAINED** — Last commit: March 18, 2026 (bug reporting workflow, status.sh fixes, docs updates). Consistent daily development activity suggests ongoing active support.

## Proven Impact

Benchmark testing: 100% task success rate vs 27.7% baseline; 89% reduction in context-switching overhead.

---

**In short:** CCPM is a mature, actively-maintained project management framework for AI agents that automates epic→task decomposition, GitHub issue syncing, and parallel work orchestration. Not specifically Claude Code-only—it's framework-agnostic Agent Skills.
