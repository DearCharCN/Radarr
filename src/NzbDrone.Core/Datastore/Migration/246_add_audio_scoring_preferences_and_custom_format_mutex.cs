using FluentMigrator;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace NzbDrone.Core.Datastore.Migration
{
    [Migration(246)]
    public class add_audio_scoring_preferences_and_custom_format_mutex : NzbDroneMigrationBase
    {
        protected override void MainDbUpgrade()
        {
            Create.TableForModel("AudioScoreProfiles")
                  .WithColumn("Name").AsString().NotNullable()
                  .WithColumn("Enabled").AsBoolean().WithDefaultValue(true)
                  .WithColumn("Rules").AsString().NotNullable()
                  .WithColumn("MutexGroups").AsString().NotNullable();

            Create.TableForModel("AudioLanguagePreferences")
                  .WithColumn("Name").AsString().NotNullable()
                  .WithColumn("Enabled").AsBoolean().WithDefaultValue(true)
                  .WithColumn("ScoreGapThreshold").AsInt32().WithDefaultValue(0)
                  .WithColumn("AudioScoreProfileId").AsInt32().Nullable()
                  .WithColumn("Entries").AsString().NotNullable();

            Create.TableForModel("CustomFormatMutexGroups")
                  .WithColumn("Name").AsString().NotNullable()
                  .WithColumn("Enabled").AsBoolean().WithDefaultValue(true)
                  .WithColumn("CustomFormatIds").AsString().NotNullable();

            Alter.Table("QualityProfiles").AddColumn("AudioLanguagePreferenceId").AsInt32().Nullable();
            Alter.Table("QualityProfiles").AddColumn("AudioScoreProfileId").AsInt32().Nullable();
            Alter.Table("QualityProfiles").AddColumn("CustomFormatMutexGroupIds").AsString().NotNullable().WithDefaultValue("[]");

            Insert.IntoTable("AudioScoreProfiles").Row(new
            {
                Name = "Default Audio Score",
                Enabled = true,
                Rules = "[{\"name\":\"7.1\",\"matchType\":\"contains\",\"pattern\":\"7.1\",\"score\":30,\"mutexGroup\":\"Channels\",\"enabled\":true},{\"name\":\"5.1\",\"matchType\":\"contains\",\"pattern\":\"5.1\",\"score\":15,\"mutexGroup\":\"Channels\",\"enabled\":true},{\"name\":\"TrueHD\",\"matchType\":\"normalizedContains\",\"pattern\":\"TrueHD\",\"score\":50,\"mutexGroup\":\"Codec\",\"enabled\":true},{\"name\":\"DTS-HD MA\",\"matchType\":\"normalizedContains\",\"pattern\":\"DTSHDMA\",\"score\":50,\"mutexGroup\":\"Codec\",\"enabled\":true},{\"name\":\"DDP\",\"matchType\":\"normalizedContains\",\"pattern\":\"DDP\",\"score\":15,\"mutexGroup\":\"Codec\",\"enabled\":true},{\"name\":\"DD\",\"matchType\":\"normalizedContains\",\"pattern\":\"DD\",\"score\":10,\"mutexGroup\":\"Codec\",\"enabled\":true},{\"name\":\"Atmos\",\"matchType\":\"normalizedContains\",\"pattern\":\"Atmos\",\"score\":50,\"enabled\":true}]",
                MutexGroups = "[{\"name\":\"Channels\",\"enabled\":true},{\"name\":\"Codec\",\"enabled\":true}]"
            });

            Insert.IntoTable("AudioLanguagePreferences").Row(new
            {
                Name = "Chinese then Origin",
                Enabled = true,
                ScoreGapThreshold = 80,
                AudioScoreProfileId = (int?)null,
                Entries = "[{\"languageTag\":\"Chinese\",\"enabled\":true},{\"languageTag\":\"Origin\",\"enabled\":true}]"
            });
        }
    }
}
