# ZaneTask Design System (Master)

Source of truth for the Blazor UI. Page files in `pages/` override these rules for that page only.
Derived from the `ui-ux-pro-max` skill (palette + typography from `--design-system`, style from
`--domain style`); the suggested "Motion-Driven" style and landing-page pattern were rejected as a
poor fit for a work tool.

## Style
**Minimalism & Swiss** — clean, functional, high contrast, grid-based, essential elements only.
Flat surfaces with 1px borders; shadows only for overlays (dialogs, menus, dragged card).

## Color tokens (defined in `wwwroot/css/app.css`)
| Token | Light | Dark | Use |
|---|---|---|---|
| `--bg` | `#F5F3FF` | `#0F0E1A` | App background |
| `--surface` | `#FFFFFF` | `#18172A` | Cards, dialogs, columns |
| `--surface-muted` | `#EBEFF9` | `#23213A` | Column background, chips, hover |
| `--fg` | `#1E1B4B` | `#E6E8FF` | Primary text |
| `--fg-muted` | `#475569` | `#A8AEC8` | Secondary text (≥4.5:1 on bg and surface) |
| `--border` | `#DCE1F5` | `#302E4A` | Dividers, inputs |
| `--primary` | `#4F46E5` | `#818CF8` | Primary buttons, links, focus ring |
| `--on-primary` | `#FFFFFF` | `#14123A` | Text on primary |
| `--success` | `#047857` | `#34D399` | Done status |
| `--danger` | `#DC2626` | `#F87171` | Destructive actions, errors |

The skill suggested `#6366F1` for primary; it is darkened to `#4F46E5` so white button text passes 4.5:1.

## Typography
Plus Jakarta Sans (400/500/600/700) from Google Fonts with `display=swap`.
Scale: 12 / 14 / 16 / 18 / 24 / 32. Body 16px, line-height 1.5. Headings 600–700.

## Spacing & shape
4px base scale: 4, 8, 12, 16, 24, 32, 48. Radius: 6px controls, 10px cards/dialogs, full for avatars/chips.
Content max-width 1200px (board is full-width with horizontal columns that stack on mobile).

## Motion
Subtle only: 150–200ms ease-out on color/opacity/transform. No entrance choreography.
Everything is disabled under `prefers-reduced-motion: reduce`.

## Icons
Lucide (stroke 2, 16/20px) inlined as SVG via `Components/Icon.razor`. No emoji icons.
Decorative icons next to text get `aria-hidden`; icon-only buttons need `aria-label`.

## Interaction & accessibility rules
- Visible 2px focus ring (`--primary`, 2px offset) on every interactive element.
- Pointer targets ≥ 24×24 CSS px (buttons are 36–40px tall).
- Kanban drag-and-drop always has a non-drag alternative: per-card "Move" menu (left/right column, up/down).
- Priority and status never rely on color alone: icon + text.
- Forms: visible labels, `autocomplete`, correct input types, inline errors, and a focusable
  `role="alert"` error summary after a failed submit. Submit buttons show a spinner and disable while busy.
- Destructive actions (delete task/project, remove member) ask for confirmation and use `--danger`.
- Dialogs use native `<dialog>` (focus trap, Esc to close, returns focus to the trigger).
- Toasts are announced via `aria-live="polite"` and auto-dismiss after 4s.
- Empty states explain what is missing and offer the primary action.
