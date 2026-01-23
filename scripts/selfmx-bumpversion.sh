#!/bin/bash
# =============================================================================
# selfmx-bumpversion.sh
#
# Bumps the version in src/SelfMX.Api/SelfMX.Api.csproj
#
# Usage:
#   ./selfmx-bumpversion.sh           # Bump patch version (1.0.0 -> 1.0.1)
#   ./selfmx-bumpversion.sh minor     # Bump minor version (1.0.5 -> 1.1.0)
#   ./selfmx-bumpversion.sh major     # Bump major version (1.5.3 -> 2.0.0)
#
# =============================================================================

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

# Clean up Syncthing conflict files first
"$SCRIPT_DIR/clean.sh"
REPO_ROOT="$SCRIPT_DIR/.."
CSPROJ="$REPO_ROOT/src/SelfMX.Api/SelfMX.Api.csproj"

# Check if csproj file exists
if [[ ! -f "$CSPROJ" ]]; then
    echo "Error: $CSPROJ not found"
    exit 1
fi

# Get bump type from argument (default: patch)
BUMP_TYPE="${1:-patch}"

# Show help
if [[ "$BUMP_TYPE" == "-h" || "$BUMP_TYPE" == "--help" ]]; then
    echo "Usage: $0 [patch|minor|major]"
    echo ""
    echo "Bumps the version in SelfMX.Api project"
    echo ""
    echo "Arguments:"
    echo "  patch   Bump patch version (1.0.0 -> 1.0.1) [default]"
    echo "  minor   Bump minor version (1.0.5 -> 1.1.0)"
    echo "  major   Bump major version (1.5.3 -> 2.0.0)"
    exit 0
fi

# Validate bump type
if [[ "$BUMP_TYPE" != "patch" && "$BUMP_TYPE" != "minor" && "$BUMP_TYPE" != "major" ]]; then
    echo "Error: Invalid bump type '$BUMP_TYPE'"
    echo "Usage: $0 [patch|minor|major]"
    exit 1
fi

# =============================================================================
# Check for clean git state before proceeding
# =============================================================================

cd "$REPO_ROOT"

# Check for uncommitted changes (staged or unstaged) or untracked files
HAS_CHANGES=false
UNTRACKED=$(git ls-files --others --exclude-standard)

if ! git diff --quiet || ! git diff --cached --quiet; then
    HAS_CHANGES=true
fi

if [[ -n "$UNTRACKED" ]]; then
    HAS_CHANGES=true
fi

if [[ "$HAS_CHANGES" == "true" ]]; then
    echo "Warning: You have uncommitted changes or untracked files."
    echo ""
    git status --short
    echo ""
    echo "Options:"
    echo "  [C] Auto-commit with Claude-generated message, then continue"
    echo "  [S] Stop and handle manually"
    echo ""
    read -p "Select option [C/S]: " DIRTY_RESPONSE

    case "$DIRTY_RESPONSE" in
        [cC])
            echo ""
            echo "Generating commit message with Claude..."
            echo ""

            # Build the prompt for Claude to generate a commit message
            COMMIT_PROMPT=$(cat <<'COMMIT_PROMPT_EOF'
Generate a git commit message for the following changes. Output ONLY the commit message, nothing else.

Use conventional commit format (e.g., "feat:", "fix:", "chore:", "docs:", "refactor:").
Keep the first line under 72 characters. Add a body if needed for complex changes.

Changes to commit:
COMMIT_PROMPT_EOF
)
            COMMIT_PROMPT="$COMMIT_PROMPT"$'\n\n'"$(git status --short)"$'\n\n'"$(git diff --stat)"

            # Generate commit message using Claude
            GENERATED_MSG=$(claude -p "$COMMIT_PROMPT" --dangerously-skip-permissions 2>/dev/null)

            if [[ -z "$GENERATED_MSG" ]]; then
                echo "Error: Failed to generate commit message with Claude."
                exit 1
            fi

            echo "Generated commit message:"
            echo "─────────────────────────────────────────"
            echo "$GENERATED_MSG"
            echo "─────────────────────────────────────────"
            echo ""

            # Stage all changes
            git add -A

            # Commit with generated message
            git commit -m "$GENERATED_MSG

Co-Authored-By: Claude Opus 4.5 <noreply@anthropic.com>"

            echo ""
            echo "Changes committed. Continuing with version bump..."
            echo ""
            ;;
        *)
            echo ""
            echo "Stopping. Please commit or stash your changes before bumping the version."
            exit 1
            ;;
    esac
else
    echo "Git state: clean (all changes committed)"
fi

echo ""

# =============================================================================
# Build verification - ensure everything compiles before bumping version
# =============================================================================

echo "Verifying build..."
echo ""

if ! dotnet build "$CSPROJ" --nologo -v q; then
    echo ""
    echo "Error: Build failed. Please fix build errors before bumping the version."
    exit 1
fi

echo "Build verification passed"
echo ""

# Extract current version from csproj
CURRENT_VERSION=$(grep -oP '<Version>\K[0-9]+\.[0-9]+\.[0-9]+(?=</Version>)' "$CSPROJ")

