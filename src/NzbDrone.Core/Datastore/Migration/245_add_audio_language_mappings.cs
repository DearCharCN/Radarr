using FluentMigrator;
using NzbDrone.Core.Datastore.Migration.Framework;
using NzbDrone.Core.Languages;

namespace NzbDrone.Core.Datastore.Migration
{
    [Migration(245)]
    public class add_audio_language_mappings : NzbDroneMigrationBase
    {
        protected override void MainDbUpgrade()
        {
            Create.TableForModel("AudioLanguageMappings")
                  .WithColumn("Language").AsInt32().NotNullable()
                  .WithColumn("Aliases").AsString().NotNullable()
                  .WithColumn("Enabled").AsBoolean().WithDefaultValue(true);

            Insert.IntoTable("AudioLanguageMappings").Row(new
            {
                Language = (int)Language.Chinese,
                Aliases = "[\"Chinese\",\"Mandarin\",\"Guoyu\",\"Putonghua\",\"Cantonese\",\"ZH\",\"ZHO\",\"CHI\",\"CMN\",\"YUE\",\"CHS\",\"CHT\",\"CN\"]",
                Enabled = true
            });
        }
    }
}
