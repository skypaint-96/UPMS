# UI/UX Review Summary

This pass focused on improving the existing Blazor Server UI without changing the underlying workflows or routes.

## What was improved

### Navigation and structure
- Refreshed the main layout with stronger branding, clearer active-state navigation, and a sticky header.
- Added a skip link for keyboard users.
- Added a more informative footer and aligned page spacing.

### Visual hierarchy
- Introduced reusable page headers, surface cards, stat cards, helper text, and empty-state styling.
- Improved spacing, grouping, and scanning across dashboards, forms, and data-heavy pages.

### Core workflow pages
- **Home**: clearer dashboard summary, quick actions, recent snapshot visibility, and next-step guidance.
- **Tickets**: better search setup, clearer filter panel, and more readable results area.
- **Ticket detail**: stronger point-in-time context, more readable field tables, and clearer change history.
- **Snapshots / snapshot detail**: improved filtering, summary badges, and ticket-list presentation.
- **Upload**: clearer upload instructions, better file feedback, and more prominent template download actions.
- **Report Store / Report Templates**: improved plugin selection, parameter presentation, result handling, and template guidance.
- **ITSM Sources / ITSM Source Detail**: improved management flow, table readability, and field-mapping setup guidance.

### Tables and forms
- Improved responsive table wrappers and row readability.
- Improved field labels, helper text, section grouping, and empty states.
- Preserved existing `data-testid` hooks used by automated tests.

## Constraints
- The container environment used for this update does **not** include the .NET SDK or Docker, so I could not run a local build or Playwright test pass here.
- Changes were therefore kept focused on Razor markup, layout structure, and CSS, while preserving routes, headings, and test selectors where possible.

## Files with the biggest UI updates
- `src/UPMS.Web/wwwroot/app.css`
- `src/UPMS.Web/Components/Layout/MainLayout.razor`
- `src/UPMS.Web/Components/Layout/MainLayout.razor.css`
- `src/UPMS.Web/Components/Pages/*.razor`
- `src/UPMS.Web/Components/Shared/TicketTable.razor`
- `src/UPMS.Web/Components/Shared/ReportParameterForm.razor`

