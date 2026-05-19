# AssestBundleTools Agent Instructions

This file is the repository-level contract for Codex and other coding agents working in this repo.

Keep it concise and stable. Use it as the default rule set, then follow the linked design docs for area-specific detail. If a subdirectory later adds its own `AGENTS.md`, treat it as an additive, narrower-scope override for that subtree.

## 1. Read First

Before making non-trivial changes, read the docs that match the scope:

- Design doc index and current phase: [`docs/design/README.md`](docs/design/README.md)
- Architecture and layering: [`docs/design/code-architecture-guidelines.md`](docs/design/code-architecture-guidelines.md)
- Current deviation baseline: [`docs/design/implementation-deviation-register.md`](docs/design/implementation-deviation-register.md)
- Workspace model and persistence: [`docs/design/workspace-management.md`](docs/design/workspace-management.md)
- Recoverable workspace operations: [`docs/design/workspace-recoverable-operation-records.md`](docs/design/workspace-recoverable-operation-records.md)
- Build and debug commands: [`docs/design/debug-setup.md`](docs/design/debug-setup.md)
- Roadmap and current phase: [`docs/design/project-build-roadmap.md`](docs/design/project-build-roadmap.md)
- Error model: [`docs/design/error-handling-guidelines.md`](docs/design/error-handling-guidelines.md)
- Test ownership and current regression scope: [`tests/README.md`](tests/README.md)
- UI structure and constraints: [`docs/design/ui-final-spec.md`](docs/design/ui-final-spec.md)

Do not guess rules from local code alone when the docs already define them.

## 2. Project Snapshot

AssestBundleTools is a WinUI desktop tool for browsing, previewing, extracting, replacing, and eventually rebuilding Unity AssetBundle content through a workspace-based workflow.

Current direction:

- The repo is already on the real-reader / real-cache / real-workspace path.
- Changes should preserve the completed M9 direction: stable import recognition, diagnostics, cache reuse/invalidation, dependency reading, and preview pipeline boundaries.
- The next mainline direction is extraction/replacement workflow smoothness, followed by save/rebuild output planning and implementation.
- Rich preview work must continue through the documented preview pipeline, not through ad hoc UI-side reads.

## 3. Non-Negotiable Architecture Rules

### 3.1 Layering

Respect the dependency direction:

- `Presentation.WinUI` -> `Application`
- `Infrastructure` -> `Application` + `Domain`
- `Application` -> `Domain`
- `Domain` remains the innermost layer

Do not introduce reverse dependencies for convenience.

### 3.2 Composition Root Exception

`Presentation.WinUI` may keep a direct project reference to `Infrastructure` only for the controlled composition root boundary under `src/AssestBundleTools.Presentation.WinUI/Composition/`.

Outside `Composition/`, Presentation code must not directly reference `AssestBundleTools.Infrastructure` namespaces or concrete types, including `App.xaml.cs`, regular services, views, and viewmodels.

### 3.3 Workspace Context Boundary

`WorkspaceContextService` must remain a narrow Presentation coordinator.

Do not move recent-workspace restore, availability semantics, last-active workspace restore request derivation, or workspace catalog persistence orchestration back into WinUI services. Those semantics should stay behind `Application.Workspace.Services` abstractions.

### 3.4 UI / Application / Localization Boundaries

- `View` and `ViewModel` should not directly perform bundle parsing, file traversal, cache IO, direct preview generation, or heavy business decision logic.
- `Application` must depend on abstractions, not concrete file IO, window objects, WinUI controls, or serialization details.
- User-visible text must not be hardcoded in XAML or C#.
- `Presentation` formats localized user text; `Application` returns stable semantics; `Infrastructure` returns raw data or structured diagnostics.
- Expected failures should prefer `Result` / `AppError`; exceptions are for unexpected faults.
- Raw external tool `stdout` / `stderr` or result-file messages may appear only as diagnostic fallback/detail, never as the app's primary user prompt.

## 4. ViewModel and Collaboration Rules

- Regular handwritten business/UI classes should usually stay within `200-500` lines.
- Page-level facade/orchestrator viewmodels have a soft limit of `600` lines.
- `800+` lines are split-required transitional state; `1000+` lines are not acceptable as steady state.
- Resource catalogs, localization key catalogs, and simple constant catalogs are exempt from line-count splitting by default; keep them centralized unless there is a clear maintainability reason beyond file length.
- If a current change pushes a touched handwritten file above a documented threshold, or leaves a touched oversize file in place, do not only report it; continue the same change with the required split/refactor when the docs already define a clear direction.
- Only defer the structural fix when the correct split path is genuinely ambiguous or carries a meaningful product/architecture tradeoff that the docs do not already resolve; in that case, stop and ask the user instead of guessing.
- Prefer explicit collaborator types such as `Coordinator`, `Presenter`, `Loader`, `Formatter`, `Mapper`, `Resolver`, and `ActionHandler`.
- Parent/page viewmodels may compose child viewmodels.
- Avoid sibling concrete coupling, child-to-parent concrete coupling, and cyclic viewmodel graphs.

