using FluentMigrator;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace NzbDrone.Core.Datastore.Migration
{
    [Migration(032)]
    public class postgres_update_timestamp_columns_to_with_timezone_part2 : NzbDroneMigrationBase
    {
        protected override void MainDbUpgrade()
        {
            Alter.AlterDateTimeOffsetColumn("DownloadHistory", "Date").Nullable();
            Alter.AlterDateTimeOffsetColumn("ImportListStatus", "LastInfoSync").Nullable();
        }
    }
}
