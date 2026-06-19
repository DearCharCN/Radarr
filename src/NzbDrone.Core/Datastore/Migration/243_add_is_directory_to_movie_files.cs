using FluentMigrator;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace NzbDrone.Core.Datastore.Migration
{
    [Migration(243)]
    public class add_is_directory_to_movie_files : NzbDroneMigrationBase
    {
        protected override void MainDbUpgrade()
        {
            Alter.Table("MovieFiles").AddColumn("IsDirectory").AsBoolean().WithDefaultValue(false);
        }
    }
}
