---
name: code-memory
description: >-
  Captures and retrieves structural understanding of source code gained during investigations.
  Use after reading and understanding code to save context for future sessions, or before
  investigating code to check for existing context. Stores summaries in memory/ files mirroring
  source paths. Triggers: "save what I learned", "remember this code", "check if we know about",
  "investigate", or any post-investigation context capture.
---

# Code Memory

Persist understanding of code structure and implementation details gained during investigations
so future sessions can ramp up quickly without re-reading source files.

## When to Use

- After investigating source code and gaining understanding of how classes/modules work
- Before investigating code, to check if prior context exists that provides a shortcut
- When asked to "remember" or "save" understanding of code
- During any investigation that reads multiple source files to understand a subsystem
- When switching to a new session that may need the same code understanding

## When Not to Use

- For storing user preferences or project-wide conventions (use CLAUDE.md or copilot-instructions.md)
- For tracking todos or work items (use session SQL)
- For files outside the current repository

## Inputs

| Input | Required | Description |
|-------|----------|-------------|
| Source file path(s) | Yes | The file(s) that were investigated or need investigation |
| Mode | No | `save` (write/update context), `load` (retrieve context), or `auto` (load first, save after investigation). Default: `auto` |

## Workflow

### Step 1: Determine the memory file path

Map the source file path to a memory file path. The memory file mirrors the source path structure
inside the `memory/` directory within this skill's folder.

**Path mapping rule:**
- Take the source file path **relative to the repository root**
- Replace path separators with `/` for consistency
- Append `.md` extension
- Prefix with the memory directory path

**Example mappings** (repository root: `D:\git\CoreWCF`):

| Source File | Memory File |
|------------|-------------|
| `src\CoreWCF.Http\src\ServiceModelHttpMiddleware.cs` | `memory/src/CoreWCF.Http/src/ServiceModelHttpMiddleware.cs.md` |
| `src\CoreWCF.Primitives\src\Dispatcher\DispatchRuntime.cs` | `memory/src/CoreWCF.Primitives/src/Dispatcher/DispatchRuntime.cs.md` |

The memory directory base path is: ` .copilot/skills/code-memory/memory/`

### Step 2: Check for existing context (Load phase)

Before reading a source file during an investigation:

1. **Check if a memory file exists** for the source path
2. If it exists, **check timestamps**:
   - Get the last-modified time of the source file
   - Get the last-modified time of the memory file
   - If the source file is **newer** than the memory file, flag the context as potentially stale
3. If the memory file exists and is not stale:
   - Read and summarize the memory file
   - Decide if it contains sufficient information for the current investigation
   - If sufficient, **use the cached context instead of reading the source file**
   - If insufficient, proceed to read the source file (and update context afterward in Step 4)
4. If the memory file exists but is stale:
   - Read the memory file for baseline understanding
   - Read the source file to validate and identify changes
   - Proceed to Step 4 to update the context
5. If no memory file exists, read the source file and proceed to Step 3/4

### Step 3: Investigate the source code

Read and understand the source file(s). Focus on capturing:

- **Class/interface purpose** — What role does this play in the system?
- **Key public API** — Important methods, properties, and their contracts
- **Internal structure** — How the class organizes its work (key private methods, state management)
- **Dependencies** — What other classes/services does it depend on? How are they injected?
- **Design patterns** — Observer, strategy, factory, etc. if applicable
- **Threading/async model** — Concurrency considerations, locks, async flows
- **Extension points** — Virtual methods, events, delegates, interfaces for customization
- **Non-obvious behavior** — Gotchas, edge cases, implicit assumptions, ordering requirements
- **Relationships** — How this class connects to its callers and callees in the broader system

### Step 4: Write or update the memory file (Save phase)

Create or update the memory file with the gained understanding.

**Memory file format:**

```markdown
# {ClassName} — {Brief one-line purpose}

**Source:** `{relative/path/to/source.cs}`
**Namespace:** `{Namespace}`
**Last validated:** {YYYY-MM-DD}

## Purpose

{1-3 sentences describing what this class does and why it exists}

## Key API

{List the important public methods/properties with brief descriptions of what they do,
their parameters, and return values. Focus on what callers need to know.}

## Internal Structure

{Describe how the class works internally. Key fields, state management,
the flow of important operations. This is the "how" that saves future readers
from having to trace through the code.}

## Dependencies

{What does this class depend on? DI-injected services, static helpers, base classes.
Note how dependencies are obtained and used.}

## Design Notes

{Patterns used, threading model, non-obvious behavior, gotchas, important
invariants that must be maintained. Only include if there's something
worth noting.}

## Relationships

{How does this class fit into the broader system? What calls it?
What does it call? Key interaction flows.}
```

**Writing guidelines:**
- Write for copilot needing to understand this code as part of an investigation **without reading it**
- Be specific — include actual method names, field names, parameter types
- Focus on "why" and "how" over "what" (the source code already shows "what")
- Omit sections that have nothing noteworthy to say
- Keep each file under 200 lines — this is a summary, not documentation
- Use code snippets sparingly, only for key patterns that are hard to describe in prose

**When updating an existing memory file:**
- Preserve existing content that is still accurate
- Add new sections or details learned in the current investigation
- Update the "Last validated" date
- If the source file changed, revise any descriptions that no longer match

### Step 5: Create parent directories as needed

Before writing the memory file, ensure all parent directories in the memory path exist.

```powershell
New-Item -ItemType Directory -Path "{parent_directory}" -Force
```

## Validation

After saving context:
- The memory file exists at the expected path
- The memory file contains the required header fields (Source, Namespace, Last validated)
- The content accurately reflects the source code as currently understood
- The file is under 200 lines

After loading context:
- The memory file was found (or confirmed not to exist)
- Staleness was checked via timestamp comparison
- The loaded context was evaluated for sufficiency before deciding to read source

## Common Pitfalls

| Pitfall | Solution |
|---------|----------|
| Writing too much detail | Focus on structure and "why", not line-by-line description. Under 200 lines. |
| Stale context misleads | Always check source file timestamp against memory file timestamp |
| Context too shallow to be useful | Include internal structure and non-obvious behavior, not just public API |
| Forgetting to update after investigation | Always save/update context after gaining new understanding |
| Absolute paths in memory files | Use repository-relative paths in the Source field |
| Missing parent directories | Create directories with `-Force` before writing |