For `BundleExplorer` specifically:

- New page bindings and shared page state should prefer child state hosts or explicit coordinators.
- Do not re-grow parent compatibility passthrough properties, bridge layers, or sibling concrete coupling for convenience.
- After extracting child state hosts or collaborators, do not let future changes quietly re-aggregate their responsibilities back into the parent/page viewmodel.

## 5. Workspace, Browsing, and Preview Rules

- Workspace metadata, runtime session state, and page-level temporary state must stay distinct.
- Bundle tabs and asset document tabs are page-level browsing state, not new workspace entities.
- Filtering/searching must not silently destroy already-open asset document context.
- Prefer existing narrow Application gateways and explicit coordinators/presenters; do not reintroduce aggregate Presentation facades or total-entry services for convenience.
- Preview loading must go through the Application preview pipeline and cache model, not direct UI-side file reads.
- Cache invalidation must remain structured and explainable.
- Workspace open/activate flows must preserve the `pending` pre-registration -> successful promotion -> failure rollback model; do not revert to "register only after successful load".

## 6. Testing and Verification

- Stable documented contracts, confirmed regressions, and high-risk paths should gain tests in the same change.
- Skeleton, placeholder, or transitional flows should only test behavior that is implemented and documented today.
- Replacement and save chains still contain partial skeleton behavior, so tests should stay focused on the current documented contract until real write-path, conflict, cancellation, rollback, and final persistence semantics are implemented and documented.
- Do not use tests to bypass architecture boundaries.
- Detailed test project ownership and current regression scope live in [`tests/README.md`](tests/README.md).
- Prefer Visual Studio `MSBuild` for command-line verification; see [`docs/design/debug-setup.md`](docs/design/debug-setup.md).
- Verify the affected flow when practical instead of stopping at compilation.

## 7. Editing Workflow Expectations

Before editing:

- Inspect the relevant code path and matching design docs.
- Prefer `rg` / `rg --files` for searching.
- Check whether the logic already exists elsewhere and can be reused.

While editing:

- Keep responsibilities clear.
- When creating or moving directories, follow the repo-wide naming and placement rules in [`docs/design/code-architecture-guidelines.md`](docs/design/code-architecture-guidelines.md); do not create new generic dumping folders.
- Remove dead or superseded code promptly.
- Avoid convenience hacks that break layering.
- Add concise Chinese comments only when intent, boundary, or non-obvious design needs explanation.
- Do not hardcode user-visible strings.

After editing:

- Run a post-change compliance review on the touched area before close-out.
- Check touched handwritten files for line-count thresholds, responsibility drift, and whether a collaborator split is now required.
- Check touched code against documented repository constraints such as dependency direction, directory placement, localization boundaries, preview/workspace rules, and Presentation/Infrastructure separation.
- If a documented constraint is violated and the fix direction is already defined by repo docs, continue refactoring in the same change instead of reporting-and-deferring.
- If the correct fix is not clearly defined by the docs, or there are multiple non-obvious paths with different tradeoffs, stop and discuss it with the user before inventing a new rule in code.
- Update relevant design docs when behavior, constraints, or implementation status changes.
- For phased work, sync current progress, what remains out of scope, and recommended next steps.
- For user-requested bug fixes, UI tweaks, or discussion-driven adjustments, align with the user before promoting the conclusion into long-lived design docs.

## 8. Git and Change Safety

- Never revert user changes unless explicitly asked.
- Do not clean up unrelated dirty files.
- Keep commits focused and architecture-safe.
- When a distinct modification task is clearly completed, create one focused git commit for that task before close-out.
- Do not commit unfinished, exploratory, or inconclusive work; for example, a bug investigation or fix that has not reached a clear completion point should remain uncommitted.
- If you introduce a temporary transitional structure during refactor, make the transition explicit and keep the repo buildable.

## 9. When Unsure

Prefer the option that:

1. preserves documented layer boundaries,
2. keeps state ownership explicit,
3. improves reuse instead of duplicating similar logic,
4. reduces long-term viewmodel growth,
5. keeps user-visible behavior localizable and diagnosable.

If code and docs disagree, reconcile deliberately against the design docs instead of blindly following local code.

If the docs do not define the behavior clearly, or the change touches an undefined boundary with meaningful product or architecture impact, stop and discuss it with the user before inventing a new rule in code.
