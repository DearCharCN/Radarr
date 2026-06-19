using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NLog;
using NzbDrone.Common.Disk;

namespace NzbDrone.Core.MediaFiles.BlurayDisc
{
    public interface IBlurayPlaylistParser
    {
        string GetMainStreamFile(string playlistPath, string streamPath);
    }

    public class BlurayPlaylistParser : IBlurayPlaylistParser
    {
        private readonly IDiskProvider _diskProvider;
        private readonly Logger _logger;

        private static readonly byte[] MplsSignature = { 0x4D, 0x50, 0x4C, 0x53 }; // "MPLS"

        public BlurayPlaylistParser(IDiskProvider diskProvider, Logger logger)
        {
            _diskProvider = diskProvider;
            _logger = logger;
        }

        public string GetMainStreamFile(string playlistPath, string streamPath)
        {
            try
            {
                var mplsFiles = _diskProvider.GetFiles(playlistPath, false)
                    .Where(f => Path.GetExtension(f).Equals(".mpls", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (!mplsFiles.Any())
                {
                    return null;
                }

                var playlists = new List<PlaylistInfo>();

                foreach (var mplsFile in mplsFiles)
                {
                    var info = ParsePlaylist(mplsFile, streamPath);

                    if (info != null)
                    {
                        playlists.Add(info);
                    }
                }

                if (!playlists.Any())
                {
                    return null;
                }

                // The main feature is typically the playlist with the longest total duration
                var mainPlaylist = playlists.OrderByDescending(p => p.TotalSize).First();

                _logger.Debug("Selected main playlist: {0} with {1} clips, total size: {2} bytes",
                    mainPlaylist.FilePath, mainPlaylist.StreamFiles.Count, mainPlaylist.TotalSize);

                // Return the largest stream file in the main playlist
                return mainPlaylist.StreamFiles
                    .OrderByDescending(f => _diskProvider.GetFileSize(f))
                    .FirstOrDefault();
            }
            catch (Exception ex)
            {
                _logger.Warn(ex, "Failed to parse Blu-ray playlists from: {0}", playlistPath);
                return null;
            }
        }

        private PlaylistInfo ParsePlaylist(string mplsFile, string streamPath)
        {
            try
            {
                using var stream = File.OpenRead(mplsFile);
                using var reader = new BinaryReader(stream);

                // Verify MPLS signature
                var signature = reader.ReadBytes(4);

                if (!signature.SequenceEqual(MplsSignature))
                {
                    return null;
                }

                // Read version (4 bytes, e.g. "0200" or "0100")
                var version = reader.ReadBytes(4);

                // Read playlist start address (big-endian uint32 at offset 8)
                var playlistStartAddress = ReadUInt32BE(reader);

                // Skip to playlist
                stream.Seek(playlistStartAddress, SeekOrigin.Begin);

                // Read playlist length
                var playlistLength = ReadUInt32BE(reader);

                // Skip 2 reserved bytes
                reader.ReadBytes(2);

                // Read number of play items
                var numberOfPlayItems = ReadUInt16BE(reader);

                // Skip number of sub paths
                reader.ReadBytes(2);

                var streamFiles = new List<string>();
                long totalSize = 0;

                for (var i = 0; i < numberOfPlayItems; i++)
                {
                    var clipFile = ParsePlayItem(reader, streamPath);

                    if (clipFile != null && _diskProvider.FileExists(clipFile))
                    {
                        streamFiles.Add(clipFile);
                        totalSize += _diskProvider.GetFileSize(clipFile);
                    }
                }

                if (!streamFiles.Any())
                {
                    return null;
                }

                return new PlaylistInfo
                {
                    FilePath = mplsFile,
                    StreamFiles = streamFiles,
                    TotalSize = totalSize
                };
            }
            catch (Exception ex)
            {
                _logger.Debug(ex, "Failed to parse MPLS file: {0}", mplsFile);
                return null;
            }
        }

        private string ParsePlayItem(BinaryReader reader, string streamPath)
        {
            try
            {
                // PlayItem length (2 bytes)
                var itemLength = ReadUInt16BE(reader);
                var startPosition = reader.BaseStream.Position;

                // Clip Information file name (5 bytes ASCII, e.g. "00001")
                var clipNameBytes = reader.ReadBytes(5);
                var clipName = System.Text.Encoding.ASCII.GetString(clipNameBytes);

                // Skip the rest of the play item
                reader.BaseStream.Seek(startPosition + itemLength, SeekOrigin.Begin);

                // Build the m2ts file path
                var m2tsFile = Path.Combine(streamPath, clipName + ".m2ts");
                return m2tsFile;
            }
            catch
            {
                return null;
            }
        }

        private static uint ReadUInt32BE(BinaryReader reader)
        {
            var bytes = reader.ReadBytes(4);
            Array.Reverse(bytes);
            return BitConverter.ToUInt32(bytes, 0);
        }

        private static ushort ReadUInt16BE(BinaryReader reader)
        {
            var bytes = reader.ReadBytes(2);
            Array.Reverse(bytes);
            return BitConverter.ToUInt16(bytes, 0);
        }

        private class PlaylistInfo
        {
            public string FilePath { get; set; }
            public List<string> StreamFiles { get; set; }
            public long TotalSize { get; set; }
        }
    }
}
