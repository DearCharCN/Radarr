using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using FizzWare.NBuilder;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Http;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Indexers.Newznab;
using NzbDrone.Core.Indexers.Torznab;
using NzbDrone.Core.Languages;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Validation;

namespace NzbDrone.Core.Test.IndexerTests.TorznabTests
{
    [TestFixture]
    public class TorznabFixture : CoreTest<Torznab>
    {
        private NewznabCapabilities _caps;

        [SetUp]
        public void Setup()
        {
            Subject.Definition = new IndexerDefinition()
            {
                Name = "Torznab",
                Settings = new TorznabSettings()
                {
                    BaseUrl = "http://indexer.local/",
                    Categories = new int[] { 1 }
                }
            };

            _caps = new NewznabCapabilities
            {
                Categories = Builder<NewznabCategory>.CreateListOfSize(1).All().With(t => t.Id = 1).Build().ToList()
            };

            Mocker.GetMock<INewznabCapabilitiesProvider>()
                .Setup(v => v.GetCapabilities(It.IsAny<NewznabSettings>()))
                .Returns(_caps);
        }

        [Test]
        public async Task should_parse_recent_feed_from_torznab_hdaccess_net()
        {
            var recentFeed = ReadAllText(@"Files/Indexers/Torznab/torznab_hdaccess_net.xml");

            Mocker.GetMock<IHttpClient>()
                .Setup(o => o.ExecuteAsync(It.Is<HttpRequest>(v => v.Method == HttpMethod.Get)))
                .Returns<HttpRequest>(r => Task.FromResult(new HttpResponse(r, new HttpHeader(), recentFeed)));

            var releases = await Subject.FetchRecent();

            releases.Should().HaveCount(5);

            releases.First().Should().BeOfType<TorrentInfo>();
            var releaseInfo = releases.First() as TorrentInfo;

            releaseInfo.Title.Should().Be("Better Call Saul S01E05 Alpine Shepherd 1080p NF WEBRip DD5.1 x264");
            releaseInfo.DownloadProtocol.Should().Be(DownloadProtocol.Torrent);
            releaseInfo.DownloadUrl.Should().Be("https://hdaccess.net/download.php?torrent=11515&passkey=123456");
            releaseInfo.InfoUrl.Should().Be("https://hdaccess.net/details.php?id=11515&hit=1");
            releaseInfo.CommentUrl.Should().Be("https://hdaccess.net/details.php?id=11515&hit=1#comments");
            releaseInfo.Indexer.Should().Be(Subject.Definition.Name);
            releaseInfo.PublishDate.Should().Be(DateTime.Parse("2015/03/14 21:10:42"));
            releaseInfo.Size.Should().Be(2538463390);
            releaseInfo.InfoHash.Should().Be("63e07ff523710ca268567dad344ce1e0e6b7e8a3");
            releaseInfo.Seeders.Should().Be(7);
            releaseInfo.Peers.Should().Be(7);
        }

        [Test]
        public async Task should_parse_recent_feed_from_torznab_tpb()
        {
            var recentFeed = ReadAllText(@"Files/Indexers/Torznab/torznab_tpb.xml");

            Mocker.GetMock<IHttpClient>()
                .Setup(o => o.ExecuteAsync(It.Is<HttpRequest>(v => v.Method == HttpMethod.Get)))
                .Returns<HttpRequest>(r => Task.FromResult(new HttpResponse(r, new HttpHeader(), recentFeed)));

            var releases = await Subject.FetchRecent();

            releases.Should().HaveCount(5);

            releases.First().Should().BeOfType<TorrentInfo>();
            var releaseInfo = releases.First() as TorrentInfo;

            releaseInfo.Title.Should().Be("Series Title S05E02 HDTV x264-Xclusive [eztv]");
            releaseInfo.DownloadProtocol.Should().Be(DownloadProtocol.Torrent);
            releaseInfo.MagnetUrl.Should().Be("magnet:?xt=urn:btih:9fb267cff5ae5603f07a347676ec3bf3e35f75e1&dn=Game+of+Thrones+S05E02+HDTV+x264-Xclusive+%5Beztv%5D&tr=udp:%2F%2Fopen.demonii.com:1337&tr=udp:%2F%2Ftracker.coppersurfer.tk:6969&tr=udp:%2F%2Ftracker.leechers-paradise.org:6969&tr=udp:%2F%2Fexodus.desync.com:6969");
            releaseInfo.DownloadUrl.Should().Be("magnet:?xt=urn:btih:9fb267cff5ae5603f07a347676ec3bf3e35f75e1&dn=Game+of+Thrones+S05E02+HDTV+x264-Xclusive+%5Beztv%5D&tr=udp:%2F%2Fopen.demonii.com:1337&tr=udp:%2F%2Ftracker.coppersurfer.tk:6969&tr=udp:%2F%2Ftracker.leechers-paradise.org:6969&tr=udp:%2F%2Fexodus.desync.com:6969");
            releaseInfo.InfoUrl.Should().Be("https://thepiratebay.se/torrent/11811366/Series_Title_S05E02_HDTV_x264-Xclusive_%5Beztv%5D");
            releaseInfo.CommentUrl.Should().Be("https://thepiratebay.se/torrent/11811366/Series_Title_S05E02_HDTV_x264-Xclusive_%5Beztv%5D");
            releaseInfo.Indexer.Should().Be(Subject.Definition.Name);
            releaseInfo.PublishDate.Should().Be(DateTime.Parse("Sat, 11 Apr 2015 21:34:00 -0600").ToUniversalTime());
            releaseInfo.Size.Should().Be(388895872);
            releaseInfo.InfoHash.Should().Be("9fb267cff5ae5603f07a347676ec3bf3e35f75e1");
            releaseInfo.Seeders.Should().Be(34128);
            releaseInfo.Peers.Should().Be(36724);
        }

