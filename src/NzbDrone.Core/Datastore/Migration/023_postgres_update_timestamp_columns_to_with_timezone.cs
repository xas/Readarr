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

            Alter.Table("Authors").AlterColumn("LastInfoSync").AsDateTimeOffsetCompatible().Nullable();
            Alter.Table("Authors").AlterColumn("Added").AsDateTimeOffsetCompatible().Nullable();
            Alter.Table("AuthorMetadata").AlterColumn("Born").AsDateTimeOffsetCompatible().Nullable();
            Alter.Table("AuthorMetadata").AlterColumn("Died").AsDateTimeOffsetCompatible().Nullable();
            Alter.Table("Blocklist").AlterColumn("Date").AsDateTimeOffsetCompatible().NotNullable();
            Alter.Table("Blocklist").AlterColumn("PublishedDate").AsDateTimeOffsetCompatible().Nullable();
            Alter.Table("Books").AlterColumn("ReleaseDate").AsDateTimeOffsetCompatible().Nullable();
            Alter.Table("Books").AlterColumn("LastInfoSync").AsDateTimeOffsetCompatible().Nullable();
            Alter.Table("Books").AlterColumn("Added").AsDateTimeOffsetCompatible().Nullable();
            Alter.Table("BookFiles").AlterColumn("DateAdded").AsDateTimeOffsetCompatible().Nullable();
            Alter.Table("BookFiles").AlterColumn("Modified").AsDateTimeOffsetCompatible().Nullable();
            Alter.Table("Commands").AlterColumn("QueuedAt").AsDateTimeOffsetCompatible().NotNullable();
            Alter.Table("Commands").AlterColumn("StartedAt").AsDateTimeOffsetCompatible().Nullable();
            Alter.Table("Commands").AlterColumn("EndedAt").AsDateTimeOffsetCompatible().Nullable();
            Alter.Table("DownloadClientStatus").AlterColumn("InitialFailure").AsDateTimeOffsetCompatible().Nullable();
            Alter.Table("DownloadClientStatus").AlterColumn("MostRecentFailure").AsDateTimeOffsetCompatible().Nullable();
            Alter.Table("DownloadClientStatus").AlterColumn("DisabledTill").AsDateTimeOffsetCompatible().Nullable();
            Alter.Table("Editions").AlterColumn("ReleaseDate").AsDateTimeOffsetCompatible().Nullable();
            Alter.Table("ExtraFiles").AlterColumn("Added").AsDateTimeOffsetCompatible().NotNullable();
            Alter.Table("ExtraFiles").AlterColumn("LastUpdated").AsDateTimeOffsetCompatible().NotNullable();
            Alter.Table("History").AlterColumn("Date").AsDateTimeOffsetCompatible().NotNullable();
            Alter.Table("ImportListStatus").AlterColumn("InitialFailure").AsDateTimeOffsetCompatible().Nullable();
            Alter.Table("ImportListStatus").AlterColumn("MostRecentFailure").AsDateTimeOffsetCompatible().Nullable();
            Alter.Table("ImportListStatus").AlterColumn("DisabledTill").AsDateTimeOffsetCompatible().Nullable();
            Alter.Table("IndexerStatus").AlterColumn("InitialFailure").AsDateTimeOffsetCompatible().Nullable();
            Alter.Table("IndexerStatus").AlterColumn("MostRecentFailure").AsDateTimeOffsetCompatible().Nullable();
            Alter.Table("IndexerStatus").AlterColumn("DisabledTill").AsDateTimeOffsetCompatible().Nullable();
            Alter.Table("MetadataFiles").AlterColumn("LastUpdated").AsDateTimeOffsetCompatible().NotNullable();
            Alter.Table("MetadataFiles").AlterColumn("Added").AsDateTimeOffsetCompatible().Nullable();
            Alter.Table("PendingReleases").AlterColumn("Added").AsDateTimeOffsetCompatible().NotNullable();
            Alter.Table("ScheduledTasks").AlterColumn("LastExecution").AsDateTimeOffsetCompatible().NotNullable();
            Alter.Table("ScheduledTasks").AlterColumn("LastStartTime").AsDateTimeOffsetCompatible().Nullable();
            Alter.Table("VersionInfo").AlterColumn("AppliedOn").AsDateTimeOffsetCompatible().Nullable();
        }

        protected override void LogDbUpgrade()
        {
            Alter.Table("Logs").AlterColumn("Time").AsDateTimeOffsetCompatible().NotNullable();
            Alter.Table("VersionInfo").AlterColumn("AppliedOn").AsDateTimeOffsetCompatible().Nullable();
        }

        protected override void CacheDbUpgrade()
        {
            Alter.Table("HttpResponse").AlterColumn("LastRefresh").AsDateTimeOffsetCompatible().Nullable();
            Alter.Table("HttpResponse").AlterColumn("Expiry").AsDateTimeOffsetCompatible().Nullable();
        }
    }
}
