using Prometheus;

namespace BMIRussian_ru.Services
{
    /// <summary>
    /// Service for tracking application metrics for Grafana/Prometheus.
    /// Labels stay low-cardinality: source, result, host, model.
    /// </summary>
    public class MetricsService
    {
        private static readonly Counter RegisteredUsersCounter = Metrics
            .CreateCounter(
                "bmirussian_registered_users_total",
                "Total number of registered users",
                new CounterConfiguration
                {
                    LabelNames = new[] { "source" }
                });

        private static readonly Counter LoginsCounter = Metrics
            .CreateCounter(
                "bmirussian_logins_total",
                "Successful sign-ins of existing accounts",
                new CounterConfiguration
                {
                    LabelNames = new[] { "source" }
                });

        private static readonly Counter CredentialsLinkedCounter = Metrics
            .CreateCounter(
                "bmirussian_credentials_linked_total",
                "Credentials linked to an existing account",
                new CounterConfiguration
                {
                    LabelNames = new[] { "source" }
                });

        private static readonly Counter AgreementsAcceptedCounter = Metrics
            .CreateCounter(
                "bmirussian_agreements_accepted_total",
                "Legal agreements accepted by users");

        private static readonly Counter VideoViewsCounter = Metrics
            .CreateCounter(
                "bmirussian_video_views_total",
                "Published video pages rendered");

        private static readonly Counter VideoSearchesCounter = Metrics
            .CreateCounter(
                "bmirussian_video_searches_total",
                "Catalog searches",
                new CounterConfiguration
                {
                    LabelNames = new[] { "result" }
                });

        private static readonly Counter VideosImportedCounter = Metrics
            .CreateCounter(
                "bmirussian_videos_imported_total",
                "Admin video import attempts",
                new CounterConfiguration
                {
                    LabelNames = new[] { "result", "host" }
                });

        private static readonly Counter VideosPublishedCounter = Metrics
            .CreateCounter(
                "bmirussian_videos_published_total",
                "Videos created as published or changed to published");

        private static readonly Counter TranscriptsUploadedCounter = Metrics
            .CreateCounter(
                "bmirussian_transcripts_uploaded_total",
                "Transcript files uploaded");

        private static readonly Counter VideosDeletedCounter = Metrics
            .CreateCounter(
                "bmirussian_videos_deleted_total",
                "Videos deleted by an admin");

        private static readonly Counter StlJobsCounter = Metrics
            .CreateCounter(
                "bmirussian_stl_jobs_total",
                "STL generation jobs by outcome",
                new CounterConfiguration
                {
                    LabelNames = new[] { "model", "result" }
                });

        private static readonly Histogram StlJobDuration = Metrics
            .CreateHistogram(
                "bmirussian_stl_job_duration_seconds",
                "Time from STL job creation to completion",
                new HistogramConfiguration
                {
                    LabelNames = new[] { "model", "result" },
                    Buckets = new[] { 1.0, 2, 5, 10, 30, 60, 120, 300, 600 }
                });

        private static readonly Counter StlDownloadsCounter = Metrics
            .CreateCounter(
                "bmirussian_stl_downloads_total",
                "STL files downloaded",
                new CounterConfiguration
                {
                    LabelNames = new[] { "model" }
                });

        private static readonly Counter StlPreviewsCounter = Metrics
            .CreateCounter(
                "bmirussian_stl_previews_total",
                "STL engraving previews rendered");

        private static readonly Counter MediaInfoResultsCounter = Metrics
            .CreateCounter(
                "bmirussian_mediainfo_results_total",
                "Video metadata parse results",
                new CounterConfiguration
                {
                    LabelNames = new[] { "result" }
                });

        /// <param name="source">Registration source: telegram, google, vk.</param>
        public void IncrementRegisteredUsers(string source = "unknown")
        {
            RegisteredUsersCounter.WithLabels(Label(source)).Inc();
        }

        public void IncrementLogins(string source)
        {
            LoginsCounter.WithLabels(Label(source)).Inc();
        }

        public void IncrementCredentialsLinked(string source)
        {
            CredentialsLinkedCounter.WithLabels(Label(source)).Inc();
        }

        public void IncrementAgreementsAccepted(int count = 1)
        {
            if (count > 0)
                AgreementsAcceptedCounter.Inc(count);
        }

        public void IncrementVideoViews() => VideoViewsCounter.Inc();

        public void IncrementVideoSearches(string result) =>
            VideoSearchesCounter.WithLabels(Label(result)).Inc();

        public void IncrementVideosImported(string result, string host) =>
            VideosImportedCounter.WithLabels(Label(result), Label(host)).Inc();

        public void IncrementVideosPublished() => VideosPublishedCounter.Inc();

        public void IncrementTranscriptsUploaded() => TranscriptsUploadedCounter.Inc();

        public void IncrementVideosDeleted() => VideosDeletedCounter.Inc();

        public void IncrementStlJobs(string model, string result) =>
            StlJobsCounter.WithLabels(Label(model), Label(result)).Inc();

        public void ObserveStlJobDuration(string model, string result, double seconds)
        {
            if (double.IsNaN(seconds) || double.IsInfinity(seconds))
                return;
            if (seconds < 0)
                seconds = 0;
            StlJobDuration.WithLabels(Label(model), Label(result)).Observe(seconds);
        }

        public void IncrementStlDownloads(string model) =>
            StlDownloadsCounter.WithLabels(Label(model)).Inc();

        public void IncrementStlPreviews() => StlPreviewsCounter.Inc();

        public void IncrementMediaInfoResults(string result) =>
            MediaInfoResultsCounter.WithLabels(Label(result)).Inc();

        private static string Label(string? value) =>
            string.IsNullOrWhiteSpace(value) ? "unknown" : value;
    }
}
