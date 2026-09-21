using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CodeBrix.Ollama.ModelManager;

namespace CodeBrix.Audio.MusicGeneration.SkyTNT.ModelStager;

/// <summary>
/// Reproduces this repository's shipped model artifact from upstream: the SkyTNT MIDI model's published
/// ONNX pair is pulled at a pinned commit, both graphs are reduced to block-wise four-bit weights by the
/// model library's own managed engine, the result is checked against what was recorded for this machine,
/// and the four files the loader wants are written flat into <c>staging/output</c> with a provenance file
/// beside the repository's LICENSE.
/// </summary>
/// <remarks>
/// <para>
/// NOTHING HAS TO BE INSTALLED. The reduction engine is PINNED TO THE MANAGED ONE and never left to the
/// automatic choice, so no future change of default can pull a Python requirement into a build machine.
/// If a step ever found itself on a Python road the run stops and says so by name.
/// </para>
/// <para>
/// EVERYTHING STAYS INSIDE THE REPOSITORY: the store, the downloads, the temporary folder the reduction
/// lays the 822 MB source graph out in, and the artifact. The default store under the user's home
/// directory is never opened.
/// </para>
/// </remarks>
internal static class Program
{
    /// <summary>
    /// The upstream model, in the store's own name grammar. The tag is a label of this repository's
    /// choosing - the publisher ships several formats from one repository and this pull takes the ONNX
    /// pair - so the revision is named in the options rather than read from the tag.
    /// </summary>
    private const string UpstreamName = "hf.co/skytnt/midi-model-tv2o-medium:onnx-only";

    /// <summary>The Hugging Face repository the files come from.</summary>
    private const string UpstreamRepository = "skytnt/midi-model-tv2o-medium";

    /// <summary>
    /// The commit the pull is PINNED to. A branch moves; a staged artifact that cannot say which bytes it
    /// was made from is not provenance, so the revision is named here and recorded in the provenance file.
    /// </summary>
    private const string UpstreamRevision = "0f8f265d4330f4e46527ac2313200254c5757f5f";

    /// <summary>The name the reduced bundle is stored under.</summary>
    private const string ReducedName = "hf.co/skytnt/midi-model-tv2o-medium:onnx-int4";

    /// <summary>The larger graph, which the MIDI driver runs the decoder from.</summary>
    private const string BaseGraph = "onnx/model_base.onnx";

    /// <summary>The smaller graph, which the MIDI driver runs the token head from.</summary>
    private const string TokenGraph = "onnx/model_token.onnx";

    /// <summary>How many weight values share one scale in the block-wise reduction.</summary>
    private const int BlockSize = 128;

    /// <summary>
    /// Whether each block is quantized symmetrically. It is not: a zero point per block is the tooling's
    /// own default and the setting the artifact of record was made with.
    /// </summary>
    private const bool IsSymmetric = false;

    /// <summary>
    /// What the run needs free before it starts: the published pair, the working layout the reduction
    /// makes of it, the reduced graphs in the store and the copy in the output folder, with room over.
    /// </summary>
    private const long RequiredFreeBytes = 3_758_096_384L;

    /// <summary>How many steps the run reports.</summary>
    private const int StepCount = 5;

    /// <summary>
    /// What the package ships, in the flat layout the loader expects: the two reduced graphs under the
    /// publisher's own file names, with the two configuration files beside them. The publisher's
    /// <c>README.md</c> is pulled and kept in the store but is not part of the artifact.
    /// </summary>
    /// <remarks>
    /// The sizes and hashes are those measured on the machine the artifact of record was staged on.
    /// RE-STAGING TO THESE HASHES ON THIS MACHINE IS THE GATE; bytes are never pinned across platforms,
    /// and MODEL-PROVENANCE.json records which machine they belong to.
    /// </remarks>
    private static readonly ExpectedArtifact[] Expected =
    {
        new ExpectedArtifact(
            "config.json", "config.json", 2_016L,
            "1f393e1e8c630ddc81a976348ee246549d613f0a117dc0622eed49918dfcb511"),
        new ExpectedArtifact(
            "generation_config.json", "generation_config.json", 69L,
            "e367d0feb45ba71e180b151aaf976961ce48b0a70ba8ed47bf7deba0dae273a3"),
        new ExpectedArtifact(
            BaseGraph, "model_base.onnx", 123_950_877L,
            "c772d6c8b5b927ab5d81498075f2c900d4bdbedbed99d4240e918856b62cf272"),
        new ExpectedArtifact(
            TokenGraph, "model_token.onnx", 28_229_659L,
            "e5a7182ad4a72dd74e5422f25a95985dfad2514e2269036b66b63037abfe9227")
    };

