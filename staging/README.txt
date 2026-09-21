================================================================================
staging/ - where the model stager works
CodeBrix.Audio.MusicGeneration.SkyTNT
================================================================================

This folder is the model stager's workshop. EVERYTHING the stager downloads,
reduces and writes stays inside this repository: the default model store under
the user's home directory is never opened and never touched, and the system
temporary directory is redirected in here before the first call into the model
library, because the reduction lays the whole 938 MB published bundle out under
it and a Debian laptop's /tmp is a small in-memory file system.

Only this file is checked in. Everything else here is ignored by git.


THE FOLDERS
-----------
  download/   Reserved for a downloader that keeps its downloads apart from its
              store. The model library does not: a partly downloaded file is a
              sidecar beside the blob it will become, inside store/. The folder
              is created, is ignored by git, and normally stays empty.

  store/      The model store - blobs and manifests, laid out exactly as Ollama
              lays its own out. It holds the publisher's ONNX pair as it was
              pulled and the reduced bundle made from it. It is working
              material: deleting it costs a re-download, nothing else.

  tmp/        The temporary directory the reduction lays its work out under.
              Empty between steps; deleting it is always safe.

  output/     THE ARTIFACT THAT SHIPS, and the only folder here that matters
              once a run is over. A later phase's tests read it and the package
              build takes the model from it. NEVER delete it. The stager itself
              never clears it.


RUNNING THE STAGER
------------------
From the repository root:

    dotnet run --project tools/ModelStager -c Release

It reports what it is doing as it goes, verifies every file it produced against
the size and the sha256 recorded for this machine, writes MODEL-PROVENANCE.json
at the repository root, and prints a usage report: what it downloaded, how large
this folder grew, and how long each step took.

    --clean-after     clear download/, store/ and tmp/ when the run succeeds,
                      without asking. output/ is always kept.
    --keep            never ask and never clear; leave everything in place.
    --help            what the tool does and what it takes.

With neither flag, a run at an interactive console offers to clear the three
working folders when it finishes.

A run from an EMPTY staging/ downloads about 938 MB and needs roughly 3.5 GB of
free disk space at its peak. It checks for that space before it starts. A second
run costs no download: the files are already in store/. The reduction of the
larger graph is the long part of the run and it is CPU-bound.


WHAT THE STAGER DOES
--------------------
  1. Pulls the ONNX pair the publisher ships, pinned to one commit, with the two
     configuration files and the model card. The safetensors weights, the
     duplicate .bin and the training logs in the same repository are left alone.
  2. Reduces BOTH graphs to block-wise four-bit weights through the model
     library's own MANAGED engine, which is pinned rather than left to the
     automatic choice, so no future change of default can require Python on a
     machine that stages this model. No Python is involved at any point.
  3. Writes the four files the loader wants into output/, FLAT: config.json,
     generation_config.json, model_base.onnx and model_token.onnx. The
     publisher's model card stays in the store and is not part of the artifact.
  4. Checks every one of them against its recorded size and sha256.
  5. Writes MODEL-PROVENANCE.json: where the model came from, the commit, the
     licence, the reduction's settings exactly as the store recorded them, the
     size and sha256 of every file that ships, and the operating system and
     processor it was staged on.

Re-staging on the SAME machine reproduces the same bytes, and that is the gate.
Bytes are never pinned across platforms: a different operating system or
processor may legitimately produce a different file, and the provenance records
which machine the recorded hashes belong to.
