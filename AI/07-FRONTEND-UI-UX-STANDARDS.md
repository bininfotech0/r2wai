# Frontend / UI / UX Standards

## Product UX goal

> Simple like a modern AI product + powerful like an enterprise control plane.

## Primary navigation

```text
Home
Agents
Connections
Knowledge
Automations
Publish
Activity
Settings
```

## UX principles

- progressive disclosure
- clear hierarchy
- whitespace
- restrained visual language
- accessible contrast
- responsive behavior
- keyboard navigation
- meaningful loading states
- meaningful empty states
- useful error states
- no fake data
- no unnecessary configuration on first screen

## Home

Home should be a command center:

```text
Greeting
What do you want your AI to do?
Continue where you left off
Needs attention
Agents
Recent activity
Platform health
```

## Agent detail

Preferred tabs:

```text
Overview
Configure
Knowledge
Tools
Test
Versions
Activity
```

## Connection UX

```text
Connect
→ Discover
→ Review
→ Test
→ Activate
```

## Playground

Provide:
- conversation
- citations
- tool activity
- execution status
- errors
- latency

Do not expose private chain-of-thought.

## Design system

Reuse the current repository's design system.

Do not create a second component library without an explicit architecture decision.

Preferred reusable components include concepts such as:
- AppShell
- Sidebar
- WorkspaceSwitcher
- GlobalSearch
- CommandPalette
- PageHeader
- AgentCard
- ConnectionCard
- ActivityTimeline
- EmptyState
- ErrorState
- Skeleton
- DataTable
- AgentComposer
- ChatComposer
- ExecutionTrace
- PublishChecklist
- ConnectionWizard

Use existing equivalents when they already exist.

## Accessibility

Target WCAG 2.2 AA principles where applicable:
- keyboard access
- semantic controls
- focus visibility
- contrast
- labels
- error messaging
- screen-reader support

## UI implementation

Prefer:

```text
API
→ Typed client
→ View model
→ Component
```

Never hardcode production data merely to make the UI look complete.
