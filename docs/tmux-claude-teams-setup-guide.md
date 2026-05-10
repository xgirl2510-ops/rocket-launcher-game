# Tmux + Claude Code Agent Teams Setup Guide

Setup tmux split panes cho Claude Code Agent Teams trên macOS.

---

## 1. Install tmux

```bash
brew install tmux
```

Verify: `tmux -V` (cần 3.3+)

---

## 2. Tmux Config

Tạo `~/.tmux.conf`:

```bash
# === General ===
set -g default-terminal "screen-256color"
set -ga terminal-overrides ",xterm-256color:Tc"
set -g mouse on
set -g history-limit 10000
set -g base-index 1
setw -g pane-base-index 1
set -g renumber-windows on
set -g escape-time 0
set -g focus-events on
set -g set-clipboard on

# === Prefix: Ctrl+a (keep Ctrl+b too) ===
set -g prefix2 C-a
bind C-a send-prefix -2

# === Pane splitting (intuitive keys) ===
bind | split-window -h -c "#{pane_current_path}"
bind - split-window -v -c "#{pane_current_path}"
bind c new-window -c "#{pane_current_path}"

# === Vi-style pane navigation ===
bind h select-pane -L
bind j select-pane -D
bind k select-pane -U
bind l select-pane -R

# === Resize panes (Prefix + H/J/K/L) ===
bind -r H resize-pane -L 5
bind -r J resize-pane -D 5
bind -r K resize-pane -U 5
bind -r L resize-pane -R 5

# === Quick layouts ===
bind E select-layout even-horizontal
bind V select-layout even-vertical
bind T select-layout tiled

# === Copy mode (vi) ===
setw -g mode-keys vi
bind -T copy-mode-vi v send-keys -X begin-selection
bind -T copy-mode-vi y send-keys -X copy-pipe-and-cancel "pbcopy"

# === Status bar ===
set -g status-position bottom
set -g status-interval 5
set -g status-style "bg=#1a1b26,fg=#c0caf5"
set -g status-left-length 30
set -g status-right-length 60
set -g status-left "#[fg=#1a1b26,bg=#7aa2f7,bold] #S #[fg=#7aa2f7,bg=#1a1b26] "
set -g status-right "#[fg=#565f89] %H:%M #[fg=#7aa2f7]| #[fg=#c0caf5]#(tmux list-panes | wc -l | tr -d ' ')P "

# === Window status ===
setw -g window-status-format "#[fg=#565f89] #I:#W "
setw -g window-status-current-format "#[fg=#1a1b26,bg=#7aa2f7,bold] #I:#W "

# === Pane borders ===
set -g pane-border-style "fg=#3b4261"
set -g pane-active-border-style "fg=#7aa2f7"

# === Quick reload ===
bind r source-file ~/.tmux.conf \; display "Config reloaded"

# === Kill pane without confirm ===
bind x kill-pane
```

---

## 3. Claude Code Settings

Trong `~/.claude/settings.json`, thêm:

```json
{
  "env": {
    "CLAUDE_CODE_EXPERIMENTAL_AGENT_TEAMS": "1",
    "teammateMode": "tmux"
  },
  "tmuxSplitPanes": true,
  "hooks": {
    "TaskCompleted": [
      {
        "hooks": [
          {
            "type": "command",
            "command": "node \"$HOME/.claude/hooks/task-completed-handler.cjs\""
          }
        ]
      }
    ],
    "TeammateIdle": [
      {
        "hooks": [
          {
            "type": "command",
            "command": "node \"$HOME/.claude/hooks/teammate-idle-handler.cjs\""
          }
        ]
      }
    ]
  }
}
```

## 3b. Hook Scripts

Copy 3 file vao `~/.claude/hooks/`:

| File | Chuc nang |
|------|-----------|
| `team-context-inject.cjs` | Inject team context (peers, tasks) khi teammate start |
| `teammate-idle-handler.cjs` | Bao lead khi teammate idle, show available tasks |
| `task-completed-handler.cjs` | Notify lead khi task completed |
| `lib/ck-config-utils.cjs` | Shared util (required by hooks above) |

