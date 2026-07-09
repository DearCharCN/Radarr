using System.Collections.Generic;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Languages;
using NzbDrone.Core.Movies;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Profiles.AudioLanguageMappings;

namespace NzbDrone.Core.Test.DecisionEngineTests
{
    [TestFixture]
    public class AudioLanguageMapperFixture
    {
        private RemoteMovie GivenRemoteMovie(Language originalLanguage)
        {
            return new RemoteMovie
            {
                Movie = new Movie
                {
                    MovieMetadata = new MovieMetadata
                    {
                        OriginalLanguage = originalLanguage
                    }
                }
            };
        }

        [Test]
        public void should_map_builtin_chinese_aliases()
        {
            var audio = new ReleaseAudioInfo { Language = "Guoyu", Specification = "DDP 5.1" };

            var result = AudioLanguageMapper.TagAudioTrack(GivenRemoteMovie(Language.English), audio);

            result.MappedLanguage.Should().Be(Language.Chinese);
            result.LanguageTags.Should().Contain("Chinese");
            result.MatchedLanguageAlias.Should().Be("Guoyu");
        }

        [Test]
        public void should_prefer_user_mapping_before_builtin_aliases()
        {
            var audio = new ReleaseAudioInfo { Language = "Guoyu", Specification = "DDP 5.1" };
            var mappings = new List<AudioLanguageMapping>
            {
                new ()
                {
                    Language = Language.Japanese,
                    Aliases = new List<string> { "Guoyu" }
                }
            };

            var result = AudioLanguageMapper.TagAudioTrack(GivenRemoteMovie(Language.English), audio, mappings);

            result.MappedLanguage.Should().Be(Language.Japanese);
            result.LanguageTags.Should().Contain("Japanese");
        }

        [Test]
        public void should_add_origin_tag_when_track_matches_original_language()
        {
            var audio = new ReleaseAudioInfo { Language = "English", Specification = "TrueHD Atmos 7.1" };

            var result = AudioLanguageMapper.TagAudioTrack(GivenRemoteMovie(Language.English), audio);

            result.MappedLanguage.Should().Be(Language.English);
            result.LanguageTags.Should().Contain("English");
            result.LanguageTags.Should().Contain(AudioLanguageMapper.OriginTag);
        }
    }
}
