# Little Castle PBSkyURP Unity 6000.5 compatibility port

Source repository: https://github.com/jiaozi158/UnityPhysicallyBasedSkyURP
Original commit: f8f1f3fbc4708f378252b6188ce416a57ccd06af
License: MIT (original LICENSE.md preserved).

One code file is changed: Runtime/PhysicallyBasedSkyURP.cs.
Five pairs of OnCameraSetup/Execute legacy overrides are compiled only on Unity versions before 6000.4, because the Unity 6000.5 URP API no longer exposes these overrides. The existing five RecordRenderGraph implementations remain unchanged.

Unity recognizes this embedded Packages/ directory before the original pinned Git dependency listed in Packages/manifest.json. Delete the embedded folder in an isolated test copy to revert to upstream. Never modify PackageCache.

The LittleCastle.Runtime and LittleCastle.Editor assembly definition files have explicit references to PBSkyURP and com.cqf.urpvolumetricfog.runtime.

Source-only patch; Unity C# and shader compilation, play mode, visual results and performance are NOT yet tested. The user map and production renderer are not modified. See docs/features/optional-atmosphere-pbrsky-volumetric-20261104-ru.md.
