using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CodeBrix.Audio.Midi;
using CodeBrix.Audio.MusicGeneration.Presets;
using SilverAssertions;
using SilverAssertions.Collections;
using SilverAssertions.Numeric;
using SilverAssertions.Primitives;
using Xunit;

namespace CodeBrix.Audio.MusicGeneration.SkyTNT.Tests;

public class SkyTNTModelTests
{
    [Fact]
    public void paths_are_absolute_below_the_consuming_application_and_have_unique_names()
    {
        // Arrange
        var root = Path.Combine(Path.GetTempPath(), "CodeBrix-model-path-test");

        // Act
        var files = SkyTNTModel.ResolveFiles(root);

        // Assert
        files.Keys.Should().BeEquivalentTo(SkyTNTModel.FileNames);
        foreach (var pair in files)
            pair.Value.Should().Be(Path.Combine(root, SkyTNTModel.RelativeModelDirectory, pair.Key));
        SkyTNTModel.RelativeModelDirectory.Should().Be("Models/CodeBrix.Audio.MusicGeneration.SkyTNT");
        var mutable = (IDictionary<string, string>)files;
        Action change = () => mutable.Add("extra", "elsewhere");
        change.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void registering_twice_is_lazy_and_does_not_replace_the_default_replay()
    {
        // Arrange
        var replay = MusicGeneratorRegistry.Resolve(null);

        // Act
        SkyTNTModel.Register();
        SkyTNTModel.Register();

        // Assert
        SkyTNTModel.IsRegistered.Should().BeTrue();
        SkyTNTModel.IsLoaded.Should().BeFalse();
        MusicGeneratorRegistry.Resolve(SkyTNTModel.GeneratorName).Should().BeSameAs(SkyTNTModel.Instance);
        MusicGeneratorRegistry.Resolve(null).Should().BeSameAs(replay);
        MusicGeneratorRegistry.Registered.Count(item => ReferenceEquals(item, SkyTNTModel.Instance)).Should().Be(1);
        SkyTNTModel.IsAvailable.Should().BeTrue("the package build targets copy the staged artifacts");
        foreach (var file in new[] { "LICENSE", "THIRD-PARTY-NOTICES.txt", "MODEL-CARD.md", "MODEL-PROVENANCE.json" })
            File.Exists(Path.Combine(SkyTNTModel.ModelDirectory, file)).Should().BeTrue();
    }

    [Fact]
    public async Task the_copied_model_generates_notes_without_a_staging_path()
    {
        // Arrange
        SkyTNTModel.Register();
        var request = SkyTNTPresets.AmbientElectronica.CreateRequest();
        request.Controls.MaximumEvents = 32;
        request.Seed = 29;
        var notes = 0;

        // Act
        try
        {
            await foreach (var item in SkyTNTModel.Instance.GenerateAsync(request, TestContext.Current.CancellationToken))
                if (item.Event is NoteOnEvent) notes++;
        }
        finally
        {
            SkyTNTModel.Instance.Release();
        }

        // Assert
        notes.Should().BeGreaterThan(0);
        SkyTNTModel.IsLoaded.Should().BeFalse();
    }
}
