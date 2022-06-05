using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using Microsoft.Extensions.Logging;

namespace jellyfin_ani_sync.Helpers;

public class AnimeListHelpers {
    public static int? GetAniDbId(ILogger logger, Dictionary<string, string> providers, int episodeNumber) {
        int aniDbId;
        if (providers.ContainsKey("Anidb")) {
            if (int.TryParse(providers["Anidb"], out aniDbId)) return aniDbId;
        } else if (providers.ContainsKey("Tvdb")) {
            int tvDbId;
            if (!int.TryParse(providers["Tvdb"], out tvDbId)) return null;
            AnimeListXml animeListXml = GetAnimeListFileContents(logger);
            if (animeListXml == null) return null;
            var foundAnime = animeListXml.Anime.Where(anime => int.TryParse(anime.Tvdbid, out int xmlTvDbId) && xmlTvDbId == tvDbId).ToList();
            if (!foundAnime.Any()) return null;
            logger.LogInformation("Anime reference found in anime list XML");
            if (foundAnime.Count() == 1) return int.TryParse(foundAnime.First().Anidbid, out aniDbId) ? aniDbId : null;
            for (var i = 0; i < foundAnime.Count; i++) {
                // xml first seasons episode offset is always null
                if ((int.TryParse(foundAnime[i].Episodeoffset, out var episodeOffset) && episodeOffset <= episodeNumber) || i == 0) {
                    if (foundAnime.ElementAtOrDefault(i + 1) != null && int.TryParse(foundAnime[i + 1].Episodeoffset, out episodeOffset) && episodeOffset <= episodeNumber) continue;
                    logger.LogInformation($"Anime {foundAnime[i].Name} found in anime XML file");
                    return int.TryParse(foundAnime[i].Anidbid, out aniDbId) ? aniDbId : null;
                }
            }
        }

        return null;
    }

    private static AnimeListXml GetAnimeListFileContents(ILogger logger) {
        if (Plugin.Instance.PluginConfiguration.animeListSaveLocation == null) {
            return null;
        }

        try {
            using (var stream = File.OpenRead(Path.Combine(Plugin.Instance.PluginConfiguration.animeListSaveLocation, "anime-list-full.xml"))) {
                var serializer = new XmlSerializer(typeof(AnimeListXml));
                return (AnimeListXml)serializer.Deserialize(stream);
            }
        } catch (Exception e) {
            logger.LogError($"Could not deserialize anime list XML; {e.Message}. Try forcibly redownloading the XML file");
            throw;
        }
    }

    [XmlRoot(ElementName = "anime")]
    public class Anime {
        [XmlElement(ElementName = "name")] public string Name { get; set; }

        [XmlAttribute(AttributeName = "anidbid")]
        public string Anidbid { get; set; }

        [XmlAttribute(AttributeName = "tvdbid")]
        public string Tvdbid { get; set; }

        [XmlAttribute(AttributeName = "defaulttvdbseason")]
        public string Defaulttvdbseason { get; set; }

        [XmlAttribute(AttributeName = "episodeoffset")]
        public string Episodeoffset { get; set; }

        [XmlAttribute(AttributeName = "tmdbid")]
        public string Tmdbid { get; set; }
    }

    [XmlRoot(ElementName = "anime-list")]
    public class AnimeListXml {
        [XmlElement(ElementName = "anime")] public List<Anime> Anime { get; set; }
    }
}