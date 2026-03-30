# Content Pipeline

## Directory Structure

- content/abilities
- content/npcs
- content/items
- content/quests
- content/zones

## Format

- JSON source files are the source of truth.
- IDs should be lowercase and stable.
- References should use IDs, not file paths.

## Validation Rules

- Every content record must have an id.
- No duplicate IDs per content type.
- Unknown references should fail validation.