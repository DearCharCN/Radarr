using System.Collections.Generic;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.CustomFormats;
using NzbDrone.Core.Profiles;
using NzbDrone.Core.Profiles.Qualities;

namespace NzbDrone.Core.Test.Qualities
{
    [TestFixture]
    public class QualityProfileFixture
    {
        [Test]
        public void should_return_only_formats_that_contribute_to_mutex_score()
        {
            var dolbyVision = new CustomFormat("Dolby Vision") { Id = 1 };
            var hdr = new CustomFormat("HDR") { Id = 2 };
            var remux = new CustomFormat("Remux") { Id = 3 };
            var profile = new QualityProfile
            {
                FormatItems = new List<ProfileFormatItem>
                {
                    new () { Format = dolbyVision, Score = 100 },
                    new () { Format = hdr, Score = 50 },
                    new () { Format = remux, Score = 30 }
                },
                CustomFormatMutexGroups = new List<CustomFormatMutexGroup>
                {
                    new ()
                    {
                        Name = "HDR Formats",
                        Enabled = true,
                        CustomFormatIds = new List<int> { dolbyVision.Id, hdr.Id }
                    }
                }
            };

            var matchedFormats = new List<CustomFormat> { dolbyVision, hdr, remux };

            profile.CalculateCustomFormatScore(matchedFormats).Should().Be(130);
            profile.GetScoredCustomFormats(matchedFormats).Should().Equal(dolbyVision, remux);
        }
    }
}
