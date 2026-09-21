namespace CodeBrix.Audio.MusicGeneration.SkyTNT.ModelStager;

/// <summary>
/// One file the package ships: where it comes from inside the reduced bundle, what it is called in the
/// output folder, and the size and sha256 recorded for it on the machine the artifact of record was
/// staged on.
/// </summary>
internal sealed class ExpectedArtifact
{
    /// <summary>
    /// Initializes an expectation.
    /// </summary>
    /// <param name="bundlePath">The file's path inside the reduced bundle.</param>
    /// <param name="outputName">The name it carries in the output folder.</param>
    /// <param name="bytes">Its recorded size.</param>
    /// <param name="sha256">Its recorded sha256.</param>
    internal ExpectedArtifact(string bundlePath, string outputName, long bytes, string sha256)
    {
        BundlePath = bundlePath;
        OutputName = outputName;
        Bytes = bytes;
        Sha256 = sha256;
    }

    /// <summary>The file's path inside the reduced bundle, with forward slashes.</summary>
    internal string BundlePath { get; }

    /// <summary>
    /// The name it carries in the output folder. The bundle keeps the publisher's <c>onnx/</c> folder and
    /// the loader wants one flat folder, so the two graphs lose their directory here.
    /// </summary>
    internal string OutputName { get; }

    /// <summary>Its recorded size in bytes.</summary>
    internal long Bytes { get; }

    /// <summary>Its recorded sha256, as lower-case hexadecimal.</summary>
    internal string Sha256 { get; }
}
