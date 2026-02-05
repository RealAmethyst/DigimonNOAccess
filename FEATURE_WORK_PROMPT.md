# Feature Work Prompt

Read `refactor-docs/04-architecture.md` and `refactor-docs/08-refactoring-summary.md` to understand the codebase architecture, then read `Main.cs`, `IAccessibilityHandler.cs`, and `HandlerBase.cs` to see how handlers work.

## Your Task

You are working on an accessibility mod (MelonLoader, Unity Il2Cpp) that makes Digimon World Next Order playable for blind users via screen reader. The user will describe a feature to build or modify. Follow the workflow below.

## Workflow

### Step 1: Understand the Request

Ask the user ONE clear question if the request is ambiguous. Otherwise, proceed.

### Step 2: Research with Sub-Agents

Use the Task tool with sub-agents to gather context WITHOUT loading everything into your own context. This is critical for staying efficient.

Launch these in parallel as needed:

- **Explore agent** - to find relevant files, search for game API classes in `decompiled/`, or locate existing handler code
- **Plan agent** - to design the implementation approach if the feature is non-trivial

Examples of good sub-agent prompts:
- "Search decompiled/ for any class containing 'Shop' or 'Store' in its name. List the class names, their namespaces, and any public methods/properties that look UI-related."
- "Read FieldHudHandler.cs and summarize: what panel type does it use, how does it detect open/close, what does it announce, and what cursor tracking does it do?"
- "Search all *.cs files in the project root for any handler that reads item names or inventory data. List the file names and the relevant method names."

Key rule: **Tell the agent exactly what information to return.** Do not ask it to "look around" vaguely.

### Step 3: Check Game API Before Writing Code

ALWAYS search `decompiled/` for actual class and method names before writing any code. NEVER guess Il2Cpp class names, method signatures, or property names. If a class does not exist in `decompiled/`, it does not exist.

Also check `docs/game-api.md` for already-documented patterns and key bindings.

### Step 4: Implement

#### For a NEW handler:

1. Extend `HandlerBase<TPanel>` where `TPanel` is the Il2Cpp panel type (e.g., `uShopPanel`)
2. Implement required members:
   - `LogTag` (string property, e.g., `"[ShopPanel]"`)
   - `Priority` (int property, higher = checked first for status announcements)
   - `AnnounceStatus()` (reads current state aloud)
3. Override lifecycle methods as needed:
   - `OnOpen()` - announce menu opened, read initial cursor position
   - `OnClose()` - cleanup (base handles resetting `_panel`, `_lastCursor`, `_wasActive`)
   - `OnUpdate()` - detect cursor changes, tab switches, text changes
   - `IsOpen()` - only override if the default `FindObjectOfType + activeInHierarchy` check is insufficient
4. Register in `Main.cs` by adding to the `_handlers` list in `OnSceneWasLoaded`
5. Use existing utilities, do not reinvent them:
   - `ScreenReader.Say()` for all announcements
   - `AnnouncementBuilder.CursorPosition()` / `MenuOpen()` for formatted announcements
   - `TextUtilities.StripRichTextTags()` / `CleanText()` for text cleaning
   - `PartnerUtilities` for partner labels, stat names, status effects
   - `GameStateService` for field/battle/pause state checks
   - `DebugLogger.Log()` / `Warning()` / `Error()` for all logging

#### For MODIFYING an existing handler:

1. Use a sub-agent to read the handler file and summarize its current behavior
2. Only read the file yourself when you are ready to edit
3. Make targeted changes; do not refactor surrounding code unless asked

### Step 5: Build and Verify

Run: `"C:\Program Files\dotnet\dotnet.exe" build DigimonNOAccess.csproj`

Fix any errors. The build should produce 0 errors. Pre-existing warnings (CS0414 in CarePanelHandler, StoragePanelHandler, EvolutionHandler) are expected.

### Step 6: Commit (only if asked)

Follow the git commit instructions in your system prompt. Do not commit automatically.

## Sub-Agent Strategy

The biggest risk to efficiency is loading too many files into your main context. Prevent this by:

- **Use Explore agents for search tasks.** Instead of running multiple Grep/Glob calls yourself, launch an Explore agent: "Find all files that reference `uBattlePanel` and list what each one does with it."
- **Use Explore agents to read files you only need summaries from.** Instead of reading a 500-line handler yourself, ask: "Read BattlePanelHandler.cs and tell me: what panel type, what states it tracks, what it announces on cursor change."
- **Use Bash agents for build tasks.** Launch a Bash agent to build and report errors while you continue working.
- **Launch independent agents in parallel.** If you need to search `decompiled/` for a class AND read an existing handler, do both at once.
- **Only read files into your own context when you are about to edit them.**

## Patterns to Follow

- Private fields: `_camelCase`
- Handler classes: `[Feature]Handler`
- One handler per game panel/screen
- Track state changes with `_lastX` fields to avoid duplicate announcements
- Use `try/catch` around Il2Cpp property access (these can throw on null native pointers)
- Never override game key bindings; only use safe mod keys from `docs/game-api.md`
- All logs and comments in English

## Reference Files

- `docs/game-api.md` - Game key bindings, documented methods and patterns
- `docs/ACCESSIBILITY_MODDING_GUIDE.md` - Code patterns and architecture guide
- `docs/menu-accessibility-checklist.md` - Checklist for menu implementations
- `refactor-docs/03-code-patterns.md` - Inventory of code patterns used across handlers
- `refactor-docs/01-project-structure.md` - Full file listing with descriptions