        [Test]
        public async Task should_parse_recent_feed_from_torznab_animetosho()
        {
            var recentFeed = ReadAllText(@"Files/Indexers/Torznab/torznab_animetosho.xml");

            Mocker.GetMock<IHttpClient>()
                .Setup(o => o.ExecuteAsync(It.Is<HttpRequest>(v => v.Method == HttpMethod.Get)))
                .Returns<HttpRequest>(r => Task.FromResult(new HttpResponse(r, new HttpHeader(), recentFeed)));

            var releases = await Subject.FetchRecent();

            releases.Should().HaveCount(2);

            releases.First().Should().BeOfType<TorrentInfo>();
            var releaseInfo = releases.First() as TorrentInfo;

            releaseInfo.Title.Should().Be("[finFAGs]_Frame_Arms_Girl_07_(1280x720_TV_AAC)_[1262B6F7].mkv");
            releaseInfo.DownloadProtocol.Should().Be(DownloadProtocol.Torrent);
            releaseInfo.DownloadUrl.Should().Be("http://storage.localhost/torrents/123451.torrent");
            releaseInfo.InfoUrl.Should().Be("https://localhost/view/finfags-_frame_arms_girl_07_-1280x720_tv_aac-_-1262b6f7-mkv.123451");
            releaseInfo.CommentUrl.Should().Be("https://localhost/view/finfags-_frame_arms_girl_07_-1280x720_tv_aac-_-1262b6f7-mkv.123451");
            releaseInfo.Indexer.Should().Be(Subject.Definition.Name);
            releaseInfo.PublishDate.Should().Be(DateTime.Parse("Wed, 17 May 2017 20:36:06 +0000").ToUniversalTime());
            releaseInfo.Size.Should().Be(316477946);
            releaseInfo.TmdbId.Should().Be(0);
            releaseInfo.InfoHash.Should().Be("2d69a861bef5a9f2cdf791b7328e37b7953205e1");
            releaseInfo.Seeders.Should().BeNull();
            releaseInfo.Peers.Should().BeNull();
        }

