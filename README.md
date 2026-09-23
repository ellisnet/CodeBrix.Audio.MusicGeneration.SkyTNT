# CodeBrix.Audio.MusicGeneration.SkyTNT

The SkyTNT weight-only INT4 ONNX model for [CodeBrix.Audio.MusicGeneration](https://github.com/ellisnet/CodeBrix.Audio.MusicGeneration), packaged with a small registration assembly. It generates streaming MIDI events.

```csharp
using CodeBrix.Audio.ModestSynth;
using CodeBrix.Audio.MusicGeneration;
using CodeBrix.Audio.MusicGeneration.SkyTNT;

GeneralMidiInstrumentLibrary.Register();
SkyTNTModel.Register();
using var music = new MusicSession(new MusicGenerationOptions { Generator = SkyTNTModel.GeneratorName });
music.Play();
```

Registration loads nothing. The model loads on first use or explicit preloading. The package copies its model and notices into `Models/CodeBrix.Audio.MusicGeneration.SkyTNT/` in build and publish output, including when referenced through an intermediary library. MuPT and SkyTNT can be installed together. Registering does not select a generator; specify its name.

Inference assets: **145.13 MiB** before NuGet compression. File size is not a runtime-memory limit. No runtime downloads, ModelManager or Python are required. SkyTNT uses ModelRunner's managed ONNX engine.

See [README-INDEX.txt](README-INDEX.txt) for consumer and maintainer documentation, and [MODEL-PROVENANCE.json](MODEL-PROVENANCE.json) for the reproducible recipe. The wrapper and model are Apache-2.0 licensed; attribution is in [THIRD-PARTY-NOTICES.txt](THIRD-PARTY-NOTICES.txt).

Release preparation is in progress. The published MusicGeneration dependency and final consuming-package gates must be verified before publication.
