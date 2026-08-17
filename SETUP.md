# GymPlanner Codex setup (Windows)

## What is included

- `AGENTS.md` — project-wide rules loaded by Codex.
- `.agents/skills/` — six repository-scoped skills.
- `.codex/config.toml` — project-scoped Context7 and GitHub MCP configuration.

## Installation

Copy the following items into the Git repository root:

```text
AGENTS.md
.agents/
.codex/
```

The repository root is the folder that contains `.git`.

Commit `AGENTS.md`, `.agents/`, and `.codex/config.toml` so they travel with the project.

## Prerequisites

- Current Codex CLI.
- Node.js 18 or newer for Context7's `npx` server.
- Git.
- .NET SDK required by the project.

## Verify skills

Start Codex from the repository root:

```powershell
codex
```

Inside Codex:

```text
/skills
```

You should see skills whose names start with `gymplanner-`.

You can explicitly invoke one by typing `$`, for example:

```text
$gymplanner-debug Find why the notes list does not update after save.
```

Codex may also invoke a skill automatically when the task matches its description.

## Verify AGENTS.md

From PowerShell in the repository root:

```powershell
codex --ask-for-approval never "Summarize the active project instructions."
```

## Verify Context7 MCP

From PowerShell:

```powershell
codex mcp list
```

Then start Codex and run:

```text
/mcp
```

Context7 should be listed. A useful test prompt:

```text
Using Context7, check the current official documentation relevant to this project's .NET and Blazor versions.
```

## Enable GitHub MCP securely

The GitHub MCP entry is included but disabled by default.

1. Create a least-privilege GitHub Personal Access Token.
2. Save it as a Windows user environment variable:

```powershell
[Environment]::SetEnvironmentVariable(
    "GITHUB_PAT_TOKEN",
    "PASTE_TOKEN_HERE",
    "User"
)
```

3. Close and reopen the terminal.
4. Change this line in `.codex/config.toml`:

```toml
enabled = false
```

to:

```toml
enabled = true
```

5. Run:

```powershell
codex mcp list
```

Do not place the token in `.codex/config.toml`, `.env` committed to Git, `AGENTS.md`, or a skill.

## About Filesystem MCP

It is not included. Codex CLI already has native repository file and shell tools. Adding a separate Filesystem MCP duplicates capability and increases the number of tools and permissions without a clear benefit.

## Recommended first prompt

```text
Inspect this repository without changing files. Read AGENTS.md, list the available gymplanner skills, map the solution structure, identify the actual frameworks and database stack, and propose any corrections needed in AGENTS.md before development continues.
```

This first inspection is important because the package is based on the project information currently known, not a direct scan of your local repository.
