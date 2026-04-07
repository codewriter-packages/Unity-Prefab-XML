#!/usr/bin/env bash
set -euo pipefail

REPO="codewriter-packages/Unity-Prefab-XML"
BRANCH="main"
BASE_URL="https://raw.githubusercontent.com/${REPO}/${BRANCH}"

GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m'

TARGET="$HOME/.claude"

for arg in "$@"; do
    case "$arg" in
        --local) TARGET=".claude" ;;
        --help|-h)
            echo "Usage: install-claude-skill.sh [--local]"
            echo ""
            echo "Install PrefabXML skill and agent for Claude Code."
            echo ""
            echo "By default installs to ~/.claude (available in all projects)."
            echo "Use --local to install into .claude/ in the current directory."
            exit 0
            ;;
        *)
            echo "Unknown option: $arg (use --help for usage)"
            exit 1
            ;;
    esac
done

SKILL_DIR="$TARGET/skills/prefabxml"
AGENT_DIR="$TARGET/agents"

echo -e "${GREEN}PrefabXML Claude Skill Installer${NC}"
echo "================================="
echo ""
echo "Target: $TARGET"
echo ""

mkdir -p "$SKILL_DIR" "$AGENT_DIR"

download() {
    local url="${BASE_URL}/$1"
    local dest="$2"
    echo -n "  ${dest} ... "
    if curl -fsSL "$url" -o "$dest" 2>/dev/null; then
        echo -e "${GREEN}ok${NC}"
    else
        echo -e "${YELLOW}failed${NC}"
        return 1
    fi
}

echo "Downloading files:"
download ".claude/skills/prefabxml/SKILL.md"     "$SKILL_DIR/SKILL.md"
download "FORMAT.md"                               "$SKILL_DIR/FORMAT.md"
download "GUIDE.md"                                "$SKILL_DIR/GUIDE.md"
download ".claude/skills/prefabxml/TEMPLATES.md"  "$SKILL_DIR/TEMPLATES.md"
download ".claude/agents/agent-prefabxml.md"      "$AGENT_DIR/agent-prefabxml.md"

echo ""
echo -e "${GREEN}Done!${NC}"
echo ""
echo "Installed:"
echo "  Skill:  /prefabxml — generate .prefabxml from text description"
echo "  Agent:  agent-prefabxml — parallel prefab generation subagent"
echo ""
echo "Usage:"
echo "  /prefabxml settings panel with volume slider and music toggle"
echo "  @agent-prefabxml create a leaderboard screen"
echo ""
echo -e "${YELLOW}To update, run this script again.${NC}"
