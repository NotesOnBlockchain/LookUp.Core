namespace LookUp.Scanner.Services
{
    // What the health check knows about scanning. ScannerService reports progress after every stored block and on
    // every pass that finds nothing to scan, so a scanner stuck retrying the same block stops reporting.
    public class ScanStatus
    {
        // A block takes seconds and an idle pass runs every minute; this long without progress means scanning is stuck.
        public static readonly TimeSpan StalledAfter = TimeSpan.FromMinutes(15);

        private readonly object _lock = new();
        private int? _nextBlockHeight;
        private DateTimeOffset _lastProgressAt = DateTimeOffset.UtcNow;

        public void ReportProgress(int nextBlockHeight)
        {
            lock (_lock)
            {
                _nextBlockHeight = nextBlockHeight;
                _lastProgressAt = DateTimeOffset.UtcNow;
            }
        }

        // Before the first report, the time counts from startup.
        public (int? NextBlockHeight, DateTimeOffset LastProgressAt, bool Stalled) Get()
        {
            lock (_lock)
            {
                return (_nextBlockHeight, _lastProgressAt, DateTimeOffset.UtcNow - _lastProgressAt > StalledAfter);
            }
        }
    }
}
