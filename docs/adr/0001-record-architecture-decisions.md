# 0001. Record architecture decisions

- Status: Accepted
- Date: 2026-09-23

## Context

Cadence is a public, long-lived codebase built one milestone at a time. Readers of the repository, and future maintainers including the author, need to understand not just *what* the architecture is but *why* it ended up that way. Commit messages explain individual changes, but not the decisions that span many commits.

## Decision

Significant decisions are recorded as lightweight Architecture Decision Records in `docs/adr/`, numbered sequentially, using the template in [README.md](README.md). A decision is "significant" if reversing it later would be expensive: technology choices, structural patterns, data model conventions, and deployment topology.

ADRs are immutable once accepted. A changed decision gets a new ADR that supersedes the old one.

## Consequences

- The reasoning behind the architecture is reviewable in pull requests alongside the code that implements it.
- There is a small writing cost per decision, which is kept low by the short template.