    /// <summary>
    /// Runs the staging.
    /// </summary>
    /// <param name="args">The command line.</param>
    /// <returns>0 when the artifact was staged and matched, 1 on a failure, 2 on a mismatch.</returns>
    private static async Task<int> Main(string[] args)
    {
        //THE TEMPORARY DIRECTORY IS REDIRECTED BEFORE THE FIRST CALL INTO THE MODEL LIBRARY. The reduction
        //lays the whole 938 MB bundle out under it and writes the reduced graphs beside that, and /tmp is a
        //small in-memory file system on the machine this is staged on. Only this tool's own path
        //arithmetic runs ahead of it.
        StagingLayout layout;
        try
        {
            layout = StagingLayout.Discover();
            layout.CreateDirectories();
            layout.RedirectTemporaryDirectory();
        }
        catch (InvalidOperationException error)
        {
            Log.Error(error.Message);
            return 1;
        }

        bool cleanAfter = false;
        bool keep = false;
        foreach (string argument in args)
        {
            switch (argument)
            {
                case "--clean-after":
                    cleanAfter = true;
                    break;
                case "--keep":
                    keep = true;
                    break;
                case "--help":
                case "-h":
                    WriteUsage();
                    return 0;
                default:
                    Log.Error("Unknown argument " + argument + ".");
                    WriteUsage();
                    return 1;
            }
        }

        Log.Line("SkyTNT model stager - " + UpstreamName);
        Log.Detail("repository root   " + layout.RepositoryRoot);
        Log.Detail("store             " + layout.StoreDirectory);
        Log.Detail("temporary         " + Path.TrimEndingDirectorySeparator(Path.GetTempPath()));
        Log.Detail("output            " + layout.OutputDirectory);

        var total = Stopwatch.StartNew();
        using var watcher = new StagingWatcher(layout);
        var report = new UsageReport();

        try
        {
            int result = await StageAsync(layout, watcher, report, CancellationToken.None)
                .ConfigureAwait(false);
            total.Stop();
            report.Write(watcher, total.Elapsed);

            if (result == 0)
            {
                Clean(layout, cleanAfter, keep);
                Log.Blank();
                Log.Line("DONE. The artifact is in " + layout.OutputDirectory + ".");
            }

            return result;
        }
        catch (PythonNotAvailableException error)
        {
            return StopOnPython(error);
        }
        catch (PythonModuleNotInstalledException error)
        {
            return StopOnPython(error);
        }
        catch (PythonScriptException error)
        {
            return StopOnPython(error);
        }
        catch (Exception error)
        {
            total.Stop();
            Log.Blank();
            Log.Error(error.GetType().Name + ": " + error.Message);
            Log.Detail("The staging folders are left exactly as they are, so a second run resumes.");
            return 1;
        }
    }

