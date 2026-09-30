using FluentMigrator;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace NzbDrone.Core.Datastore.Migration
{
    [Migration(23)]
    public class postgres_update_timestamp_columns_to_with_timezone : NzbDroneMigrationBase
    {
        protected override void MainDbUpgrade()
        {
            Delete.FromTable("Commands").AllRows();

            Alter.AlterDateTimeOffsetColumn("Authors", "LastInfoSync").Nullable();
            Alter.AlterDateTimeOffsetColumn("Authors", "Added").Nullable();
            Alter.AlterDateTimeOffsetColumn("AuthorMetadata", "Born").Nullable();
            Alter.AlterDateTimeOffsetColumn("AuthorMetadata", "Died").Nullable();
            Alter.AlterDateTimeOffsetColumn("Blocklist", "Date").NotNullable();
            Alter.AlterDateTimeOffsetColumn("Blocklist", "PublishedDate").Nullable();
            Alter.AlterDateTimeOffsetColumn("Books", "ReleaseDate").Nullable();
            Alter.AlterDateTimeOffsetColumn("Books", "LastInfoSync").Nullable();
            Alter.AlterDateTimeOffsetColumn("Books", "Added").Nullable();
            Alter.AlterDateTimeOffsetColumn("BookFiles", "DateAdded").Nullable();
            Alter.AlterDateTimeOffsetColumn("BookFiles", "Modified").Nullable();
            Alter.AlterDateTimeOffsetColumn("Commands", "QueuedAt").NotNullable();
            Alter.AlterDateTimeOffsetColumn("Commands", "StartedAt").Nullable();
            Alter.AlterDateTimeOffsetColumn("Commands", "EndedAt").Nullable();
            Alter.AlterDateTimeOffsetColumn("DownloadClientStatus", "InitialFailure").Nullable();
            Alter.AlterDateTimeOffsetColumn("DownloadClientStatus", "MostRecentFailure").Nullable();
            Alter.AlterDateTimeOffsetColumn("DownloadClientStatus", "DisabledTill").Nullable();
            Alter.AlterDateTimeOffsetColumn("Editions", "ReleaseDate").Nullable();
            Alter.AlterDateTimeOffsetColumn("ExtraFiles", "Added").NotNullable();
            Alter.AlterDateTimeOffsetColumn("ExtraFiles", "LastUpdated").NotNullable();
            Alter.AlterDateTimeOffsetColumn("History", "Date").NotNullable();
            Alter.AlterDateTimeOffsetColumn("ImportListStatus", "InitialFailure").Nullable();
            Alter.AlterDateTimeOffsetColumn("ImportListStatus", "MostRecentFailure").Nullable();
            Alter.AlterDateTimeOffsetColumn("ImportListStatus", "DisabledTill").Nullable();
            Alter.AlterDateTimeOffsetColumn("IndexerStatus", "InitialFailure").Nullable();
            Alter.AlterDateTimeOffsetColumn("IndexerStatus", "MostRecentFailure").Nullable();
            Alter.AlterDateTimeOffsetColumn("IndexerStatus", "DisabledTill").Nullable();
            Alter.AlterDateTimeOffsetColumn("MetadataFiles", "LastUpdated").NotNullable();
            Alter.AlterDateTimeOffsetColumn("MetadataFiles", "Added").Nullable();
            Alter.AlterDateTimeOffsetColumn("PendingReleases", "Added").NotNullable();
            Alter.AlterDateTimeOffsetColumn("ScheduledTasks", "LastExecution").NotNullable();
            Alter.AlterDateTimeOffsetColumn("ScheduledTasks", "LastStartTime").Nullable();
            Alter.AlterDateTimeOffsetColumn("VersionInfo", "AppliedOn").Nullable();
        }

        protected override void LogDbUpgrade()
        {
            Alter.AlterDateTimeOffsetColumn("Logs", "Time").NotNullable();
            Alter.AlterDateTimeOffsetColumn("VersionInfo", "AppliedOn").Nullable();
        }

        protected override void CacheDbUpgrade()
        {
            Alter.AlterDateTimeOffsetColumn("HttpResponse", "LastRefresh").Nullable();
            Alter.AlterDateTimeOffsetColumn("HttpResponse", "Expiry").Nullable();
        }
    }
}
