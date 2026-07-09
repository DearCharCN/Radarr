using FluentMigrator;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace NzbDrone.Core.Datastore.Migration
{
    [Migration(244)]
    public class add_release_filter_profiles : NzbDroneMigrationBase
    {
        protected override void MainDbUpgrade()
        {
            Create.TableForModel("ReleaseFilterProfiles")
                  .WithColumn("Name").AsString().NotNullable()
                  .WithColumn("Enabled").AsBoolean().WithDefaultValue(true)
                  .WithColumn("Filter").AsString().NotNullable();

            Alter.Table("QualityProfiles").AddColumn("ReleaseFilterProfileId").AsInt32().Nullable();
        }
    }
}