    /// <summary>
    /// Pulls, reduces, copies the artifact out and writes the provenance.
    /// </summary>
    /// <param name="layout">Where everything goes.</param>
    /// <param name="watcher">The watcher that samples what the run costs.</param>
    /// <param name="report">Where the timings and the byte counts are collected.</param>
    /// <param name="cancellationToken">A token that cancels the run.</param>
    /// <returns>0 when the artifact matched what was recorded, 1 on a failure, 2 on a mismatch.</returns>
    private static async Task<int> StageAsync(
        StagingLayout layout,
        StagingWatcher watcher,
        UsageReport report,
        CancellationToken cancellationToken)
    {
        var step = Stopwatch.StartNew();

        Log.Step(1, StepCount, "free space");
        long free = DiskUsage.AvailableFreeBytes(layout.StagingDirectory);
        Log.Detail("needed     " + Log.Bytes(RequiredFreeBytes));
        Log.Detail("available  " + (free < 0 ? "unknown on this platform" : Log.Bytes(free)));
        if (free >= 0 && free < RequiredFreeBytes)
        {
            Log.Error("There is not enough free space to stage this model. Free some and run again.");
            return 1;
        }

        report.Record("free space", step.Elapsed);

        using var store = new ModelStore(new ModelStoreOptions { StoreDirectory = layout.StoreDirectory });

        //PULL, pinned to the commit, and only the files this artifact is made of: the ONNX pair, the two
        //   configurations and the model card. The safetensors weights, the duplicate .bin and the training
        //   logs the same repository ships are left where they are.
        step.Restart();
        Log.Step(2, StepCount, "pull " + UpstreamName + " at " + UpstreamRevision);
        var pullOptions = new PullOptions
        {
            Source = PullSource.HuggingFaceFiles,
            Repository = UpstreamRepository,
            Revision = UpstreamRevision,
            Filter = new FileFilter(
                new[] { "onnx/**", "config.json", "generation_config.json", "README.md" },
                Array.Empty<string>())
        };

        long downloaded = 0;
        var seen = new Dictionary<string, long>(StringComparer.Ordinal);
        DateTime next = DateTime.MinValue;
        await foreach (PullProgress progress in store
            .PullAsync(UpstreamName, pullOptions, cancellationToken)
            .ConfigureAwait(false))
        {
            //A file reports repeatedly as it comes down; only its last count is part of the total.
            if (progress.Digest != null || progress.TotalBytes > 0)
            {
                seen[progress.Status] = progress.CompletedBytes;
            }

            if (DateTime.UtcNow >= next || progress.TotalBytes == 0)
            {
                Log.Detail(Describe(progress));
                next = DateTime.UtcNow + Log.ProgressInterval;
            }
        }

        foreach (KeyValuePair<string, long> file in seen)
        {
            downloaded += file.Value;
        }

        report.DownloadedBytes = downloaded;
        watcher.Sample();
        report.Record("pull", step.Elapsed);

        ModelInfo source = await store.ShowAsync(UpstreamName, cancellationToken).ConfigureAwait(false);
        ResolvedModel resolvedSource = await store.ResolveAsync(UpstreamName, cancellationToken)
            .ConfigureAwait(false);
        Log.Detail("licence    " + (source.License == null ? "none stated" : source.License.LicenseId));
        Log.Detail("files      " + resolvedSource.Files.Count + ", " + Log.Bytes(source.Size));

        //REDUCE. Block-wise four-bit weights over BOTH graphs, through the MANAGED engine, which is
        //   PINNED rather than left to the automatic choice so that no future default can require Python.
        step.Restart();
        Log.Step(3, StepCount, "reduce both graphs to weight-only int4 (managed engine)");
        var reduceOptions = new ReduceOptions
        {
            Mode = ReduceMode.WeightOnlyInt4,
            Engine = ReduceEngine.Managed,
            BlockSize = BlockSize,
            IsSymmetric = IsSymmetric,
            Files = new[] { BaseGraph, TokenGraph },
            OutputName = ReducedName,
            Overwrite = true
        };

        ReduceResult reduced = await store
            .ReduceOnnxAsync(UpstreamName, reduceOptions, Watch(), cancellationToken)
            .ConfigureAwait(false);

        if (reduced.EngineUsed != ReduceEngine.Managed)
        {
            Log.Blank();
            Log.Error("STOPPED: the reduction ran on the " + reduced.EngineUsed + " engine, not the managed"
                + " one it was pinned to. Staging must never need Python.");
            return 1;
        }

        Log.Detail("engine     " + reduced.EngineUsed + " (" + reduced.Tool + " "
            + (reduced.ToolVersion ?? "no version stated") + ")");
        Log.Detail("graphs     " + Log.Bytes(reduced.SourceBytes) + " -> " + Log.Bytes(reduced.ReducedBytes));
        watcher.Sample();
        report.Record("reduce", step.Elapsed);

        //THE ARTIFACT. The loader wants the two graphs and the two configurations in ONE FLAT FOLDER
        //   under the publisher's own file names, so the bundle's blobs are copied out by name rather
        //   than materialized as the tree the bundle records.
        step.Restart();
        Log.Step(4, StepCount, "copy the artifact out and check it");
        ResolvedModel resolved = await store.ResolveAsync(ReducedName, cancellationToken)
            .ConfigureAwait(false);

        var artifacts = new List<ArtifactFile>();
        bool matched = true;
        foreach (ExpectedArtifact wanted in Expected)
        {
            string blob = FindBlob(resolved, wanted.BundlePath);
            string target = Path.Combine(layout.OutputDirectory, wanted.OutputName);
            if (File.Exists(target))
            {
                File.Delete(target);
            }

            File.Copy(blob, target);
            ArtifactFile artifact = ArtifactFile.Measure(target);
            artifacts.Add(artifact);

            bool agrees = artifact.Matches(wanted.Bytes, wanted.Sha256);
            matched &= agrees;
            Log.Detail(artifact.Name);
            Log.Detail("    bytes    " + artifact.Bytes.ToString("N0", CultureInfo.InvariantCulture)
                + "   expected " + wanted.Bytes.ToString("N0", CultureInfo.InvariantCulture));
            Log.Detail("    sha256   " + artifact.Sha256);
            Log.Detail("    expected " + wanted.Sha256 + (agrees ? "   MATCH" : "   MISMATCH"));
        }

        watcher.Sample();
        report.Record("copy out and hash", step.Elapsed);
        Log.Detail(matched
            ? "MATCH - this run reproduced the artifact of record, file for file."
            : "MISMATCH - this run did NOT reproduce the artifact of record.");

        //PROVENANCE. It is written whatever the comparison said, because a file that does not match is
        //   exactly the one whose settings and machine somebody will want to read.
        step.Restart();
        Log.Step(5, StepCount, "write MODEL-PROVENANCE.json");
        ModelInfo reducedInfo = await store.ShowAsync(ReducedName, cancellationToken).ConfigureAwait(false);

        ProvenanceFile.Write(
            layout.ProvenancePath,
            UpstreamName,
            UpstreamRepository,
            UpstreamRevision,
            source,
            resolvedSource.Files,
            new[] { ProvenanceStep.FromModel("reduce", "IModelStore.ReduceOnnxAsync", reducedInfo) },
            artifacts,
            "staging/output");
        Log.Detail("wrote      " + layout.ProvenancePath);
        report.Record("provenance", step.Elapsed);

        if (!matched)
        {
            Log.Blank();
            Log.Error("The staged files are not the ones recorded for this machine.");
            Log.Detail("Bytes are never pinned across platforms: a different operating system or processor"
                + " may legitimately produce different files, and MODEL-PROVENANCE.json says which machine"
                + " the recorded hashes belong to. On the SAME machine this is a real difference and wants"
                + " investigating before anything is published.");
            return 2;
        }

        return 0;
    }