if [[ -z "$CURRENT_VERSION" ]]; then
    echo "Error: Could not find <Version>x.y.z</Version> in $CSPROJ"
    echo "Please add a <Version> element to your csproj file first."
    exit 1
fi

# Parse version components
IFS='.' read -r MAJOR MINOR PATCH <<< "$CURRENT_VERSION"

# Bump version based on type
case "$BUMP_TYPE" in
    major)
        MAJOR=$((MAJOR + 1))
        MINOR=0
        PATCH=0
        ;;
    minor)
        MINOR=$((MINOR + 1))
        PATCH=0
        ;;
    patch)
        PATCH=$((PATCH + 1))
        ;;
esac

NEW_VERSION="$MAJOR.$MINOR.$PATCH"

# Update csproj file
sed -i "s|<Version>$CURRENT_VERSION</Version>|<Version>$NEW_VERSION</Version>|" "$CSPROJ"

echo "Version bumped: $CURRENT_VERSION -> $NEW_VERSION"
echo "  - src/SelfMX.Api/SelfMX.Api.csproj"

# =============================================================================
# Run Claude to update changelog
# =============================================================================

echo ""
echo "Running Claude to update changelog..."
echo ""

CLAUDE_PROMPT=$(cat <<'PROMPT_EOF'
You have just bumped the SelfMX version. Your task is to add a changelog entry for this release.

## Version Information
- Previous version: $CURRENT_VERSION
- New version: $NEW_VERSION
- Bump type: $BUMP_TYPE

## Tasks

### 1. Review Recent Changes

Look at recent git commits since the last version to understand what changed:
```bash
git log --oneline HEAD~20..HEAD
```

### 2. Update Changelog

Add a new entry to `website/src/pages/changelog.md` for version $NEW_VERSION.

Insert the new entry at the TOP of the changelog (after the frontmatter), using this format:

```markdown
## [$NEW_VERSION] - $(date +%Y-%m-%d)

### Added
- [New features added]

### Changed
- [Changes to existing functionality]

### Fixed
- [Bug fixes]

---

```

Guidelines:
- Only include sections (Added/Changed/Fixed/Removed) that have actual changes
- Focus on user-facing changes, not internal refactoring
- Keep descriptions concise but informative
- If this is a patch release with minor changes, a simple list is fine

### 3. Verify the changelog builds

After editing, verify the website builds:
```bash
cd website && npm run build
```

## Output

Provide a brief summary of the changelog entry you added.
PROMPT_EOF
)

# Substitute variables into the prompt
CLAUDE_PROMPT="${CLAUDE_PROMPT//\$CURRENT_VERSION/$CURRENT_VERSION}"
CLAUDE_PROMPT="${CLAUDE_PROMPT//\$NEW_VERSION/$NEW_VERSION}"
CLAUDE_PROMPT="${CLAUDE_PROMPT//\$BUMP_TYPE/$BUMP_TYPE}"
CLAUDE_PROMPT="${CLAUDE_PROMPT//\$(date +%Y-%m-%d)/$(date +%Y-%m-%d)}"

echo "=========================================="
echo "Claude will now update the changelog."
echo "This runs with --dangerously-skip-permissions to avoid prompts."
echo "=========================================="
echo ""

# Run Claude with -p (non-interactive, auto-exits) and skip permission prompts
cd "$REPO_ROOT"
claude -p "$CLAUDE_PROMPT" --dangerously-skip-permissions

# =============================================================================
# Commit and push the version bump
# =============================================================================

echo ""
echo "Committing version bump and changelog..."
echo ""

cd "$REPO_ROOT"

# Stage all changes (csproj and changelog)
git add -A

# Create commit with version bump message
git commit -m "chore: bump version to $NEW_VERSION

- Updated version in src/SelfMX.Api/SelfMX.Api.csproj
- Updated changelog"

echo ""
echo "Pushing to remote..."
echo ""

# Check GitHub authentication before pushing
if ! gh auth status &>/dev/null; then
    echo "Warning: Not authenticated with GitHub CLI."
    echo "Run 'gh auth login' to authenticate, then push manually with 'git push'."
    echo ""
    echo "=========================================="
    echo "Version bump complete: $CURRENT_VERSION -> $NEW_VERSION"
    echo "Changes committed locally but NOT pushed."
    echo "=========================================="
else
    # Push to remote
    git push

    echo ""
    echo "=========================================="
    echo "Version bump complete: $CURRENT_VERSION -> $NEW_VERSION"
    echo "Changes committed and pushed to remote."
    echo "=========================================="
fi

# =============================================================================
# Ask user if they want to publish
# =============================================================================

echo ""
read -p "Do you want to publish this version? This will run selfmx-push.sh (y/N): " PUBLISH_RESPONSE

case "$PUBLISH_RESPONSE" in
    [yY]|[yY][eE][sS])
        echo ""
        echo "Running selfmx-push.sh to publish..."
        echo ""
        "$SCRIPT_DIR/selfmx-push.sh"
        ;;
    *)
        echo ""
        echo "Skipping publish. Run './scripts/selfmx-push.sh' manually when ready."
        ;;
esac
