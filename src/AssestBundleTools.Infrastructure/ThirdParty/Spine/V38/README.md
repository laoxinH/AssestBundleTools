# Spine Runtime 3.8 Source

This directory vendors the official `spine-csharp` runtime source from the upstream `3.8` branch for the V38 adapter.

The source files keep the Esoteric Software Spine Runtimes license header. Their namespace is mechanically rewritten to
`AssestBundleTools.Infrastructure.ThirdParty.Spine.V38` so multiple runtime versions can coexist without leaking runtime
types outside Infrastructure.

Runtime objects must remain behind Infrastructure adapters. Application and Presentation code should consume only the
runtime-neutral DTOs defined in `AssestBundleTools.Application`.