Cac file nay nam tai: `~/.claude/hooks/` tren may hien tai. Copy nguyen folder hooks sang may moi:

```bash
# Tren may nguon:
tar czf claude-hooks.tar.gz -C ~/.claude hooks/

# Tren may dich:
mkdir -p ~/.claude
tar xzf claude-hooks.tar.gz -C ~/.claude/
```

---

## 4. Cach su dung

### Bat dau session

```bash
tmux new -s dev
```

### Chay Claude Code trong tmux

```bash
claude
```

### Khi spawn Agent Teams (vd: /team review)

Claude Code tu dong tao split panes cho moi teammate. Lead o pane chinh, teammates o cac pane con.

### Phim tat tmux (sau Ctrl+a hoac Ctrl+b)

| Phim | Chuc nang |
|------|-----------|
| `\|` | Split doc (pane ben canh) |
| `-` | Split ngang (pane ben duoi) |
| `h/j/k/l` | Di chuyen giua panes |
| `H/J/K/L` | Resize pane |
| `E` | Layout deu ngang |
| `V` | Layout deu doc |
| `T` | Layout tiled (luoi) |
| `x` | Dong pane |
| `r` | Reload config |
| `z` | Zoom/unzoom pane hien tai |

### Xem tat ca panes

```bash
# Trong tmux:
Ctrl+a + w    # list windows
Ctrl+a + q    # show pane numbers
```

---

## 5. Quick Setup Script

Copy paste chay 1 lan tren may moi:

```bash
# Install tmux
brew install tmux

# Tao tmux config
curl -fsSL https://raw.githubusercontent.com/xgirl2510-ops/rocket-launcher-game/main/docs/tmux-claude-teams-setup-guide.md | sed -n '/^```bash$/,/^```$/p' | head -50 > /dev/null
# Hoac copy noi dung Section 2 vao ~/.tmux.conf

# Enable Agent Teams trong Claude Code
claude config set env.CLAUDE_CODE_EXPERIMENTAL_AGENT_TEAMS 1
claude config set teammateMode tmux
claude config set tmuxSplitPanes true

# Verify
tmux -V
cat ~/.claude/settings.json | grep -i team
echo "Done! Chay: tmux new -s dev && claude"
```

---

## 6. Skill /team

Claude dung `/team` skill de spawn agents. Skill nay nam tai `~/.claude/skills/team/SKILL.md`.

Copy sang may moi:
```bash
# Tren may nguon:
tar czf claude-team-skill.tar.gz -C ~/.claude skills/team/

# Tren may dich:
mkdir -p ~/.claude/skills
tar xzf claude-team-skill.tar.gz -C ~/.claude/skills/
```

Hoac cai ClaudeKit Marketing kit (bao gom skill nay):
```bash
npx claudekit install marketing
```

---

## 7. Kiem tra toan bo

```bash
# 1. tmux
tmux -V                              # >= 3.3

# 2. Settings
cat ~/.claude/settings.json | grep -i team   # AGENT_TEAMS=1, tmux

# 3. Hooks
ls ~/.claude/hooks/team*.cjs         # 1 file
ls ~/.claude/hooks/teammate*.cjs     # 1 file
ls ~/.claude/hooks/task-completed*.cjs  # 1 file

# 4. Skill
ls ~/.claude/skills/team/SKILL.md    # exists

# 5. Test
tmux new -s test
claude
# Goi: /team review src/
# Phai thay split panes tu dong tao
```

---

## Troubleshooting

| Van de | Fix |
|--------|-----|
| Panes khong tu tao | Check `tmuxSplitPanes: true` trong settings.json |
| "Agent Teams not available" | Check `CLAUDE_CODE_EXPERIMENTAL_AGENT_TEAMS=1` |
| Mouse khong scroll | Check `set -g mouse on` trong .tmux.conf |
| Copy khong hoat dong | Dung `Ctrl+a + [` vao copy mode, `v` select, `y` copy |