    /// <summary>
    /// The blob one file of a bundle lives in.
    /// </summary>
    /// <param name="resolved">The resolved bundle.</param>
    /// <param name="path">The file's path inside the bundle.</param>
    /// <returns>The blob's full path.</returns>
    /// <exception cref="InvalidOperationException">The bundle holds no such file.</exception>
    private static string FindBlob(ResolvedModel resolved, string path)
    {
        foreach (ResolvedFile file in resolved.Files)
        {
            if (string.Equals(file.Name, path, StringComparison.Ordinal))
            {
                return file.BlobPath;
            }
        }

        throw new InvalidOperationException(
            "The reduced bundle holds no file called " + path + ", so there is nothing to ship. What it"
                + " does hold is: " + string.Join(", ", Names(resolved)) + ".");
    }

    /// <summary>
    /// The paths a resolved bundle holds, for a message that has to say what was there instead.
    /// </summary>
    /// <param name="resolved">The resolved bundle.</param>
    /// <returns>The paths.</returns>
    private static IReadOnlyList<string> Names(ResolvedModel resolved)
    {
        var names = new List<string>();
        foreach (ResolvedFile file in resolved.Files)
        {
            names.Add(file.Name);
        }

        return names;
    }

    /// <summary>
    /// A progress sink that prints the library's own statuses, no more often than the tool prints anything.
    /// </summary>
    /// <returns>The sink.</returns>
    private static IProgress<PullProgress> Watch()
    {
        DateTime next = DateTime.MinValue;
        return new Progress<PullProgress>(progress =>
        {
            if (DateTime.UtcNow < next && progress.TotalBytes > 0)
            {
                return;
            }

            Log.Detail(Describe(progress));
            next = DateTime.UtcNow + Log.ProgressInterval;
        });
    }

