# Spine Runtime 4.2 Source

This directory vendors the official `spine-csharp` runtime source for the V42 adapter.

The source files keep the Esoteric Software Spine Runtimes license header. Their namespace is mechanically rewritten to
`AssestBundleTools.Infrastructure.ThirdParty.Spine.V42` so future runtime versions can coexist without leaking runtime
types outside Infrastructure.

Runtime objects must remain behind Infrastructure adapters. Application and Presentation code should consume only the
runtime-neutral DTOs defined in `AssestBundleTools.Application`.
