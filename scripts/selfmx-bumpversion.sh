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

# Check for uncommitted changes (staged or unstaged)
if ! git diff --quiet || ! git diff --cached --quiet; then
    echo "Error: You have uncommitted changes."
    echo "Please commit or stash your changes before bumping the version."
    echo ""
    git status --short
    exit 1
fi

# Check for untracked files (excluding common ignored patterns)
UNTRACKED=$(git ls-files --others --exclude-standard)
if [[ -n "$UNTRACKED" ]]; then
    echo "Error: You have untracked files."
    echo "Please commit, stash, or add to .gitignore before bumping the version."
    echo ""
    echo "$UNTRACKED"
    exit 1
fi

echo "Git state: clean (all changes committed)"
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
# Commit and push the version bump
# =============================================================================

echo ""
echo "Committing version bump..."
echo ""

cd "$REPO_ROOT"

# Stage the changed file
git add "$CSPROJ"

# Create commit with version bump message
git commit -m "chore: bump version to $NEW_VERSION"

echo ""
echo "Pushing to remote..."
echo ""

# Push to remote
git push

echo ""
echo "=========================================="
echo "Version bump complete: $CURRENT_VERSION -> $NEW_VERSION"
echo "Changes committed and pushed to remote."
echo "=========================================="

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
