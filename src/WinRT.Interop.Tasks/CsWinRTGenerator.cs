﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace WindowsRuntime.Interop.Tasks;

/// <summary>
/// The custom MSBuild task that invokes the 'cswinrtgen' tool.
/// </summary>
public sealed class CsWinRTGenerator : ToolTask
{
    /// <summary>
    /// The name of the generated interop assembly.
    /// </summary>
    private const string InteropAssemblyName = "WinRT.Interop.dll";

    /// <summary>
    /// Gets or sets the paths to assembly files that are reference assemblies, representing
    /// the entire surface area for compilation. These assemblies are the full set of assemblies
    /// that will contribute to the interop .dll being generated.
    /// </summary>
    [Required]
    public ITaskItem[]? ReferenceAssemblyPaths { get; set; }

    /// <summary>
    /// Gets or sets the path to the output assembly that was produced by the build (for the current project).
    /// </summary>
    /// <remarks>
    /// This property is an array, but it should only ever receive a single item.
    /// </remarks>
    [Required]
    public ITaskItem[]? OutputAssemblyPath { get; set; }

    /// <summary>
    /// Gets or sets the directory where the generated interop assembly will be placed.
    /// </summary>
    /// <remarks>If not set, the same directory as <see cref="OutputAssemblyPath"/> will be used.</remarks>
    public string? InteropAssemblyDirectory { get; set; }

    /// <summary>
    /// Gets or sets the directory where the debug repro will be produced.
    /// </summary>
    /// <remarks>If not set, no debug repro will be produced.</remarks>
    public string? DebugReproDirectory { get; set; }

    /// <summary>
    /// Gets or sets the tools directory where the 'cswinrtgen' tool is located.
    /// </summary>
    [Required]
    public string? CsWinRTToolsDirectory { get; set; }

    /// <summary>
    /// Gets or sets whether to use <c>Windows.UI.Xaml</c> projections.
    /// </summary>
    /// <remarks>If not set, it will default to <see langword="false"/> (i.e. using <c>Microsoft.UI.Xaml</c> projections).</remarks>
    public bool UseWindowsUIXamlProjections { get; set; } = false;

    /// <summary>
    /// Gets whether to validate the assembly version of <c>WinRT.Runtime.dll</c>, to ensure it matches the generator.
    /// </summary>
    public required bool ValidateWinRTRuntimeAssemblyVersion { get; init; } = true;

    /// <summary>
    /// Gets or sets the maximum number of parallel tasks to use for execution.
    /// </summary>
    /// <remarks>If not set, the default will match the number of available processor cores.</remarks>
    public int MaxDegreesOfParallelism { get; set; } = -1;

    /// <summary>
    /// Gets the resulting generated interop .dll item.
    /// </summary>
    [Output]
    public ITaskItem? InteropAssemblyPath { get; private set; }

    /// <inheritdoc/>
    protected override string ToolName => "cswinrtgen.exe";

    /// <summary>
    /// Gets the effective item spec for the output assembly.
    /// </summary>
    private string EffectiveOutputAssemblyItemSpec => OutputAssemblyPath![0].ItemSpec;

    /// <summary>
    /// Gets the effective directory where the generated interop assembly will be placed.
    /// </summary>
    private string EffectiveGeneratedAssemblyDirectory => InteropAssemblyDirectory ?? Path.GetDirectoryName(EffectiveOutputAssemblyItemSpec)!;

    /// <summary>
    /// Gets the effective path of the produced interop assembly.
    /// </summary>
    private string EffectiveGeneratedAssemblyPath => Path.Combine(EffectiveGeneratedAssemblyDirectory, InteropAssemblyName);

    /// <inheritdoc/>
    [MemberNotNullWhen(true, nameof(InteropAssemblyPath))]
    public override bool Execute()
    {
        // If the tool execution fails, we will not have a generated interop .dll path
        if (!base.Execute())
        {
            InteropAssemblyPath = null;

            return false;
        }

        // Return the generated interop assembly path as an output item
        InteropAssemblyPath = new TaskItem(EffectiveGeneratedAssemblyPath);

        return true;
    }

