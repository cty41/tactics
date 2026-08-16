# Third-Party Notices

Tactics depends on the following third-party projects. These components remain
under their own licenses; they are not relicensed by the project's
Apache-2.0 or CC BY 4.0 grants.

| Component | Version range or locked version | License | Use |
|---|---:|---|---|
| Godot Engine / GodotSharp / Godot.NET.Sdk | 4.7.1 | MIT | Engine and C# SDK |
| .NET SDK | 9.0.312 | MIT and component notices | Build/runtime toolchain |
| NUnit | 4.3.2 | MIT | Core/Application/Oracle tests |
| NUnit3TestAdapter | 4.6.0 | MIT | Test discovery |
| Microsoft.NET.Test.Sdk | 17.14.1 | MIT | Test host |
| GdUnit4 API | 5.1.0-rc5 | MIT | Godot test API |
| GdUnit4 test adapter | 3.1.1 | MIT | Godot test host integration |
| Newtonsoft.Json | 13.0.3 (transitive) | MIT | Test infrastructure dependency |
| commander | 12.1.0 | MIT | Gameplay Spec CLI |
| js-yaml | 4.1.0 | MIT | Gameplay Spec YAML parsing |
| zod | 3.24.2 | MIT | Gameplay Spec validation |
| TypeScript | 5.5.3 | Apache-2.0 | Gameplay Spec compiler |
| Node.js type declarations | locked by package-lock | MIT | TypeScript development |
| PyYAML | >=6.0,<7.0 | MIT | OKF YAML parsing |

The authoritative versions are the checked-in NuGet and npm lockfiles. The
public-release verifier emits a machine-readable dependency report and fails
when an undeclared direct dependency is introduced.

Godot, .NET, NUnit, GdUnit4, Node.js, TypeScript, PyYAML, and their respective
names and logos are the property of their respective owners.