    /// <summary>
    /// One progress report as a line of text.
    /// </summary>
    /// <param name="progress">The report.</param>
    /// <returns>The line.</returns>
    private static string Describe(PullProgress progress)
    {
        if (progress.TotalBytes <= 0)
        {
            return progress.Status;
        }

        return string.Format(
            CultureInfo.InvariantCulture,
            "{0}  {1:F1}%  ({2:N0} / {3:N0} bytes)",
            progress.Status,
            100.0 * progress.CompletedBytes / progress.TotalBytes,
            progress.CompletedBytes,
            progress.TotalBytes);
    }

    /// <summary>
    /// Clears the working folders when the run is allowed to, and never touches the output folder.
    /// </summary>
    /// <param name="layout">Where everything is.</param>
    /// <param name="cleanAfter">Whether the command line asked for it.</param>
    /// <param name="keep">Whether the command line forbade it.</param>
    private static void Clean(StagingLayout layout, bool cleanAfter, bool keep)
    {
        if (keep)
        {
            return;
        }

        bool clear = cleanAfter;
        if (!clear && !Console.IsInputRedirected && !Console.IsOutputRedirected)
        {
            Log.Blank();
            Console.Write(
                "Clear staging/download, staging/store and staging/tmp now? staging/output is kept. [y/N] ");
            string answer = Console.ReadLine();
            clear = answer != null && answer.Trim().StartsWith("y", StringComparison.OrdinalIgnoreCase);
        }

        if (!clear)
        {
            return;
        }

        long freed = layout.ClearWorkingDirectories();
        Log.Line("Cleared the working folders: " + Log.Bytes(freed) + " given back. staging/output is kept.");
    }

    /// <summary>
    /// Stops the run because something asked for Python, which neither stager may ever need.
    /// </summary>
    /// <param name="error">What the library said.</param>
    /// <returns>The exit code.</returns>
    private static int StopOnPython(Exception error)
    {
        Log.Blank();
        Log.Error("STOPPED: a step of this stager found itself on a PYTHON road.");
        Log.Detail("This tool reproduces its artifact with the model library's own managed code, and"
            + " nothing it does needs an interpreter. Something has changed, and the run stops rather"
            + " than quietly requiring Python on a build machine.");
        Log.Detail(error.GetType().Name + ": " + error.Message);
        return 1;
    }

    /// <summary>
    /// Writes what the tool does and what it takes.
    /// </summary>
    private static void WriteUsage()
    {
        Console.WriteLine();
        Console.WriteLine("ModelStager - reproduces this repository's shipped model artifact from upstream.");
        Console.WriteLine();
        Console.WriteLine("  dotnet run --project tools/ModelStager -c Release [options]");
        Console.WriteLine();
        Console.WriteLine("  --clean-after   clear staging/download, staging/store and staging/tmp when the");
        Console.WriteLine("                  run succeeds. staging/output is always kept.");
        Console.WriteLine("  --keep          never ask and never clear.");
        Console.WriteLine("  --help, -h      this text.");
        Console.WriteLine();
        Console.WriteLine("Everything the tool writes stays inside the repository. See staging/README.txt.");
        Console.WriteLine();
    }
}
