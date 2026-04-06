# Vietnamese IME Fix for Claude Code — Technical Analysis

**Repo:** https://github.com/manhit96/claude-code-vietnamese-fix  
**Date:** 2026-04-06  
**Scope:** Mechanism, installation, version stability

---

## 1. PROBLEM STATEMENT

Vietnamese IMEs (Input Method Editors) like OpenKey, EVKey, PHTV, Unikey use **backspace-then-replace technique** to convert characters:
- User types `a`
- IME sends: backspace (DEL char `0x7F`) + `á`
- **Bug:** Claude Code processes backspace, deletes `a`, but never inserts replacement `á`
- **Result:** Characters vanish; user sees nothing or garbled text

Claude Code's `cli.js` (npm) has input handling logic that explicitly checks `.includes("\\x7f")` (DEL char) and processes backspace, but lacks the "insert replacement" part.

---

## 2. THE FIX — TECHNICAL MECHANISM

### Target File
- **Path:** `~/.npm/_npx/.../@anthropic-ai/claude-code/cli.js` (or similar NPM cache path)
- **What it is:** Compiled/minified JavaScript (npm package)
- **Why it's there:** Claude Code CLI runs via npm

### What the Patcher Does

The `patcher.py` script performs **code injection surgery**:

**Step 1: Locate Bug Block**
```python
pattern = '.includes("\\x7f")'  # Find the DEL char check
```
Finds the `if()` statement that handles Vietnamese IME backspace detection.

**Step 2: Extract Variable Names**
Uses regex to capture dynamic variable names from minified code:
```
- input     → the input text variable (e.g., `a`, `l`, `t`)
- state     → the current editor state object
- cur_state → the previous cursor state
- update_text   → function that updates display text
- update_offset → function that updates cursor position
```

Since code is minified, variable names are `a`, `b`, `c` (single letters). Patcher must extract these dynamically.

**Step 3: Replace Buggy Block**
Original logic (pseudo):
```javascript
if (input.includes("\x7f")) {
    // backspace only — bug!
    state = curState.backspace();
}
```

Injected fix (reconstructed from patcher.py `generate_fix`):
```javascript
/* Vietnamese IME fix */
if (input.includes("\x7f")) {
    let count = (input.match(/\x7f/g) || []).length,  // How many backspaces?
        textWithoutDel = input.replace(/\x7f/g, ""),   // Replacement characters
        state = curState;
    
    // Apply each backspace
    for (let i = 0; i < count; i++) {
        state = state.backspace();
    }
    
    // Apply each replacement character
    for (const char of textWithoutDel) {
        state = state.insert(char);
    }
    
    // Update display if changed
    if (!curState.equals(state)) {
        if (curState.text !== state.text) {
            updateText(state.text);
        }
        updateOffset(state.offset);
    }
    return;  // Exit early, don't process backspace again
}
```

**Key insight:** The fix separates:
1. Backspace count (number of DEL chars in input)
2. Replacement text (DEL chars removed = the actual Vietnamese characters)

---

## 3. INSTALLATION FLOW

### macOS/Linux
```bash
curl -fsSL https://raw.githubusercontent.com/manhit96/claude-code-vietnamese-fix/main/install.sh | bash
```

**What it does:**
1. Checks for `git` (clone repo)
2. Checks for `python3` or `python`
3. Clones repo to `~/.claude-vn-fix/`
4. Runs `patcher.py --auto` (applies patch)

### Windows (PowerShell)
```powershell
irm https://raw.githubusercontent.com/manhit96/claude-code-vietnamese-fix/main/install.ps1 | iex
```
Same flow, PowerShell version.

### Post-Installation
Stores patcher in `~/.claude-vn-fix/patcher.py` for manual re-runs after Claude Code updates.

---

## 4. WHY IT STOPS WORKING AFTER UPDATES

**Root cause:** NPM updates = new `cli.js` = loss of patch.

### Lifecycle

| Event | State | Action |
|-------|-------|--------|
| User installs patch | `cli.js` patched | Working ✓ |
| Anthropic releases Claude Code v2.x | NPM downloads NEW `cli.js` | **Patch lost** |
| User runs `npm install -g @anthropic-ai/claude-code` | New binary installed | Bug reappears ✗ |

### Why Patch is Lost
1. Patcher injects code into cached npm package
2. `npm install -g` **downloads fresh copy** from npm registry
3. Fresh copy = original buggy code (no patch marker)
4. Users must re-run: `python3 ~/.claude-vn-fix/patcher.py`

### Detection
The patcher checks for marker `/* Vietnamese IME fix */` in the file:
- **Present?** → Already patched, skip
- **Missing?** → Either fresh install OR Claude Code was updated

---

## 5. PATCH STABILITY & FAILURE MODES

### What Can Break the Fix

| Scenario | Impact | Recovery |
|----------|--------|----------|
| **Claude Code API changes** | Patcher can't find bug pattern | Manual restore: `patcher.py --restore` |
| **Minification differs** | Variable extraction fails | Patcher shows error; no file modified (rollback automatic) |
| **New Claude Code architecture** | No `.includes("\x7f")` pattern | Anthropic may have fixed upstream; patcher will report it |
| **npm cache location changes** | `find_cli_js()` fails to locate file | Can specify path: `patcher.py --path /custom/path/cli.js` |

### Safeguards
1. **Backup before patch:** Creates `cli.js.backup-YYYYMMDD-HHMMSS`
2. **Verify after patch:** Confirms patch marker written successfully
3. **Automatic rollback:** On error, restores from backup and deletes backup

---

## 6. MANUAL OPERATIONS

After installation in `~/.claude-vn-fix/`:

```bash
# Re-apply after Claude Code update
python3 ~/.claude-vn-fix/patcher.py

# Restore original cli.js (revert patch)
python3 ~/.claude-vn-fix/patcher.py --restore

# Fix custom cli.js path
python3 ~/.claude-vn-fix/patcher.py --path /path/to/cli.js

# Update patcher itself to latest version
cd ~/.claude-vn-fix && git pull
```

---

## 7. KEY TECHNICAL INSIGHTS

**Why This Approach Works:**
- Targets the **exact bug location** (no side effects)
- Early return prevents double-processing
- Preserves original variable naming (survives minification)
- Regex extraction handles any variable names

**Why It Might Fail:**
- Claude Code updates its `cli.js` structure (version lock issue)
- Anthropic fixes the bug upstream (makes patch unnecessary but harmless)
- Different npm cache paths on corporate networks/proxies
- Python not in PATH during installation

**Why Users Need to Re-run:**
- NPM doesn't persist patches — fresh install = fresh files
- Patch lives in editor config, not in npm registry
- No hook to auto-re-patch on npm update (would require npm scripts or global post-install hook)

---

## UNRESOLVED QUESTIONS

1. **Will Anthropic fix this upstream?** Unknown. If they do, patch marker check prevents double-patching, but users should still update.

2. **What if multiple Claude Code versions are installed?** Patcher searches standard npm paths; custom installations may require `--path` flag.

3. **Cross-platform backup compatibility?** Backups use paths with separators; restore should work cross-platform (tested bash/PowerShell).

4. **Does this affect other IME languages (Chinese, Japanese)?** Likely similar backspace-insert pattern, but unclear if they use `0x7F` as delimiter. Fix is Vietnamese-specific.

5. **Performance impact?** Injected loop processes each backspace + each character. For typical IME input (2-3 chars), negligible overhead.
