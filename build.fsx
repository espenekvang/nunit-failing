#r @"tools/FAKE/tools/FakeLib.dll"
#r @"tools/FAKE/tools/Fake.FluentMigrator.dll"
#r @"tools/Nuget.Core/lib/net40-Client/NuGet.Core.dll"

open Fake
open Fake.Testing.NUnit3

let appName = "NUnit.Failing"

//------------------------------------------------------------------------------
// Variables
//------------------------------------------------------------------------------

let sourceDir = __SOURCE_DIRECTORY__
let buildDir = sourceDir @@ @"\build"
let testOutput = sourceDir @@ @"\testresults"
let artifactDir = sourceDir @@ @"\artifacts"

let runningOnBuildServer =
    match buildServer with
    | LocalBuild -> true // SWITCH TO FALSE BEFORE MERGING
    | _ -> true // | TeamFoundation | TeamCity | AppVeyor etc

let version =
    match buildServer with
    | TeamFoundation | TeamCity -> buildVersion
    | LocalBuild -> "1.0.0-local"
    | _ -> environVarOrDefault "version" "1.0.0"
let buildMode = if runningOnBuildServer then "Release" else "Debug"
let buildOptimize = if runningOnBuildServer then "True" else "False"

let findDllInBuildFolder dllGlob =
    sprintf "%s/%s" (if runningOnBuildServer then buildDir else sprintf "./src/**/bin/%s" buildMode) dllGlob

//------------------------------------------------------------------------------
// Variables
//------------------------------------------------------------------------------

// TestCategories can be used in TestFilters (MSTest) to exclude tests:
// TestCategory!=IgnoreOnVSTSBecauseOfNoAccessToOnPremResource&TestCategory!=NeedsAadCertificateInLocalMachineStore
let setNUnit3Params testResultsFile (defaults : NUnit3Params) =
    let output = sprintf "%s\\%s" testOutput testResultsFile
    { defaults with
        ResultSpecs = [output]
        TeamCity = (buildServer = TeamCity)
        Params = if runningOnBuildServer then "exclude=IgnoreOnVSTSBecauseOfNoAccessToOnPremResource;exclude=NeedsAadCertificateInLocalMachineStore" else ""
        ToolPath = "./tools/Nunit.ConsoleRunner/tools/nunit3-console.exe"  }

let setMsBuildParams (defaults : MSBuildParams) =
    { defaults with
        Verbosity = Some MSBuildVerbosity.Minimal
        ToolsVersion = Some "15.0"
        Properties =
            [
                "Optimize", buildOptimize
                "DebugSymbols", "True"
                "VisualStudioVersion", "15.0"
                "Configuration", getBuildParamOrDefault "buildMode" buildMode
            ] }

MSBuildDefaults <- (setMsBuildParams MSBuildDefaults)

let sln = sprintf "src/%s.sln" appName

//------------------------------------------------------------------------------
// Targets
//------------------------------------------------------------------------------

Target "AssemblyInfo" <| fun _ ->
    ReplaceAssemblyInfoVersionsBulk (!! "src/*/Properties/AssemblyInfo.cs") (fun p ->
        {
            p with
                AssemblyVersion = version
                AssemblyCompany = "If Skadeforsikring"
                AssemblyCopyright = System.DateTime.Now.ToString("yyyy")
        })

Target "Clean" <| fun _ ->
    CleanDirs [buildDir; artifactDir; testOutput]
    !! sln
    |> MSBuild null "Clean" [] |> ignore

Target "RestorePackages" <| fun _ ->
    sln
    |> RestoreMSSolutionPackages (fun p ->
        { p with
            OutputPath = "./src/packages"
            Retries = 4 })

Target "Build" <| fun _ ->
    !! sln
    |> MSBuild null "Build" [
                                if runningOnBuildServer then yield "OutputPath", buildDir
                            ]
    |> ignore

Target "UnitTests" <| fun _ ->
    CreateDir testOutput
    !! (findDllInBuildFolder "*.Tests.Unit.dll")
    |> NUnit3 (setNUnit3Params "UnitTestResults.xml")

type PackageReferenceFile = NuGet.PackageReferenceFile
Target "NuGetPackagesConsolidated" <| fun _ ->
    !! (sprintf "./src/%s*/packages.config" appName)
    -- "**/obj/**/packages.config"
    |> Seq.map PackageReferenceFile
    |> Seq.collect (fun prf -> prf.GetPackageReferences())
    |> Seq.groupBy (fun pr -> pr.Id)
    |> Seq.filter (fun p -> (snd p |> Seq.distinct |> Seq.length) > 1 )
    |> Seq.map (fun p -> fst p , snd p |> Seq.distinct)
    |> function 
        | packages when packages |> Seq.isEmpty -> ()
        | packages -> 
            seq {
                yield "The following packages are not consolidated:"

                for (k,v) in packages do
                    yield (sprintf "    Package: %s Versions: %A" k v)
            
                yield "You need to consolidate packages across the solution:"
                yield "    * Right click on the solution inside VS"
                yield "    * Choose Manage NuGet Packages for Solution"
                yield "    * Choose the Consolidate tab"
                yield "    * Make sure you sync the package versions" }
            |> Seq.iter (printfn "%s")
            failwith "Packages not consolidated"

Target "CI" <| DoNothing

//------------------------------------------------------------------------------
// Dependencies
//------------------------------------------------------------------------------

"Clean"
    ==> "RestorePackages"
    ==> "Build"   

"NuGetPackagesConsolidated" ==> "CI"
"UnitTests"  ==> "CI"

"Build" ==> "UnitTests"

RunTargetOrDefault "UnitTests"