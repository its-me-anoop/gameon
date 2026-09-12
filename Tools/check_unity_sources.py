#!/usr/bin/env python3
"""Compile Unity C# and run engine-independent NUnit tests without opening Editor.

This checks C# contracts against an installed Editor's assemblies. It does not
import assets, run Unity lifecycle/rendering, build IL2CPP, or exercise Apple APIs.
Profile JSON/file recovery and rendering tests must still run inside Unity.
Import Unity/OrbitOrchard with the matching Editor first so its package assemblies
exist; this checker never opens the Editor or resolves/downloads packages.
"""

import argparse
import os
from pathlib import Path
import shutil
import subprocess
import tempfile
from xml.etree import ElementTree


RUNNER = """using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework.Api;
using NUnit.Framework.Interfaces;
using NUnit.Framework.Internal;
using NUnit.Framework.Internal.Filters;

internal static class RunStandaloneTests
{
    public static int Main(string[] args)
    {
        var runner = new NUnitTestAssemblyRunner(new DefaultTestAssemblyBuilder());
        runner.Load(Assembly.LoadFrom(args[0]), new Dictionary<string, object>());
        TestFilter filter = args.Length > 2
            ? new OrFilter(args.Skip(2).Select(name => (TestFilter)new FullNameFilter(name)).ToArray())
            : TestFilter.Empty;
        var result = runner.Run(TestListener.NULL, filter);
        File.WriteAllText(args[1], result.ToXml(true).OuterXml);
        Console.WriteLine(Path.GetFileName(args[0]) + ": " + result.PassCount + " passed, "
            + result.FailCount + " failed, " + result.SkipCount + " skipped.");
        if (result.FailCount > 0) Console.WriteLine(result.ToXml(true).OuterXml);
        return result.FailCount == 0 && result.PassCount > 0 ? 0 : 1;
    }
}
"""


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--unity-app", type=Path, default=Path(
        "/Applications/Unity/Hub/Editor/6000.3.24f1/Unity.app"))
    parser.add_argument("--output", type=Path, help="Directory for assemblies and NUnit XML")
    parser.add_argument("--package-assemblies", type=Path,
                        help="Imported project ScriptAssemblies directory (defaults to this project's Library)")
    args = parser.parse_args()
    repo = Path(__file__).resolve().parent.parent
    project = repo / "Unity/OrbitOrchard"
    orchard = project / "Assets/OrbitOrchard"
    lifeline = project / "Assets/LittleLifeline"
    clinic = project / "Assets/IdleClinic"
    scripting = args.unity_app / "Contents/Resources/Scripting"
    managed = scripting / "Managed/UnityEngine"
    mono = scripting / "MonoBleedingEdge/bin/mono"
    csc = scripting / "MonoBleedingEdge/lib/mono/4.5/csc.exe"
    netstandard = scripting / "MonoBleedingEdge/lib/mono/4.5/Facades/netstandard.dll"
    xcode = args.unity_app / "Contents/Resources/BuildPipeline/UnityEditor.iOS.Extensions.Xcode.dll"
    nunit = args.unity_app / (
        "Contents/Resources/PackageManager/BuiltInPackages/"
        "com.unity.ext.nunit/net40/unity-custom/nunit.framework.dll")
    for required in (mono, csc, nunit, managed, netstandard, xcode):
        if not required.exists():
            parser.error(f"Required installed Unity component is missing: {required}")
    package_dir = (args.package_assemblies or project / "Library/ScriptAssemblies").resolve()
    # Unity.ugui's asmdef produces UnityEngine.UI.dll, not Unity.ugui.dll.
    # Use this project's resolved packages rather than unrelated template versions.
    packages = [package_dir / "Unity.InputSystem.dll", package_dir / "UnityEngine.UI.dll"]
    for package in packages:
        if not package.is_file():
            parser.error(f"Missing imported package assembly: {package}. "
                         "Import Unity/OrbitOrchard with the matching Editor first, or supply "
                         "--package-assemblies from that imported project. No Editor was launched.")
    output = (args.output or Path(tempfile.mkdtemp(prefix="gameon-csharp-check-"))).resolve()
    output.mkdir(parents=True, exist_ok=True)
    summary = output / "summary.txt"
    summary.write_text("INCOMPLETE: standalone compilation/tests have not finished.\n", encoding="utf-8")
    print(f"Standalone compiler/test artifacts: {output}", flush=True)
    print(f"Imported Unity package references: {package_dir}", flush=True)
    shutil.copy2(nunit, output / nunit.name)
    compiler = [str(mono), str(csc), "-nologo", "-langversion:9"]
    defines = "UNITY_EDITOR,UNITY_EDITOR_OSX,UNITY_IOS,UNITY_6000_0_OR_NEWER,UNITY_INCLUDE_TESTS"
    ios_defines = "UNITY_IOS,UNITY_6000_0_OR_NEWER"
    compiled = []

    def compile_assembly(name, sources, refs=(), symbols=defines):
        sources = sorted(sources)
        if not sources:
            parser.error(f"No source files found for {name}")
        destination = output / (name + ".dll")
        command = compiler + ["-target:library", f"-out:{destination}", f"-define:{symbols}"]
        command += [f"-r:{ref}" for ref in dict.fromkeys(refs)]
        command += [str(source) for source in sources]
        print(f"Compiling {name}", flush=True)
        subprocess.run(command, check=True, cwd=repo)
        compiled.append(name)
        return destination

    engine_refs = sorted(managed.glob("UnityEngine*.dll")) + [netstandard] + packages
    orchard_core = compile_assembly("OrbitOrchard.Core", (orchard / "Scripts/Core").glob("*.cs"))
    lifeline_core = compile_assembly("LittleLifeline.Core", (lifeline / "Core").glob("*.cs"))
    clinic_core = compile_assembly("IdleClinic.Core", (clinic / "Core").glob("*.cs"))
    orchard_sources = [p for p in (orchard / "Scripts").rglob("*.cs") if "Core" not in p.parts]
    lifeline_sources = list((lifeline / "Runtime").rglob("*.cs"))
    orchard_refs = engine_refs + [orchard_core]
    orchard_runtime = compile_assembly("OrbitOrchard.Runtime", orchard_sources, orchard_refs)
    orchard_ios = compile_assembly("OrbitOrchard.Runtime.iOS", orchard_sources, orchard_refs, ios_defines)
    lifeline_refs = engine_refs + [orchard_core, lifeline_core, orchard_runtime]
    lifeline_runtime = compile_assembly("LittleLifeline.Runtime", lifeline_sources, lifeline_refs)
    compile_assembly("LittleLifeline.Runtime.iOS", lifeline_sources,
                     engine_refs + [orchard_core, lifeline_core, orchard_ios], ios_defines)
    clinic_sources = list((clinic / "Runtime").rglob("*.cs"))
    clinic_refs = engine_refs + [clinic_core, orchard_runtime]
    clinic_runtime = compile_assembly("IdleClinic.Runtime", clinic_sources, clinic_refs)
    compile_assembly("IdleClinic.Runtime.iOS", clinic_sources,
                     engine_refs + [clinic_core, orchard_ios], ios_defines)
    compile_assembly("IdleClinic.Runtime.iOS.Development", clinic_sources,
                     engine_refs + [clinic_core, orchard_ios], ios_defines + ",DEVELOPMENT_BUILD")
    editor_refs = (lifeline_refs + [lifeline_runtime, clinic_core, clinic_runtime, xcode]
                   + sorted(managed.glob("UnityEditor*.dll")))
    compile_assembly("OrbitOrchard.Editor", (orchard / "Editor").glob("*.cs"), editor_refs)

    orchard_core_tests = compile_assembly("OrbitOrchard.Core.Tests",
        (orchard / "Tests/EditMode").glob("*.cs"), [orchard_core, nunit])
    orchard_profile_tests = compile_assembly("OrbitOrchard.Profile.Tests",
        (orchard / "Tests/EditMode/Profile").glob("*.cs"), orchard_refs + [orchard_runtime, nunit])
    compile_assembly("OrbitOrchard.PresentationTests", (orchard / "Tests/Editor").glob("*.cs"),
                     orchard_refs + [orchard_runtime, nunit])
    lifeline_core_tests = compile_assembly("LittleLifeline.Core.Tests",
        (lifeline / "Tests/Core").glob("*.cs"), [lifeline_core, nunit])
    lifeline_profile_tests = compile_assembly("LittleLifeline.Profile.Tests",
        (lifeline / "Tests/Profile").glob("*.cs"), lifeline_refs + [lifeline_runtime, nunit])
    compile_assembly("LittleLifeline.PresentationTests", (lifeline / "Tests/Editor").glob("*.cs"),
                     lifeline_refs + [lifeline_runtime, nunit])

    clinic_core_tests = compile_assembly("IdleClinic.Core.Tests",
        (clinic / "Tests/Core").glob("*.cs"), [clinic_core, nunit])
    clinic_profile_tests = compile_assembly("IdleClinic.Profile.Tests",
        (clinic / "Tests/Profile").glob("*.cs"), clinic_refs + [clinic_runtime, nunit])
    compile_assembly("IdleClinic.PresentationTests",
        (clinic / "Tests/Editor").glob("*.cs"), clinic_refs + [clinic_runtime, nunit])

    runner = output / "RunStandaloneTests.cs"
    runner.write_text(RUNNER, encoding="utf-8")
    executable = output / "RunStandaloneTests.exe"
    subprocess.run(compiler + [f"-out:{executable}", f"-r:{nunit}", str(runner)], check=True)
    env = os.environ.copy()
    env["MONO_PATH"] = os.pathsep.join(map(str, (output, managed, scripting / "Managed", package_dir)))
    results = []

    def run_tests(assembly, filename, names=()):
        result_path = output / filename
        subprocess.run([str(mono), str(executable), str(assembly), str(result_path)] + list(names),
                       check=True, env=env, cwd=repo)
        result = ElementTree.parse(result_path).getroot()
        results.append(f"{assembly.stem}: {result.get('passed')} passed, {result.get('failed')} failed "
                       f"({filename})")

    run_tests(orchard_core_tests, "core-nunit-results.xml")
    run_tests(lifeline_core_tests, "lifeline-core-nunit-results.xml")
    run_tests(clinic_core_tests, "clinic-core-nunit-results.xml")
    # Explicit allowlists: these rules use managed state and missing-file defaults.
    # Loading test assemblies is not permission to call Unity native JSON/UI APIs.
    orchard_profile_names = [
        "FirstLaunchHasUsableDefaultsAndNoSaveError",
        "DailyAttemptsNeverChangeClassicBestOrRunCount",
        "HistoryIsCappedAndKeepsTheMostRecentUTCDays",
        "CallerCannotMutateTheRecordedHistoryAfterTheFact",
        "InvalidRunsAreIgnoredAndTotalsCannotWrapNegative",
        "UndatedRecordsAreDiscardedWhileValidZeroScoreRunsRemain",
    ]
    run_tests(orchard_profile_tests, "profile-managed-nunit-results.xml",
        ["OrbitOrchard.App.Tests.OrchardProfileTests." + name for name in orchard_profile_names])
    lifeline_profile_names = [
        "WeeklyIdentityUsesMondayUtcDespiteLocalOffsets",
        "WeeklyOccurrenceRejectsInvalidDatesAndEndsExclusively",
    ]
    run_tests(lifeline_profile_tests, "lifeline-profile-managed-nunit-results.xml",
        ["LittleLifeline.Tests.LifelineProfileTests." + name for name in lifeline_profile_names])
    run_tests(clinic_profile_tests, "clinic-performance-nunit-results.xml",
        ["IdleClinic.Tests.ClinicPerformanceTests." + name for name in (
            "PercentilesUseRecordedFramesIncludingLongHitches",
            "RollingWindowEvictsOnlyTheOldestFrames",
            "InvalidSamplesDoNotPolluteResultsAndSummaryDoesNotConsumeFrames")])
    message = ("PASSED: standalone source compilation and explicitly selected managed tests.\n"
               f"Editor assemblies: {args.unity_app.resolve()}\n"
               f"Imported package assemblies: {package_dir}\n"
               "Compiled: " + ", ".join(compiled) + "\n" + "\n".join(results) + "\n"
               "NOT RUN: Unity Editor tests, native JSON/file recovery, UI/renderer/assets, "
               "IL2CPP, native Apple APIs, and device checks.\n")
    summary.write_text(message, encoding="utf-8")
    print(message, end="")


if __name__ == "__main__":
    main()
