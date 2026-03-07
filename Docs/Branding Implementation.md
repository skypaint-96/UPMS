# Branding Implementation

This project has been restyled to align with the supplied design specification and the provided enterprise design package where practical.

## What changed

- Reworked the global colour tokens to use the supplied neutral, purple and red spectrum.
- Updated navigation, cards, tables, buttons, badges and form controls to follow the same visual language.
- Replaced the previous dark application chrome with a lighter, presentation-friendly shell.
- Added a neutral abstract brand mark in the header instead of any literal company logo.
- Added a reserved header slot that can later be replaced with a client or company identifier.

## Deliberate constraints

- No literal CGI logos or direct CGI naming were added to the UI.
- The Angular component package was reviewed for theme values, but it was not directly embedded because this application is a Blazor application rather than an Angular application.
- Source Sans Pro is referenced in the CSS stack, but font binaries were not embedded into this project package.

## Easy future additions

To add formal branding later, the simplest entry points are:

1. `src/UPMS.Web/Components/Layout/MainLayout.razor`
2. `src/UPMS.Web/Components/Layout/MainLayout.razor.css`
3. `src/UPMS.Web/wwwroot/app.css`

The reserved header element is:

- `.brand-slot`

The abstract placeholder mark is:

- `.brand-mark`
- `.brand-mark-corner`

The main theme tokens are declared in:

- `:root` within `src/UPMS.Web/wwwroot/app.css`

## Assumptions used

- Typography should follow Source Sans Pro styling where available.
- Accent behaviour should prioritise deep purple for primary UI actions and use the red to purple gradient as a highlight rather than a constant full-surface treatment.
- Since logos were excluded, abstract geometry was used to keep the layout visually compatible with the design direction without creating explicit company identification.
