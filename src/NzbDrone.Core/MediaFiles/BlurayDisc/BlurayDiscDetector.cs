using System.IO;
using System.Linq;
using NLog;
using NzbDrone.Common.Disk;

namespace NzbDrone.Core.MediaFiles.BlurayDisc
{
    public interface IBlurayDiscDetector
    {
        bool IsBlurayDiscFolder(string path);
        string GetMainPlaylistFile(string path);
    }

    public class BlurayDiscDetector : IBlurayDiscDetector
    {
        private readonly IDiskProvider _diskProvider;
        private readonly IBlurayPlaylistParser _playlistParser;
        private readonly Logger _logger;

        public BlurayDiscDetector(IDiskProvider diskProvider,
                                  IBlurayPlaylistParser playlistParser,
                                  Logger logger)
        {
            _diskProvider = diskProvider;
            _playlistParser = playlistParser;
            _logger = logger;
        }

        public bool IsBlurayDiscFolder(string path)
        {
            if (!_diskProvider.FolderExists(path))
            {
                return false;
            }

            var bdmvPath = Path.Combine(path, "BDMV");

            if (!_diskProvider.FolderExists(bdmvPath))
            {
                return false;
            }

            var indexPath = Path.Combine(bdmvPath, "index.bdmv");

            if (!_diskProvider.FileExists(indexPath))
            {
                return false;
            }

            var streamPath = Path.Combine(bdmvPath, "STREAM");

            if (!_diskProvider.FolderExists(streamPath))
            {
                return false;
            }

            _logger.Debug("Detected Blu-ray disc structure at: {0}", path);
            return true;
        }

        public string GetMainPlaylistFile(string path)
        {
            var bdmvPath = Path.Combine(path, "BDMV");
            var streamPath = Path.Combine(bdmvPath, "STREAM");
            var playlistPath = Path.Combine(bdmvPath, "PLAYLIST");

            // Try to find main m2ts via MPLS playlist parsing first
            if (_diskProvider.FolderExists(playlistPath))
            {
                var mainStreamFile = _playlistParser.GetMainStreamFile(playlistPath, streamPath);

                if (mainStreamFile != null)
                {
                    _logger.Debug("Found main stream via playlist parsing: {0}", mainStreamFile);
                    return mainStreamFile;
                }
            }

            // Fallback: find the largest m2ts file in the STREAM directory
            _logger.Debug("Falling back to largest m2ts file detection in: {0}", streamPath);
            return GetLargestStreamFile(streamPath);
        }

        private string GetLargestStreamFile(string streamPath)
        {
            if (!_diskProvider.FolderExists(streamPath))
            {
                return null;
            }

            var m2tsFiles = _diskProvider.GetFiles(streamPath, false)
                .Where(f => Path.GetExtension(f).Equals(".m2ts", System.StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (!m2tsFiles.Any())
            {
                _logger.Warn("No m2ts files found in: {0}", streamPath);
                return null;
            }

            var largestFile = m2tsFiles
                .OrderByDescending(f => _diskProvider.GetFileSize(f))
                .First();

            _logger.Debug("Largest m2ts file: {0} ({1} bytes)", largestFile, _diskProvider.GetFileSize(largestFile));
            return largestFile;
        }
    }
}
