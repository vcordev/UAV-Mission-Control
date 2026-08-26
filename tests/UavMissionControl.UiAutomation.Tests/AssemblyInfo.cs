using Xunit;

// UI Automation's COM interop is not safe to call concurrently from multiple threads, and each
// test also launches and controls its own real app window - running test classes in parallel
// (xUnit's default) produced intermittent "Unexpected HRESULT... COM component" failures in this
// session. UI automation suites are conventionally run serially for exactly this reason.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