        [Test]
        public async Task should_parse_media_info_attributes()
        {
            var recentFeed = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<rss version=""1.0"" xmlns:torznab=""http://torznab.com/schemas/2015/feed"">
  <channel>
    <item>
      <title>Example Movie 2024 2160p UHD BluRay REMUX HEVC TrueHD 7.1</title>
      <guid>https://indexer.local/details/1</guid>
      <comments>https://indexer.local/details/1</comments>
      <pubDate>Sat, 14 Mar 2015 17:10:42 -0400</pubDate>
      <enclosure url=""https://indexer.local/download/1.torrent"" length=""123456789"" type=""application/x-bittorrent"" />
      <torznab:attr name=""category"" value=""2000"" />
      <torznab:attr name=""size"" value=""123456789"" />
      <torznab:attr name=""language"" value=""English"" />
      <torznab:attr name=""language"" value=""Chinese"" />
      <torznab:attr name=""subs"" value=""English"" />
      <torznab:attr name=""subs"" value=""Chinese"" />
      <torznab:attr name=""audio"" value=""English: TrueHD 7.1 Atmos"" />
      <torznab:attr name=""audio"" value=""Chinese: DDP 5.1"" />
      <torznab:attr name=""mediaInfoStatus"" value=""pending"" />
      <torznab:attr name=""mediaInfoSearchId"" value=""search-1"" />
      <torznab:attr name=""mediaInfoProgressStatus"" value=""pending"" />
      <torznab:attr name=""mediaInfoProgressCompleted"" value=""3"" />
      <torznab:attr name=""mediaInfoProgressTotal"" value=""11"" />
    </item>
  </channel>
</rss>";

            Mocker.GetMock<IHttpClient>()
                .Setup(o => o.ExecuteAsync(It.Is<HttpRequest>(v => v.Method == HttpMethod.Get)))
                .Returns<HttpRequest>(r => Task.FromResult(new HttpResponse(r, new HttpHeader(), recentFeed)));

            var releases = await Subject.FetchRecent();

            releases.Should().HaveCount(1);

            var releaseInfo = releases.First() as TorrentInfo;

            releaseInfo.Languages.Should().BeEquivalentTo(new[] { Language.English, Language.Chinese });
            releaseInfo.Subs.Should().BeEquivalentTo(new[] { "English", "Chinese" });
            releaseInfo.AudioInfo.Should().HaveCount(2);
            releaseInfo.AudioInfo.First().Language.Should().Be("English");
            releaseInfo.AudioInfo.First().Specification.Should().Be("TrueHD 7.1 Atmos");
            releaseInfo.AudioInfo.Last().Language.Should().Be("Chinese");
            releaseInfo.AudioInfo.Last().Specification.Should().Be("DDP 5.1");
            releaseInfo.MediaInfoStatus.Should().Be("pending");
            releaseInfo.MediaInfoSearchId.Should().Be("search-1");
            releaseInfo.MediaInfoProgressStatus.Should().Be("pending");
            releaseInfo.MediaInfoProgressCompleted.Should().Be(3);
            releaseInfo.MediaInfoProgressTotal.Should().Be(11);
        }

        [Test]
        public void should_use_best_pagesize_reported_by_caps()
        {
            _caps.MaxPageSize = 30;
            _caps.DefaultPageSize = 25;

            Subject.PageSize.Should().Be(30);
        }

        [Test]
        public void should_not_use_pagesize_over_100_even_if_reported_in_caps()
        {
            _caps.MaxPageSize = 250;
            _caps.DefaultPageSize = 25;

            Subject.PageSize.Should().Be(100);
        }

        [TestCase("http://localhost:9117/", "/api")]
        public void url_and_api_not_jackett_all(string baseUrl, string apiPath)
        {
            var setting = new TorznabSettings()
            {
                BaseUrl = baseUrl,
                ApiPath = apiPath
            };

            setting.Validate().IsValid.Should().BeTrue();
        }

        [TestCase("http://localhost:9117/torznab/all/api")]
        [TestCase("http://localhost:9117/api/v2.0/indexers/all/results/torznab")]
        public void jackett_all_url_should_not_validate(string baseUrl)
        {
            var recentFeed = ReadAllText(@"Files/Indexers/Torznab/torznab_tpb.xml");
            (Subject.Definition.Settings as TorznabSettings).BaseUrl = baseUrl;

            Mocker.GetMock<IHttpClient>()
                .Setup(o => o.ExecuteAsync(It.Is<HttpRequest>(v => v.Method == HttpMethod.Get)))
                .Returns<HttpRequest>(r => Task.FromResult(new HttpResponse(r, new HttpHeader(), recentFeed)));

            var result = new NzbDroneValidationResult(Subject.Test());
            result.IsValid.Should().BeTrue();
            result.HasWarnings.Should().BeTrue();
        }

        [TestCase("/torznab/all/api")]
        [TestCase("/api/v2.0/indexers/all/results/torznab")]
        public void jackett_all_api_should_not_validate(string apiPath)
        {
            var recentFeed = ReadAllText(@"Files/Indexers/Torznab/torznab_tpb.xml");

            Mocker.GetMock<IHttpClient>()
                  .Setup(o => o.ExecuteAsync(It.Is<HttpRequest>(v => v.Method == HttpMethod.Get)))
                  .Returns<HttpRequest>(r => Task.FromResult(new HttpResponse(r, new HttpHeader(), recentFeed)));

            (Subject.Definition.Settings as TorznabSettings).ApiPath = apiPath;

            var result = new NzbDroneValidationResult(Subject.Test());
            result.IsValid.Should().BeTrue();
            result.HasWarnings.Should().BeTrue();
        }
    }
}