    /// <inheritdoc/>
    [MemberNotNullWhen(true, nameof(ReferenceAssemblyPaths))]
    [MemberNotNullWhen(true, nameof(OutputAssemblyPath))]
    [MemberNotNullWhen(true, nameof(CsWinRTToolsDirectory))]
    protected override bool ValidateParameters()
    {
        if (!base.ValidateParameters())
        {
            return false;
        }

        if (ReferenceAssemblyPaths is not { Length: > 0 })
        {
            Log.LogWarning("Invalid 'ReferenceAssemblyPaths' input(s).");

            return false;
        }

        if (OutputAssemblyPath is not { Length: 1 })
        {
            Log.LogWarning("Invalid 'OutputAssemblyPath' input.");

            return false;
        }

        if (InteropAssemblyDirectory is not null && !Directory.Exists(InteropAssemblyDirectory))
        {
            Log.LogWarning("Generated assembly directory '{0}' does not exist.", InteropAssemblyDirectory);

            return false;
        }

        if (DebugReproDirectory is not null && !Directory.Exists(DebugReproDirectory))
        {
            Log.LogWarning("Debug repro directory '{0}' does not exist.", DebugReproDirectory);

            return false;
        }

        if (CsWinRTToolsDirectory is null || !Directory.Exists(CsWinRTToolsDirectory))
        {
            Log.LogWarning("Tools directory '{0}' does not exist.", CsWinRTToolsDirectory);

            return false;
        }

        if (MaxDegreesOfParallelism is 0 or < -1)
        {
            Log.LogWarning("Invalid 'MaxDegreesOfParallelism' value. It must be '-1' or greater than '0' (but was '{0}').", MaxDegreesOfParallelism);

            return false;
        }

        return true;
    }

    /// <inheritdoc/>
    [SuppressMessage("Style", "IDE0072", Justification = "We always use 'x86' as a fallback for all other CPU architectures.")]
    protected override string GenerateFullPathToTool()
    {
        // The tool is inside an architecture-specific subfolder, as it's a native binary
        string architectureDirectory = RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.X64 => "win-x64",
            Architecture.Arm64 => "win-arm64",
            _ => "win-x86"
        };

        return Path.Combine(CsWinRTToolsDirectory!, architectureDirectory, ToolName);
    }

    /// <inheritdoc/>
    protected override string GenerateResponseFileCommands()
    {
        StringBuilder args = new();

        IEnumerable<string> referenceAssemblyPaths = ReferenceAssemblyPaths!.Select(static path => path.ItemSpec);
        string referenceAssemblyPathsArg = string.Join(",", referenceAssemblyPaths);

        AppendResponseFileCommand(args, "--reference-assembly-paths", referenceAssemblyPathsArg);
        AppendResponseFileCommand(args, "--output-assembly-path", EffectiveOutputAssemblyItemSpec);
        AppendResponseFileCommand(args, "--generated-assembly-directory", EffectiveGeneratedAssemblyDirectory);

        // The debug repro directory is optional, and might not be set
        if (DebugReproDirectory is not null)
        {
            AppendResponseFileCommand(args, "--debug-repro-directory", DebugReproDirectory);
        }

        AppendResponseFileCommand(args, "--use-windows-ui-xaml-projections", UseWindowsUIXamlProjections.ToString());
        AppendResponseFileCommand(args, "--validate-winrt-runtime-assembly-version", ValidateWinRTRuntimeAssemblyVersion.ToString());
        AppendResponseFileCommand(args, "--max-degrees-of-parallelism", MaxDegreesOfParallelism.ToString());

        return args.ToString();
    }

    /// <summary>
    /// Appends a command line argument to the response file arguments, with the right format.
    /// </summary>
    /// <param name="args">The command line arguments being built.</param>
    /// <param name="commandName">The command name to append.</param>
    /// <param name="commandValue">The command value to append.</param>
    private static void AppendResponseFileCommand(StringBuilder args, string commandName, string commandValue)
    {
        _ = args.Append($"{commandName} ").AppendLine(commandValue);
    }
}
